using Habbak.ERP.Application.Organization.Companies.Commands.CreateCompany;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Persistence;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests;

/// <summary>
/// Settings &amp; Permissions, phase 1 (Docs/Modules/Settings-Permissions-Phase1.md): the domain rules
/// on their own, then the schema against a real migrated database — the unique indexes are
/// filtered SQL Server indexes, and the fixture database also proves the migration applies.
/// </summary>
public class SettingsPermissionsTests(PostingServiceFixture fixture) : IClassFixture<PostingServiceFixture>
{
    private static readonly BcryptPasswordHasher Hasher = new();

    private static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 13)];

    private static User NewUser(string? username = null, string? email = null)
    {
        username ??= Unique("user");
        return new User
        {
            Username = username,
            Email = email ?? $"{username}@test.local",
            PasswordHash = "$2a$12$placeholderplaceholderplaceholderplaceholderplacehold",
            FullName = "مستخدم اختبار",
            Status = UserStatus.Active
        };
    }

    private async Task<long> CreateCompanyAsync()
    {
        await using var db = fixture.CreateContext();
        var currency = await db.Currencies.IgnoreQueryFilters().FirstOrDefaultAsync();
        if (currency is null)
        {
            currency = new Currency { Code = "EGP", NameAr = "جنيه مصري", NameEn = "Egyptian Pound", IsActive = true };
            db.Currencies.Add(currency);
            await db.SaveChangesAsync();
        }

        var company = new Company { Code = Unique("CO"), NameAr = "شركة اختبار", NameEn = "Test Co", BaseCurrencyId = currency.Id };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    private async Task<Role> AddRoleAsync(long companyId, string? code = null)
    {
        await using var db = fixture.CreateContext();
        var role = new Role { CompanyId = companyId, Code = code ?? Unique("ROLE"), NameAr = "دور", NameEn = "Role" };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    private async Task<User> AddUserAsync()
    {
        await using var db = fixture.CreateContext();
        var user = NewUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    // ---------------------------------------------------------------- domain rules (no database)

    [Fact]
    public void Password_is_stored_as_a_verifiable_bcrypt_hash()
    {
        var hash = Hasher.Hash("Admin@123");

        Assert.StartsWith("$2", hash);
        Assert.DoesNotContain("Admin@123", hash);
        Assert.True(Hasher.Verify("Admin@123", hash));
        Assert.False(Hasher.Verify("admin@123", hash));
        Assert.NotEqual(hash, Hasher.Hash("Admin@123")); // a fresh salt every time
        Assert.False(Hasher.Verify("Admin@123", "not-a-bcrypt-hash"));
    }

    [Fact]
    public void Short_password_is_rejected()
    {
        var problems = new SystemSettings().CheckPassword("Ab@1");

        Assert.Contains(problems, p => p.Contains('8'));
    }

    [Fact]
    public void Password_policy_names_each_missing_rule_and_accepts_a_strong_password()
    {
        var settings = new SystemSettings();

        // Long enough and lower-case, but no upper-case letter, digit or symbol.
        Assert.Equal(3, settings.CheckPassword("abcdefgh").Count);
        Assert.Empty(settings.CheckPassword("Strong@123"));

        var relaxed = new SystemSettings { PasswordRequireSpecial = false, PasswordRequireUppercase = false };
        Assert.Empty(relaxed.CheckPassword("simple123"));
    }

    [Fact]
    public void Account_locks_on_the_fifth_failed_attempt()
    {
        var user = NewUser();
        var now = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);

        for (var i = 1; i <= 4; i++)
        {
            user.RegisterFailedLogin(5, 15, now);
            Assert.Equal(UserStatus.Active, user.Status);
        }

        user.RegisterFailedLogin(5, 15, now);

        Assert.Equal(UserStatus.Locked, user.Status);
        Assert.Equal(now.AddMinutes(15), user.LockedUntilUtc);
        Assert.Equal(5, user.FailedLoginAttempts);
    }

    [Fact]
    public void Lock_is_released_after_fifteen_minutes_and_not_before()
    {
        var user = NewUser();
        var lockedAt = new DateTime(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++) user.RegisterFailedLogin(5, 15, lockedAt);

        Assert.False(user.ReleaseExpiredLock(lockedAt.AddMinutes(14)));
        Assert.Equal(UserStatus.Locked, user.Status);

        Assert.True(user.ReleaseExpiredLock(lockedAt.AddMinutes(15)));
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.LockedUntilUtc);
        Assert.Equal(0, user.FailedLoginAttempts);
    }

    [Fact]
    public void Successful_login_clears_the_failure_count_and_a_suspended_user_is_never_auto_unlocked()
    {
        var user = NewUser();
        var now = DateTime.UtcNow;
        user.RegisterFailedLogin(5, 15, now);
        user.RegisterFailedLogin(5, 15, now);
        user.RegisterSuccessfulLogin(now);

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Equal(now, user.LastLoginAtUtc);

        var suspended = NewUser();
        suspended.Status = UserStatus.Suspended;
        for (var i = 0; i < 6; i++) suspended.RegisterFailedLogin(5, 15, now);
        Assert.Equal(UserStatus.Suspended, suspended.Status);
        Assert.False(suspended.ReleaseExpiredLock(now.AddDays(1)));
    }

    [Fact]
    public void Audit_entries_never_hold_sensitive_values_in_clear_text()
    {
        var sensitive = AuditLog.FieldChange(1, 7, "Customer", 42, "CreditLimit", "5000", "9000", DateTime.UtcNow);
        var ordinary = AuditLog.FieldChange(1, 7, "Customer", 42, "NameAr", "قديم", "جديد", DateTime.UtcNow);
        var cleared = AuditLog.FieldChange(1, 7, "Customer", 42, "Phone", "0100", null, DateTime.UtcNow);

        Assert.Equal(AuditLog.Redacted, sensitive.OldValue);
        Assert.Equal(AuditLog.Redacted, sensitive.NewValue);
        Assert.Equal("قديم", ordinary.OldValue);
        Assert.Equal("جديد", ordinary.NewValue);
        Assert.Equal(AuditLog.Redacted, cleared.OldValue);
        Assert.Null(cleared.NewValue);
    }

    [Fact]
    public void Refresh_token_is_active_only_until_it_expires_or_is_revoked()
    {
        Assert.True(new RefreshToken { ExpiresAtUtc = DateTime.UtcNow.AddDays(1) }.IsActive);
        Assert.False(new RefreshToken { ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1) }.IsActive);
        Assert.False(new RefreshToken { ExpiresAtUtc = DateTime.UtcNow.AddDays(1), RevokedAtUtc = DateTime.UtcNow }.IsActive);
    }

    // ---------------------------------------------------------------- schema (real database)

    [Fact]
    public async Task User_is_saved_and_read_back()
    {
        var user = NewUser();
        user.PasswordHash = Hasher.Hash("Strong@123");
        await using (var db = fixture.CreateContext())
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext();
        var loaded = await read.Users.SingleAsync(u => u.Username == user.Username);
        Assert.Equal(UserStatus.Active, loaded.Status);
        Assert.Equal(PreferredLanguage.Arabic, loaded.PreferredLanguage);
        Assert.True(Hasher.Verify("Strong@123", loaded.PasswordHash));
    }

    [Fact]
    public async Task Duplicate_username_is_rejected()
    {
        var first = await AddUserAsync();

        await using var db = fixture.CreateContext();
        db.Users.Add(NewUser(first.Username, $"other-{Guid.NewGuid():N}@test.local"));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("IX_Users_Username", ex.InnerException!.Message);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        var first = await AddUserAsync();

        await using var db = fixture.CreateContext();
        db.Users.Add(NewUser(Unique("user"), first.Email));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("IX_Users_Email", ex.InnerException!.Message);
    }

    [Fact]
    public async Task A_deleted_users_username_can_be_taken_again()
    {
        var first = await AddUserAsync();
        await using (var db = fixture.CreateContext())
        {
            var stored = await db.Users.SingleAsync(u => u.Id == first.Id);
            stored.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        await using var again = fixture.CreateContext();
        again.Users.Add(NewUser(first.Username, first.Email));
        await again.SaveChangesAsync();
    }

    [Fact]
    public async Task Role_is_assigned_to_a_user_once_per_branch()
    {
        var companyId = await CreateCompanyAsync();
        var role = await AddRoleAsync(companyId);
        var user = await AddUserAsync();

        await using (var db = fixture.CreateContext())
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAtUtc = DateTime.UtcNow, AssignedByUserId = 1 });
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, BranchId = 99, AssignedAtUtc = DateTime.UtcNow, AssignedByUserId = 1 });
            await db.SaveChangesAsync();
        }

        await using (var read = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var loaded = await read.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).SingleAsync(u => u.Id == user.Id);
            Assert.Equal(2, loaded.UserRoles.Count);
            Assert.All(loaded.UserRoles, ur => Assert.Equal(role.Code, ur.Role.Code));
        }

        await using var dup = fixture.CreateContext();
        dup.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAtUtc = DateTime.UtcNow, AssignedByUserId = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
    }

    [Fact]
    public async Task Scope_is_granted_and_a_second_company_wide_scope_is_rejected()
    {
        var companyId = await CreateCompanyAsync();
        var user = await AddUserAsync();

        await using (var db = fixture.CreateContext())
        {
            db.UserScopes.Add(new UserScope { UserId = user.Id, CompanyId = companyId, RoleInScope = SystemRoles.Accountant, IsDefault = true, GrantedAtUtc = DateTime.UtcNow, GrantedByUserId = 1 });
            db.UserScopes.Add(new UserScope { UserId = user.Id, CompanyId = companyId, BranchId = 5, RoleInScope = SystemRoles.Cashier, GrantedAtUtc = DateTime.UtcNow, GrantedByUserId = 1 });
            await db.SaveChangesAsync();
        }

        await using (var read = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var scopes = await read.UserScopes.Where(s => s.UserId == user.Id).ToListAsync();
            Assert.Equal(2, scopes.Count);
            Assert.True(scopes.Single(s => s.BranchId == null).IsActive);
        }

        await using var dup = fixture.CreateContext();
        dup.UserScopes.Add(new UserScope { UserId = user.Id, CompanyId = companyId, RoleInScope = SystemRoles.CompanyAdmin, GrantedAtUtc = DateTime.UtcNow, GrantedByUserId = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
    }

    [Fact]
    public async Task Screen_permission_is_one_row_per_role_and_screen()
    {
        var companyId = await CreateCompanyAsync();
        var role = await AddRoleAsync(companyId);

        await using (var db = fixture.CreateContext())
        {
            db.ScreenPermissions.Add(new ScreenPermission { CompanyId = companyId, RoleId = role.Id, ScreenCode = "SALES_INVOICES", CanView = true, CanAdd = true, CanApprove = false });
            await db.SaveChangesAsync();
        }

        await using (var read = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var permission = await read.ScreenPermissions.SingleAsync(p => p.RoleId == role.Id);
            Assert.True(permission.CanView && permission.CanAdd);
            Assert.False(permission.CanEdit || permission.CanDelete || permission.CanApprove);
        }

        await using var dup = fixture.CreateContext();
        dup.ScreenPermissions.Add(new ScreenPermission { CompanyId = companyId, RoleId = role.Id, ScreenCode = "SALES_INVOICES" });
        await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
    }

    [Fact]
    public async Task Field_permission_audits_by_default_but_can_be_switched_off()
    {
        var companyId = await CreateCompanyAsync();
        var role = await AddRoleAsync(companyId);

        await using (var db = fixture.CreateContext())
        {
            db.FieldPermissions.Add(new FieldPermission { CompanyId = companyId, RoleId = role.Id, ScreenCode = "SALES_CUSTOMERS", EntityType = "Customer", FieldName = "CreditLimit", CanView = true });
            db.FieldPermissions.Add(new FieldPermission { CompanyId = companyId, RoleId = role.Id, ScreenCode = "SALES_CUSTOMERS", EntityType = "Customer", FieldName = "Phone", CanView = true, RequiresAuditLog = false });
            await db.SaveChangesAsync();
        }

        await using (var read = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var rows = await read.FieldPermissions.Where(p => p.RoleId == role.Id).ToDictionaryAsync(p => p.FieldName);
            Assert.True(rows["CreditLimit"].RequiresAuditLog);
            Assert.False(rows["Phone"].RequiresAuditLog);
        }

        await using var dup = fixture.CreateContext();
        dup.FieldPermissions.Add(new FieldPermission { CompanyId = companyId, RoleId = role.Id, ScreenCode = "SALES_CUSTOMERS", EntityType = "Customer", FieldName = "CreditLimit" });
        await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
    }

    [Fact]
    public async Task Audit_log_is_written_and_queried_but_never_changed_or_deleted()
    {
        var entityId = Random.Shared.NextInt64(1_000_000, 9_000_000);
        await using (var db = fixture.CreateContext())
        {
            db.AuditLogs.Add(AuditLog.FieldChange(1, 7, "Customer", entityId, "CreditLimit", "5000", "9000", DateTime.UtcNow));
            db.AuditLogs.Add(new AuditLog { CompanyId = 1, UserId = 7, ActionType = AuditActionType.Approve, EntityType = "PurchaseInvoice", EntityId = entityId, OccurredAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext();
        var rows = await read.AuditLogs.Where(a => a.EntityId == entityId).OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(AuditLog.Redacted, rows[0].NewValue);
        Assert.Equal(AuditActionType.Approve, rows[1].ActionType);

        rows[1].EntityType = "Tampered";
        var update = await Assert.ThrowsAsync<InvalidOperationException>(() => read.SaveChangesAsync());
        Assert.StartsWith("AUDIT-LOG-APPEND-ONLY", update.Message);

        read.Entry(rows[1]).State = EntityState.Unchanged;
        read.AuditLogs.Remove(rows[0]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => read.SaveChangesAsync());
    }

    [Fact]
    public async Task Roles_query_returns_only_the_current_companys_roles()
    {
        var companyA = await CreateCompanyAsync();
        var companyB = await CreateCompanyAsync();
        var roleA = await AddRoleAsync(companyA, "SAME_CODE");
        var roleB = await AddRoleAsync(companyB, "SAME_CODE"); // the same code is fine in another company

        await using var asA = fixture.CreateContext(new TestCurrentCompanyContext(companyA));
        var visible = await asA.Roles.Select(r => r.Id).ToListAsync();

        Assert.Contains(roleA.Id, visible);
        Assert.DoesNotContain(roleB.Id, visible);

        await using var dup = fixture.CreateContext();
        dup.Roles.Add(new Role { CompanyId = companyA, Code = "SAME_CODE", NameAr = "مكرر", NameEn = "Dup" });
        await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
    }

    [Fact]
    public async Task Login_attempts_and_refresh_tokens_are_recorded()
    {
        var user = await AddUserAsync();
        var token = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray()));

        await using (var db = fixture.CreateContext())
        {
            db.LoginAttempts.Add(new LoginAttempt { Username = "ghost", Success = false, IpAddress = "10.0.0.1", FailureReason = "UnknownUser", AttemptedAtUtc = DateTime.UtcNow });
            db.LoginAttempts.Add(new LoginAttempt { UserId = user.Id, Username = user.Username, Success = true, IpAddress = "10.0.0.1", AttemptedAtUtc = DateTime.UtcNow });
            db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = token, ExpiresAtUtc = DateTime.UtcNow.AddDays(7), CreatedByIp = "10.0.0.1" });
            await db.SaveChangesAsync();
        }

        await using (var read = fixture.CreateContext())
        {
            Assert.Equal(2, await read.LoginAttempts.CountAsync(a => a.Username == "ghost" && !a.Success || a.UserId == user.Id));
            var stored = await read.RefreshTokens.SingleAsync(t => t.UserId == user.Id);
            Assert.True(stored.IsActive);
        }

        await using var dup = fixture.CreateContext();
        dup.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Token = token, ExpiresAtUtc = DateTime.UtcNow.AddDays(7), CreatedByIp = "10.0.0.2" });
        await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
    }

    [Fact]
    public async Task System_settings_hold_one_row_per_company()
    {
        var companyId = await CreateCompanyAsync();
        await using (var db = fixture.CreateContext())
        {
            db.SystemSettingsRows.Add(new SystemSettings { CompanyId = companyId });
            await db.SaveChangesAsync();
        }

        await using (var read = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var settings = await read.SystemSettingsRows.SingleAsync();
            Assert.Equal(8, settings.PasswordMinLength);
            Assert.Equal(5, settings.MaxFailedLoginAttempts);
            Assert.Equal(15, settings.AccountLockoutMinutes);
            Assert.Equal(7, settings.AuditRetentionYears);
        }

        await using var dup = fixture.CreateContext();
        dup.SystemSettingsRows.Add(new SystemSettings { CompanyId = companyId });
        await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
    }

    // ---------------------------------------------------------------- seed

    [Fact]
    public async Task Migration_seeds_an_active_admin_whose_password_is_Admin_at_123()
    {
        await using var db = fixture.CreateContext();
        var admin = await db.Users.SingleAsync(u => u.Username == "admin");

        Assert.Equal(UserStatus.Active, admin.Status);
        Assert.Equal("admin@habbak.com", admin.Email);
        Assert.True(Hasher.Verify("Admin@123", admin.PasswordHash));
        Assert.Equal(string.Empty, admin.PasswordSalt);
    }

    [Fact]
    public async Task New_company_gets_the_eight_system_roles_default_settings_and_super_admin_access()
    {
        // Make the seeded admin a super admin of some company first, as the migration does for
        // every company that already exists.
        var existingCompany = await CreateCompanyAsync();
        long adminId;
        await using (var db = fixture.CreateContext())
        {
            adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;
            var superAdmin = new Role { CompanyId = existingCompany, Code = SystemRoles.SuperAdmin, NameAr = "مدير النظام", NameEn = "System Administrator", IsSystemRole = true };
            db.Roles.Add(superAdmin);
            db.UserRoles.Add(new UserRole { UserId = adminId, Role = superAdmin, AssignedAtUtc = DateTime.UtcNow, AssignedByUserId = adminId });
            await db.SaveChangesAsync();
        }

        long companyId;
        await using (var db = fixture.CreateContext())
        {
            var currencyId = (await db.Currencies.IgnoreQueryFilters().FirstAsync()).Id;
            var handler = new CreateCompanyCommandHandler(db, new TestCurrentCompanyContext(existingCompany, userId: adminId));
            companyId = await handler.Handle(new CreateCompanyCommand(Unique("NEWCO"), "شركة جديدة", "New Co", null, null, currencyId), default);
        }

        await using var read = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var roles = await read.Roles.ToListAsync();
        Assert.Equal(SystemRoles.All.Select(r => r.Code).OrderBy(c => c), roles.Select(r => r.Code).OrderBy(c => c));
        Assert.All(roles, r => Assert.True(r.IsSystemRole && r.IsActive));

        Assert.Equal(5, (await read.SystemSettingsRows.SingleAsync()).MaxFailedLoginAttempts);

        var scope = await read.UserScopes.SingleAsync(s => s.UserId == adminId);
        Assert.Equal(SystemRoles.SuperAdmin, scope.RoleInScope);
        Assert.Null(scope.BranchId);
        Assert.True(await read.UserRoles.AnyAsync(ur => ur.UserId == adminId && ur.Role.Code == SystemRoles.SuperAdmin && ur.Role.CompanyId == companyId));
    }
}
