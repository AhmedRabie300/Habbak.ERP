using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.Settings.Commands.UpdateHrSettings;
using Habbak.ERP.Application.HR.Settings.Queries.GetHrSettings;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests.HR;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.0 — HrSettings backend (one row
/// per company, upserted the same way as UpdatePurchaseCycleSettingsCommand).
/// </summary>
public sealed class HrSettingsTests : IAsyncLifetime
{
    private readonly string _databaseName = $"HabbakErpTests_HrSettings_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    private AppDbContext CreateContext(ICurrentCompanyContext? companyContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        return new AppDbContext(options, companyContext);
    }

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Get_returns_the_entity_defaults_when_no_row_has_been_configured_yet()
    {
        await using var context = CreateContext(new TestCurrentCompanyContext(companyId: 1));

        var dto = await new GetHrSettingsQueryHandler(context, new TestCurrentCompanyContext(companyId: 1))
            .Handle(new GetHrSettingsQuery(), CancellationToken.None);

        Assert.Equal(90, dto.DefaultProbationDays);
        Assert.Null(dto.DefaultBranchId);
        Assert.True(dto.RequireNationalIdForActivation);
    }

    [Fact]
    public async Task Update_inserts_the_single_row_on_first_call_and_updates_the_same_row_afterwards()
    {
        await using var context = CreateContext(new TestCurrentCompanyContext(companyId: 1));
        var companyContext = new TestCurrentCompanyContext(companyId: 1);

        await new UpdateHrSettingsCommandHandler(context, companyContext).Handle(
            new UpdateHrSettingsCommand { DefaultProbationDays = 60, DefaultBranchId = null, RequireNationalIdForActivation = false, LeaveDayCountingMode = LeaveDayCountingMode.Calendar },
            CancellationToken.None);

        var afterFirstUpdate = await new GetHrSettingsQueryHandler(context, companyContext)
            .Handle(new GetHrSettingsQuery(), CancellationToken.None);
        Assert.Equal(60, afterFirstUpdate.DefaultProbationDays);
        Assert.False(afterFirstUpdate.RequireNationalIdForActivation);
        Assert.Equal(1, await context.HrSettingsRows.CountAsync());

        await new UpdateHrSettingsCommandHandler(context, companyContext).Handle(
            new UpdateHrSettingsCommand { DefaultProbationDays = 45, DefaultBranchId = null, RequireNationalIdForActivation = true, LeaveDayCountingMode = LeaveDayCountingMode.Calendar },
            CancellationToken.None);

        var afterSecondUpdate = await new GetHrSettingsQueryHandler(context, companyContext)
            .Handle(new GetHrSettingsQuery(), CancellationToken.None);
        Assert.Equal(45, afterSecondUpdate.DefaultProbationDays);
        Assert.True(afterSecondUpdate.RequireNationalIdForActivation);
        Assert.Equal(1, await context.HrSettingsRows.CountAsync());
    }

    [Fact]
    public async Task Update_rejects_a_DefaultBranchId_that_does_not_belong_to_the_current_company()
    {
        long otherCompanyBranchId;
        await using (var otherCompanyContext = CreateContext(new TestCurrentCompanyContext(companyId: 2)))
        {
            otherCompanyContext.Branches.Add(new Branch { CompanyId = 2, Code = "B1", NameAr = "فرع", NameEn = "Branch", IsActive = true });
            await otherCompanyContext.SaveChangesAsync();
            otherCompanyBranchId = otherCompanyContext.Branches.Single().Id;
        }

        await using var context = CreateContext(new TestCurrentCompanyContext(companyId: 1));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateHrSettingsCommandHandler(context, new TestCurrentCompanyContext(companyId: 1)).Handle(
            new UpdateHrSettingsCommand { DefaultProbationDays = 90, DefaultBranchId = otherCompanyBranchId, RequireNationalIdForActivation = true, LeaveDayCountingMode = LeaveDayCountingMode.Calendar },
            CancellationToken.None));
    }

    [Fact]
    public async Task Update_accepts_a_DefaultBranchId_that_belongs_to_the_current_company()
    {
        await using var context = CreateContext(new TestCurrentCompanyContext(companyId: 1));

        context.Branches.Add(new Branch { CompanyId = 1, Code = "B1", NameAr = "فرع", NameEn = "Branch", IsActive = true });
        await context.SaveChangesAsync();
        var branchId = context.Branches.Single().Id;
        var companyContext = new TestCurrentCompanyContext(companyId: 1);

        await new UpdateHrSettingsCommandHandler(context, companyContext).Handle(
            new UpdateHrSettingsCommand { DefaultProbationDays = 90, DefaultBranchId = branchId, RequireNationalIdForActivation = true, LeaveDayCountingMode = LeaveDayCountingMode.Calendar },
            CancellationToken.None);

        var dto = await new GetHrSettingsQueryHandler(context, companyContext).Handle(new GetHrSettingsQuery(), CancellationToken.None);
        Assert.Equal(branchId, dto.DefaultBranchId);
    }

    [Fact]
    public async Task Two_companies_each_keep_their_own_independent_settings_row()
    {
        await using var company1Context = CreateContext(new TestCurrentCompanyContext(companyId: 1));
        await using var company2Context = CreateContext(new TestCurrentCompanyContext(companyId: 2));

        await new UpdateHrSettingsCommandHandler(company1Context, new TestCurrentCompanyContext(companyId: 1)).Handle(
            new UpdateHrSettingsCommand { DefaultProbationDays = 30, DefaultBranchId = null, RequireNationalIdForActivation = false, LeaveDayCountingMode = LeaveDayCountingMode.Calendar },
            CancellationToken.None);
        await new UpdateHrSettingsCommandHandler(company2Context, new TestCurrentCompanyContext(companyId: 2)).Handle(
            new UpdateHrSettingsCommand { DefaultProbationDays = 120, DefaultBranchId = null, RequireNationalIdForActivation = true, LeaveDayCountingMode = LeaveDayCountingMode.Calendar },
            CancellationToken.None);

        var company1 = await new GetHrSettingsQueryHandler(company1Context, new TestCurrentCompanyContext(companyId: 1))
            .Handle(new GetHrSettingsQuery(), CancellationToken.None);
        var company2 = await new GetHrSettingsQueryHandler(company2Context, new TestCurrentCompanyContext(companyId: 2))
            .Handle(new GetHrSettingsQuery(), CancellationToken.None);

        Assert.Equal(30, company1.DefaultProbationDays);
        Assert.Equal(120, company2.DefaultProbationDays);

        await using var unscopedContext = CreateContext();
        Assert.Equal(2, await unscopedContext.HrSettingsRows.IgnoreQueryFilters().CountAsync());
    }
}
