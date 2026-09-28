using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habbak.ERP.API.Auth;
using Habbak.ERP.Domain.Approvals;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// Docs/Modules/00-Project-Overview.md §12 (Phase 2) — the HTTP round trip for
/// ApprovalWorkflowsController/ApprovalInstancesController/ScreensController, and the
/// SETTINGS_APPROVAL_WORKFLOWS/ManualReassign button-permission gate specifically (the mandatory
/// Manual Fallback, §12.4 rule 10). Mandatory-scenario business logic itself (Self-Approval Guard,
/// AnyOne/All, versioning, DirectManager resolution...) is covered against the real handlers in
/// Habbak.ERP.IntegrationTests/Approvals/ApprovalWorkflowEngineTests.cs; this file only proves the
/// screens are wired and gated correctly end-to-end.
/// </summary>
public class ApprovalWorkflowApiTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static readonly BcryptPasswordHasher Hasher = new();
    private const string Password = "Strong@123";

    private sealed record Seeded(long CompanyId, Dictionary<string, long> Roles);

    private async Task<Seeded> SeedCompanyAsync()
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

        if (!await db.Screens.AnyAsync())
        {
            db.Screens.AddRange(Habbak.ERP.Infrastructure.Persistence.Seeding.ScreenSeedData.Build());
            await db.SaveChangesAsync();
        }

        var company = new Company { Code = $"H{Guid.NewGuid():N}"[..12], NameAr = "شركة الهباك", NameEn = "Habbak Co", BaseCurrencyId = currency.Id, IsActive = true };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        await Habbak.ERP.Application.Settings.CompanySecurityDefaults.AddAsync(db, company.Id, DateTime.UtcNow, 0, default);
        await db.SaveChangesAsync();

        var roles = await db.Roles.IgnoreQueryFilters().Where(r => r.CompanyId == company.Id).ToDictionaryAsync(r => r.Code, r => r.Id);
        return new Seeded(company.Id, roles);
    }

    private async Task<long> SeedUserAsync(Seeded company, string role)
    {
        await using var db = factory.CreateDirectDbContext(company.CompanyId);
        var username = $"u{Guid.NewGuid():N}"[..14];
        var user = new User
        {
            Username = username, Email = $"{username}@test.local", FullName = "مستخدم", PasswordHash = Hasher.Hash(Password),
            Status = UserStatus.Active, PasswordChangedAtUtc = DateTime.UtcNow
        };
        user.UserRoles.Add(new UserRole { RoleId = company.Roles[role], AssignedAtUtc = DateTime.UtcNow, AssignedByUserId = 0 });
        user.UserScopes.Add(new UserScope { CompanyId = company.CompanyId, RoleInScope = role, IsDefault = true, IsActive = true, GrantedAtUtc = DateTime.UtcNow });
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private HttpClient Client(long companyId, long userId = 1, string? roles = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        if (roles is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, roles);
        return client;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()!;

    [Fact]
    public async Task Create_assign_and_approve_a_workflow_end_to_end()
    {
        var company = await SeedCompanyAsync();
        var admin = Client(company.CompanyId);

        var screens = await admin.GetFromJsonAsync<JsonElement[]>("/api/v1/screens");
        Assert.NotNull(screens);
        var screenId = screens!.First(s => s.GetProperty("code").GetString() == "HR_EMPLOYEES").GetProperty("id").GetInt64();

        var create = await admin.PostAsJsonAsync("/api/v1/approvals/workflows", new
        {
            code = $"WF{Guid.NewGuid():N}"[..10],
            nameAr = "سلسلة اختبار",
            nameEn = "Test Workflow",
            steps = new object[]
            {
                new { stepOrder = 1, mode = "AnyOne", approvers = new object[] { new { approverType = "DirectManager", approverReferenceId = (long?)null } } }
            }
        });
        create.EnsureSuccessStatusCode();
        var workflowId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        var assign = await admin.PostAsJsonAsync("/api/v1/approvals/workflows/assignments", new { screenId, approvalWorkflowId = workflowId, minAmount = (decimal?)null });
        assign.EnsureSuccessStatusCode();

        var assignments = await admin.GetFromJsonAsync<JsonElement[]>("/api/v1/approvals/workflows/assignments");
        Assert.Contains(assignments!, a => a.GetProperty("screenId").GetInt64() == screenId);

        var getWorkflow = await admin.GetAsync($"/api/v1/approvals/workflows/{workflowId}");
        getWorkflow.EnsureSuccessStatusCode();

        var deactivate = await admin.PostAsync($"/api/v1/approvals/workflows/{workflowId}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.StatusCode);

        var afterDeactivate = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/approvals/workflows/{workflowId}");
        Assert.False(afterDeactivate.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Manual_reassign_needs_the_explicit_button_permission_not_just_screen_access()
    {
        var company = await SeedCompanyAsync();
        var accountantUserId = await SeedUserAsync(company, SystemRoles.Accountant);

        // The pending instance itself, seeded directly (the trigger/eligibility path is covered by
        // ApprovalWorkflowEngineTests) — only the endpoint's permission gate is under test here.
        long instanceId;
        await using (var db = factory.CreateDirectDbContext(company.CompanyId))
        {
            var workflow = new ApprovalWorkflow { CompanyId = company.CompanyId, Code = $"WF{Guid.NewGuid():N}"[..10], NameAr = "س", NameEn = "W", IsActive = true };
            // ReassignInstanceCommandHandler (Phase 2.5) now resolves the current step's eligible
            // approvers before reassigning (to notify them) — needs a real step to find, not just
            // the workflow shell this test used to seed.
            workflow.Steps.Add(new ApprovalWorkflowStep
            {
                StepOrder = 1, Mode = ApprovalStepMode.AnyOne,
                Approvers = { new ApprovalStepApprover { ApproverType = ApprovalApproverType.DirectManager, ApproverReferenceId = null } }
            });
            db.ApprovalWorkflows.Add(workflow);
            await db.SaveChangesAsync();

            var instance = new ApprovalInstance
            {
                CompanyId = company.CompanyId, EntityType = "Test", EntityId = 1, ApprovalWorkflowId = workflow.Id, WorkflowVersionNumber = 1,
                RequestedByUserId = 999, RequestedAtUtc = DateTime.UtcNow, Amount = 0, CurrentStepOrder = 1, Status = ApprovalInstanceStatus.Pending
            };
            db.ApprovalInstances.Add(instance);
            await db.SaveChangesAsync();
            instanceId = instance.Id;
        }

        // Grant the accountant plain View+Edit on the two screens the controller accepts, but no button row.
        var grantScreens = await Client(company.CompanyId).PutAsJsonAsync(
            $"/api/v1/settings/roles/{company.Roles[SystemRoles.Accountant]}/screens",
            new[]
            {
                new { screenCode = "APPROVAL_MY_PENDING", canView = true, canAdd = false, canEdit = true, canDelete = false, canPrint = false, canExport = false, canApprove = false },
                new { screenCode = "SETTINGS_APPROVAL_WORKFLOWS", canView = true, canAdd = false, canEdit = true, canDelete = false, canPrint = false, canExport = false, canApprove = false }
            });
        Assert.Equal(HttpStatusCode.NoContent, grantScreens.StatusCode);

        var accountant = Client(company.CompanyId, accountantUserId, SystemRoles.Accountant);
        var denied = await accountant.PostAsJsonAsync($"/api/v1/approvals/instances/{instanceId}/reassign", new { newApproverUserId = accountantUserId, reason = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        // Now grant the ManualReassign button permission explicitly, through the real grant endpoint
        // (not a raw DB insert) — UserAccessService caches a role's rights for a minute, keyed by a
        // version counter the grant command bumps; a direct DB write would leave the accountant's
        // already-cached "no button" result in place for the rest of that window.
        var grantButton = await Client(company.CompanyId).PutAsJsonAsync(
            $"/api/v1/settings/roles/{company.Roles[SystemRoles.Accountant]}/buttons?screenCode=SETTINGS_APPROVAL_WORKFLOWS",
            new[] { new { screenCode = "SETTINGS_APPROVAL_WORKFLOWS", buttonCode = "ManualReassign", isEnabled = true, requiresAuditLog = true } });
        Assert.Equal(HttpStatusCode.NoContent, grantButton.StatusCode);

        var allowed = await accountant.PostAsJsonAsync($"/api/v1/approvals/instances/{instanceId}/reassign", new { newApproverUserId = accountantUserId, reason = "تجربة" });
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
    }
}
