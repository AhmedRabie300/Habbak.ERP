using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habbak.ERP.Domain.POS;
using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// POS rule 31 (Settings &amp; Permissions, phase 3): only the cashier assigned to a terminal today
/// opens its shift; opening one for an unassigned cashier, or for somebody else, takes a supervisor
/// (Approve on the shift console screen). Who closes a shift is the signed-in user.
/// </summary>
public class ShiftOpeningRuleTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private sealed record Setup(CycleIds Ids, long CashierId, long OtherCashierId, long SupervisorId);

    private async Task<Setup> SeedAsync()
    {
        var companyId = Random.Shared.NextInt64(1, long.MaxValue);
        var ids = await CycleSeed.SeedAsync(factory, companyId);

        await using var db = factory.CreateDirectDbContext(companyId);
        User NewUser(string name) => new()
        {
            Username = $"{name}{Guid.NewGuid():N}"[..14], Email = $"{Guid.NewGuid():N}@test.local", FullName = name,
            PasswordHash = "!", Status = UserStatus.Active
        };

        var cashier = NewUser("cashier");
        var other = NewUser("other");
        var supervisor = NewUser("super");
        foreach (var u in new[] { cashier, other, supervisor })
        {
            u.UserScopes.Add(new UserScope { CompanyId = companyId, RoleInScope = SystemRoles.Cashier, IsDefault = true, IsActive = true, GrantedAtUtc = DateTime.UtcNow });
        }
        db.Users.AddRange(cashier, other, supervisor);

        // Cashiers may use the console; the supervisor role may also approve there (the override).
        var cashierRole = new Role { CompanyId = companyId, Code = SystemRoles.Cashier, NameAr = "كاشير", NameEn = "Cashier" };
        var supervisorRole = new Role { CompanyId = companyId, Code = SystemRoles.BranchManager, NameAr = "مدير فرع", NameEn = "Branch manager" };
        db.Roles.AddRange(cashierRole, supervisorRole);
        await db.SaveChangesAsync();

        db.ScreenPermissions.AddRange(
            new ScreenPermission { CompanyId = companyId, RoleId = cashierRole.Id, ScreenCode = "POS_SHIFT_CONSOLE", CanView = true, CanAdd = true, CanEdit = true },
            new ScreenPermission { CompanyId = companyId, RoleId = supervisorRole.Id, ScreenCode = "POS_SHIFT_CONSOLE", CanView = true, CanAdd = true, CanEdit = true, CanApprove = true });
        db.ShiftAssignments.Add(new ShiftAssignment
        {
            CompanyId = companyId, POSTerminalId = ids.PosTerminalId, UserId = cashier.Id, AssignedDate = DateOnly.FromDateTime(DateTime.Now)
        });
        await db.SaveChangesAsync();

        return new Setup(ids, cashier.Id, other.Id, supervisor.Id);
    }

    private HttpClient Client(long companyId, long userId, string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }

    private static Task<HttpResponseMessage> OpenAsync(HttpClient client, long terminalId, long? cashierUserId = null) =>
        client.PostAsJsonAsync("/api/v1/pos/shifts/open", new
        {
            posTerminalId = terminalId,
            cashierUserId,
            openingCounts = new[] { new { denominationValue = 100m, count = 2 } }
        });

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()!;

    [Fact]
    public async Task An_unassigned_cashier_cannot_open_the_terminal_and_the_assigned_one_can()
    {
        var s = await SeedAsync();

        var refused = await OpenAsync(Client(s.Ids.CompanyId, s.OtherCashierId, SystemRoles.Cashier), s.Ids.PosTerminalId);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("POS-SHIFT-NOT-ASSIGNED", await ErrorCodeAsync(refused));

        var opened = await OpenAsync(Client(s.Ids.CompanyId, s.CashierId, SystemRoles.Cashier), s.Ids.PosTerminalId);
        opened.EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(s.Ids.CompanyId);
        var shift = await db.Shifts.SingleAsync(x => x.POSTerminalId == s.Ids.PosTerminalId && x.Status == ShiftStatus.Open);
        Assert.Equal(s.CashierId, shift.CashierUserId);
        Assert.Equal(s.CashierId, shift.CreatedBy);
    }

    [Fact]
    public async Task Opening_for_somebody_else_takes_a_supervisor()
    {
        var s = await SeedAsync();

        var byCashier = await OpenAsync(Client(s.Ids.CompanyId, s.OtherCashierId, SystemRoles.Cashier), s.Ids.PosTerminalId, s.CashierId);
        Assert.Equal("POS-SHIFT-OPEN-FOR-OTHER", await ErrorCodeAsync(byCashier));

        // The supervisor opens for a cashier who is not even assigned today.
        var bySupervisor = await OpenAsync(Client(s.Ids.CompanyId, s.SupervisorId, SystemRoles.BranchManager), s.Ids.PosTerminalId, s.OtherCashierId);
        bySupervisor.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task A_cashier_id_that_is_no_user_of_the_company_is_refused_with_a_message()
    {
        var s = await SeedAsync();

        var unknown = await OpenAsync(Client(s.Ids.CompanyId, s.SupervisorId, SystemRoles.BranchManager), s.Ids.PosTerminalId, 987_654);
        Assert.Equal(HttpStatusCode.Conflict, unknown.StatusCode);
        Assert.Equal("SET-USER-NOT-IN-COMPANY", await ErrorCodeAsync(unknown));
    }

    [Fact]
    public async Task Whoever_closes_the_shift_is_recorded_as_its_closer()
    {
        var s = await SeedAsync();
        var cashier = Client(s.Ids.CompanyId, s.CashierId, SystemRoles.Cashier);
        (await OpenAsync(cashier, s.Ids.PosTerminalId)).EnsureSuccessStatusCode();

        long shiftId;
        string rowVersion;
        await using (var db = factory.CreateDirectDbContext(s.Ids.CompanyId))
        {
            var shift = await db.Shifts.SingleAsync(x => x.POSTerminalId == s.Ids.PosTerminalId && x.Status == ShiftStatus.Open);
            shiftId = shift.Id;
            rowVersion = Convert.ToBase64String(shift.RowVersion);
        }

        var close = await cashier.PostAsJsonAsync($"/api/v1/pos/shifts/{shiftId}/close", new
        {
            rowVersion,
            closedByUserId = 1L, // ignored: the closer is whoever is signed in
            closingCounts = new[] { new { denominationValue = 100m, count = 2 } }
        });
        close.EnsureSuccessStatusCode();

        await using var check = factory.CreateDirectDbContext(s.Ids.CompanyId);
        Assert.Equal(s.CashierId, (await check.Shifts.SingleAsync(x => x.Id == shiftId)).ClosedByUserId);
    }
}
