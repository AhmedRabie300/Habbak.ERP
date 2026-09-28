using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Habbak.ERP.Application.Settings;
using Habbak.ERP.Application.Settings.Auth;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// Two-factor sign-in (Docs/Modules/Two-Factor-SignIn.md): an authenticator code after the
/// password, single-use recovery codes, lockout on guessed codes, and the administrator reset.
/// </summary>
public class TwoFactorTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static readonly BcryptPasswordHasher Hasher = new();
    private const string Password = "Strong@123";

    private sealed record Seeded(long CompanyId, long UserId, string Username);

    private async Task<Seeded> SeedAsync()
    {
        await using var db = factory.CreateDirectDbContext(0);
        var currency = await db.Currencies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Code == "EGP");
        if (currency is null)
        {
            currency = new Currency { Code = "EGP", NameAr = "جنيه مصري", NameEn = "Egyptian Pound", IsActive = true };
            db.Currencies.Add(currency);
            await db.SaveChangesAsync();
        }

        if (!await db.MenuItems.AnyAsync())
        {
            db.MenuItems.AddRange(Habbak.ERP.Infrastructure.Persistence.Seeding.MenuItemSeedData.Build());
            await db.SaveChangesAsync();
        }

        var company = new Company { Code = $"T{Guid.NewGuid():N}"[..12], NameAr = "شركة", NameEn = "Co", BaseCurrencyId = currency.Id, IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        await CompanySecurityDefaults.AddAsync(db, company.Id, DateTime.UtcNow, 0, default);
        await db.SaveChangesAsync();

        var roleId = await db.Roles.IgnoreQueryFilters().Where(r => r.CompanyId == company.Id && r.Code == SystemRoles.CompanyAdmin).Select(r => r.Id).SingleAsync();
        var username = $"u{Guid.NewGuid():N}"[..14];
        var user = new User
        {
            Username = username, Email = $"{username}@test.local", FullName = "مستخدم", PasswordHash = Hasher.Hash(Password),
            Status = UserStatus.Active, PasswordChangedAtUtc = DateTime.UtcNow
        };
        user.UserRoles.Add(new UserRole { RoleId = roleId, AssignedAtUtc = DateTime.UtcNow, AssignedByUserId = 0 });
        user.UserScopes.Add(new UserScope { CompanyId = company.Id, RoleInScope = SystemRoles.CompanyAdmin, IsDefault = true, IsActive = true, GrantedAtUtc = DateTime.UtcNow });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return new Seeded(company.Id, user.Id, username);
    }

    /// <summary>The user's own session (test auth) — for the settings endpoints.</summary>
    private HttpClient Self(Seeded s)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, s.CompanyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, s.UserId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, SystemRoles.CompanyAdmin);
        return client;
    }

    private static string CodeAt(string secret, long stepOffset = 0) =>
        Totp.Code(Totp.FromBase32(secret), Totp.StepAt(DateTime.UtcNow) + stepOffset);

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()!;

    /// <summary>Turns two-factor sign-in on for the user; returns the secret and the recovery codes.</summary>
    private async Task<(string Secret, List<string> RecoveryCodes)> EnableAsync(Seeded s)
    {
        var self = Self(s);
        var setup = await (await self.PostAsync("/api/v1/auth/two-factor/setup", null)).Content.ReadFromJsonAsync<JsonElement>();
        var secret = setup.GetProperty("secret").GetString()!;
        Assert.StartsWith("otpauth://totp/", setup.GetProperty("setupUri").GetString());

        var enabled = await self.PostAsJsonAsync("/api/v1/auth/two-factor/enable", new { code = CodeAt(secret) });
        enabled.EnsureSuccessStatusCode();
        var codes = (await enabled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToList();
        return (secret, codes);
    }

    private async Task<JsonElement> LoginAsync(Seeded s)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { username = s.Username, password = Password });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> SecondStepAsync(string challenge, string? code = null, string? recoveryCode = null) =>
        factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login/two-factor", new { challengeToken = challenge, code, recoveryCode });

    // ------------------------------------------------------------------------------------ TOTP

    [Fact]
    public void Codes_match_the_rfc_6238_test_vectors()
    {
        var key = Encoding.ASCII.GetBytes("12345678901234567890");

        Assert.Equal("94287082", Totp.Code(key, 59 / 30, 8));
        Assert.Equal("07081804", Totp.Code(key, 1111111109 / 30, 8));
        Assert.Equal("89005924", Totp.Code(key, 1234567890 / 30, 8));
        Assert.Equal(key, Totp.FromBase32(Totp.ToBase32(key)));
    }

    // ---------------------------------------------------------------------------------- sign-in

    [Fact]
    public async Task With_two_factor_on_the_password_alone_gives_a_challenge_and_the_code_gives_the_session()
    {
        var s = await SeedAsync();
        var (secret, codes) = await EnableAsync(s);
        Assert.Equal(10, codes.Count);

        var first = await LoginAsync(s);
        Assert.True(first.GetProperty("twoFactorRequired").GetBoolean());
        Assert.False(first.TryGetProperty("accessToken", out _));

        // The enable step used the current step; the next one is still inside the window.
        var session = await SecondStepAsync(first.GetProperty("challengeToken").GetString()!, CodeAt(secret, 1));
        session.EnsureSuccessStatusCode();
        Assert.False(string.IsNullOrEmpty((await session.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()));

        // The same code a second time is refused — a code is good once.
        var again = await LoginAsync(s);
        var replay = await SecondStepAsync(again.GetProperty("challengeToken").GetString()!, CodeAt(secret, 1));
        Assert.Equal("AUTH-2FA-INVALID-CODE", await ErrorCodeAsync(replay));
    }

    [Fact]
    public async Task A_recovery_code_signs_in_once()
    {
        var s = await SeedAsync();
        var (_, codes) = await EnableAsync(s);

        var ok = await SecondStepAsync((await LoginAsync(s)).GetProperty("challengeToken").GetString()!, recoveryCode: codes[0].ToLowerInvariant());
        ok.EnsureSuccessStatusCode();

        var reused = await SecondStepAsync((await LoginAsync(s)).GetProperty("challengeToken").GetString()!, recoveryCode: codes[0]);
        Assert.Equal("AUTH-2FA-INVALID-CODE", await ErrorCodeAsync(reused));

        var left = await Self(s).GetFromJsonAsync<JsonElement>("/api/v1/auth/two-factor");
        Assert.Equal(9, left.GetProperty("recoveryCodesLeft").GetInt32());
    }

    [Fact]
    public async Task Guessed_codes_lock_the_account_like_guessed_passwords()
    {
        var s = await SeedAsync();
        await EnableAsync(s);

        HttpResponseMessage last = null!;
        for (var i = 0; i < 5; i++)
        {
            last = await SecondStepAsync((await LoginAsync(s)).GetProperty("challengeToken").GetString()!, "000000");
        }

        Assert.Equal("AUTH-USER-LOCKED", await ErrorCodeAsync(last));
        await using var db = factory.CreateDirectDbContext(s.CompanyId);
        Assert.Equal(5, await db.LoginAttempts.CountAsync(a => a.UserId == s.UserId && a.FailureReason == "WrongTwoFactorCode"));
    }

    [Fact]
    public async Task A_forged_or_foreign_challenge_is_refused()
    {
        var s = await SeedAsync();
        var (secret, _) = await EnableAsync(s);
        var challenge = (await LoginAsync(s)).GetProperty("challengeToken").GetString()!;

        var forged = await SecondStepAsync(challenge[..^4] + "AAAA", CodeAt(secret, 1));
        Assert.Equal("AUTH-2FA-CHALLENGE-ENDED", await ErrorCodeAsync(forged));

        // A password change after the challenge was issued voids it.
        await using (var db = factory.CreateDirectDbContext(s.CompanyId))
        {
            var user = await db.Users.SingleAsync(u => u.Id == s.UserId);
            user.PasswordChangedAtUtc = DateTime.UtcNow.AddSeconds(5);
            await db.SaveChangesAsync();
        }

        Assert.Equal("AUTH-2FA-CHALLENGE-ENDED", await ErrorCodeAsync(await SecondStepAsync(challenge, CodeAt(secret, 1))));
    }

    // ------------------------------------------------------------------------------ settings

    [Fact]
    public async Task Turning_it_on_needs_a_code_from_the_app()
    {
        var s = await SeedAsync();
        var self = Self(s);

        Assert.Equal("AUTH-2FA-NO-SETUP", await ErrorCodeAsync(await self.PostAsJsonAsync("/api/v1/auth/two-factor/enable", new { code = "123456" })));

        var secret = (await (await self.PostAsync("/api/v1/auth/two-factor/setup", null)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("secret").GetString()!;
        var wrong = CodeAt(secret) == "000000" ? "111111" : "000000";
        Assert.Equal("AUTH-2FA-INVALID-CODE", await ErrorCodeAsync(await self.PostAsJsonAsync("/api/v1/auth/two-factor/enable", new { code = wrong })));

        // Still off: the password alone signs in.
        Assert.True((await LoginAsync(s)).TryGetProperty("accessToken", out _));

        (await self.PostAsJsonAsync("/api/v1/auth/two-factor/enable", new { code = CodeAt(secret) })).EnsureSuccessStatusCode();
        Assert.Equal("AUTH-2FA-ALREADY-ON", await ErrorCodeAsync(await self.PostAsync("/api/v1/auth/two-factor/setup", null)));
    }

    [Fact]
    public async Task Turning_it_off_takes_the_password_and_a_code()
    {
        var s = await SeedAsync();
        var (secret, _) = await EnableAsync(s);
        var self = Self(s);

        var wrongPassword = await self.PostAsJsonAsync("/api/v1/auth/two-factor/disable", new { password = "Wrong@123", code = CodeAt(secret, 1) });
        Assert.Equal("AUTH-WRONG-CURRENT-PASSWORD", await ErrorCodeAsync(wrongPassword));

        var noCode = await self.PostAsJsonAsync("/api/v1/auth/two-factor/disable", new { password = Password, code = "000000" });
        Assert.Equal("AUTH-2FA-INVALID-CODE", await ErrorCodeAsync(noCode));

        Assert.Equal(HttpStatusCode.NoContent,
            (await self.PostAsJsonAsync("/api/v1/auth/two-factor/disable", new { password = Password, code = CodeAt(secret, 1) })).StatusCode);
        Assert.True((await LoginAsync(s)).TryGetProperty("accessToken", out _));

        await using var db = factory.CreateDirectDbContext(s.CompanyId);
        Assert.False(await db.UserRecoveryCodes.AnyAsync(c => c.UserId == s.UserId));
    }

    [Fact]
    public async Task An_administrator_reset_turns_it_off_and_the_secret_never_reaches_the_audit_log()
    {
        var s = await SeedAsync();
        await EnableAsync(s);

        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, s.CompanyId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/settings/users/{s.UserId}/reset-two-factor", null)).StatusCode);

        Assert.True((await LoginAsync(s)).TryGetProperty("accessToken", out _));
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/settings/users/{s.UserId}");
        Assert.False(detail.GetProperty("twoFactorEnabled").GetBoolean());

        await using var db = factory.CreateDirectDbContext(s.CompanyId);
        var secretChanges = await db.AuditLogs.IgnoreQueryFilters()
            .Where(a => a.EntityType == "User" && a.EntityId == s.UserId && (a.FieldName == "TwoFactorSecret" || a.FieldName == "TwoFactorPendingSecret"))
            .ToListAsync();
        Assert.NotEmpty(secretChanges);
        Assert.All(secretChanges, a =>
        {
            Assert.True(a.OldValue is null or AuditLog.Redacted);
            Assert.True(a.NewValue is null or AuditLog.Redacted);
        });
    }

    [Fact]
    public async Task New_recovery_codes_replace_the_old_ones()
    {
        var s = await SeedAsync();
        var (secret, old) = await EnableAsync(s);

        var fresh = await Self(s).PostAsJsonAsync("/api/v1/auth/two-factor/recovery-codes", new { code = CodeAt(secret, 1) });
        fresh.EnsureSuccessStatusCode();
        var codes = (await fresh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToList();
        Assert.Equal(10, codes.Count);

        Assert.Equal("AUTH-2FA-INVALID-CODE",
            await ErrorCodeAsync(await SecondStepAsync((await LoginAsync(s)).GetProperty("challengeToken").GetString()!, recoveryCode: old[0])));
        (await SecondStepAsync((await LoginAsync(s)).GetProperty("challengeToken").GetString()!, recoveryCode: codes[0])).EnsureSuccessStatusCode();
    }
}
