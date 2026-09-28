using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.EmployeeDocumentTypes.Commands.CreateEmployeeDocumentType;
using Habbak.ERP.Application.HR.InsuranceOffices.Commands.CreateInsuranceOffice;
using Habbak.ERP.Application.HR.JobGrades.Commands.CreateJobGrade;
using Habbak.ERP.Application.HR.JobPositions.Commands.CreateJobPosition;
using Habbak.ERP.Application.HR.OrgUnits.Commands.CreateOrgUnit;
using Habbak.ERP.Application.HR.OrgUnits.Commands.DeleteOrgUnit;
using Habbak.ERP.Application.HR.OrgUnits.Commands.UpdateOrgUnit;
using Habbak.ERP.Application.Organization.Banks.Commands.CreateBank;
using Habbak.ERP.Application.Organization.Cities.Commands.CreateCity;
using Habbak.ERP.Application.Organization.Countries.Commands.CreateCountry;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Infrastructure.Persistence;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests.HR;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4 — Commands for the 8 "Hybrid: full screen"
/// lookups from Batch B1. Handlers are exercised directly (mirrors UpdateEmployeeCommandHandler in
/// EmployeeBatch2Tests, Batch B2) rather than through mediator.Send, so FluentValidation shape rules
/// are out of scope here — this only verifies each handler's own DB-touching business rules
/// (uniqueness, FK existence, OrgUnit's cycle detection and children-on-delete check). All 8 screens
/// are DefaultIsAutomatic = false (manual coding, Batch B1's ScreenCodeCatalog entries), so the real
/// CodeGenerator works here with no CodingRule seeding — a manual Code is always supplied.
/// </summary>
public sealed class HrLookupsBatch4Tests : IAsyncLifetime
{
    private readonly string _databaseName = $"HabbakErpTests_HrB4_{Guid.NewGuid():N}";

    private string _connectionString = null!;

    private AppDbContext CreateContext(ICurrentCompanyContext? companyContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_connectionString).Options;
        return new AppDbContext(options, companyContext);
    }

    public async Task InitializeAsync()
    {
        _connectionString = await TestSqlServer.GetConnectionStringAsync(_databaseName);
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    // ------------------------------------------------------------------ Country / City / Bank

    [Fact]
    public async Task CreateCountry_succeeds_and_CreateCity_requires_an_existing_country()
    {
        await using var context = CreateContext(new TestCurrentCompanyContext(companyId: 1));
        var codeGenerator = new CodeGenerator(context, new TestCurrentCompanyContext(companyId: 1));

        var countryId = await new CreateCountryCommandHandler(context, codeGenerator)
            .Handle(new CreateCountryCommand("EG", "مصر", "Egypt", "EG"), CancellationToken.None);
        Assert.True(countryId > 0);

        await Assert.ThrowsAsync<NotFoundException>(() => new CreateCityCommandHandler(context, codeGenerator)
            .Handle(new CreateCityCommand("CAI", "القاهرة", "Cairo", CountryId: 999_999), CancellationToken.None));

        var cityId = await new CreateCityCommandHandler(context, codeGenerator)
            .Handle(new CreateCityCommand("CAI", "القاهرة", "Cairo", countryId), CancellationToken.None);
        Assert.True(cityId > 0);
    }

    [Fact]
    public async Task CreateCountry_rejects_a_duplicate_code_system_wide()
    {
        await using var context = CreateContext();
        var codeGenerator = new CodeGenerator(context, new TestCurrentCompanyContext(companyId: 1));
        var handler = new CreateCountryCommandHandler(context, codeGenerator);

        await handler.Handle(new CreateCountryCommand("EG", "مصر", "Egypt", null), CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(new CreateCountryCommand("EG", "مصر٢", "Egypt2", null), CancellationToken.None));
    }

    [Fact]
    public async Task CreateBank_accepts_a_null_CountryId_and_rejects_a_nonexistent_one()
    {
        await using var context = CreateContext();
        var codeGenerator = new CodeGenerator(context, new TestCurrentCompanyContext(companyId: 1));
        var handler = new CreateBankCommandHandler(context, codeGenerator);

        var bankId = await handler.Handle(new CreateBankCommand("NBE", "البنك الأهلي", "NBE", null, null, null), CancellationToken.None);
        Assert.True(bankId > 0);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CreateBankCommand("QNB", "بنك قطر", "QNB", null, null, 999_999), CancellationToken.None));
    }

    // ------------------------------------------------------------------ JobGrade / EmployeeDocumentType / InsuranceOffice

    [Fact]
    public async Task CreateJobGrade_rejects_a_duplicate_code_within_the_same_company_but_allows_it_in_another()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var handler = new CreateJobGradeCommandHandler(context, company, new CodeGenerator(context, company));

        await handler.Handle(new CreateJobGradeCommand("JG1", "أ", "A", 1, 5000, 8000), CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(new CreateJobGradeCommand("JG1", "ب", "B", 2, 6000, 9000), CancellationToken.None));

        var handlerCompany2 = new CreateJobGradeCommandHandler(context, new TestCurrentCompanyContext(companyId: 2), new CodeGenerator(context, new TestCurrentCompanyContext(companyId: 2)));
        var otherCompanyId = await handlerCompany2.Handle(new CreateJobGradeCommand("JG1", "أ", "A", 1, 5000, 8000), CancellationToken.None);
        Assert.True(otherCompanyId > 0);
    }

    [Fact]
    public async Task CreateEmployeeDocumentType_and_CreateInsuranceOffice_succeed()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);

        var documentTypeId = await new CreateEmployeeDocumentTypeCommandHandler(context, company, new CodeGenerator(context, company))
            .Handle(new CreateEmployeeDocumentTypeCommand("ID", "بطاقة", "ID Card", true, true, 30), CancellationToken.None);
        Assert.True(documentTypeId > 0);

        var officeId = await new CreateInsuranceOfficeCommandHandler(context, new CodeGenerator(context, company))
            .Handle(new CreateInsuranceOfficeCommand("OFF1", "مكتب التأمينات", "Insurance Office", "GOV-1", "القاهرة"), CancellationToken.None);
        Assert.True(officeId > 0);
    }

    // ------------------------------------------------------------------ JobPosition

    [Fact]
    public async Task CreateJobPosition_rejects_a_nonexistent_OrgUnit_or_JobGrade()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var handler = new CreateJobPositionCommandHandler(context, company, new CodeGenerator(context, company));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CreateJobPositionCommand("CASH", "كاشير", "Cashier", OrgUnitId: 999_999, DefaultJobGradeId: null), CancellationToken.None));

        var orgUnitId = await new CreateOrgUnitCommandHandler(context, company, new CodeGenerator(context, company))
            .Handle(new CreateOrgUnitCommand("OU1", "إدارة", "Dept", null, null, null, null), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new CreateJobPositionCommand("CASH", "كاشير", "Cashier", orgUnitId, DefaultJobGradeId: 999_999), CancellationToken.None));

        var positionId = await handler.Handle(new CreateJobPositionCommand("CASH", "كاشير", "Cashier", orgUnitId, null), CancellationToken.None);
        Assert.True(positionId > 0);
    }

    // ------------------------------------------------------------------ OrgUnit — cycle detection + children-on-delete

    [Fact]
    public async Task UpdateOrgUnit_rejects_a_parent_reassignment_that_would_create_a_cycle()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var codeGenerator = new CodeGenerator(context, company);
        var createHandler = new CreateOrgUnitCommandHandler(context, company, codeGenerator);

        var rootId = await createHandler.Handle(new CreateOrgUnitCommand("ROOT", "الجذر", "Root", null, null, null, null), CancellationToken.None);
        var childId = await createHandler.Handle(new CreateOrgUnitCommand("CHILD", "فرعي", "Child", rootId, null, null, null), CancellationToken.None);

        var updateHandler = new UpdateOrgUnitCommandHandler(context, company);

        // Root -> Child already exists; reassigning Root's own ParentId to Child would close the loop.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => updateHandler.Handle(
            new UpdateOrgUnitCommand(rootId, "الجذر", "Root", ParentId: childId, null, null, null, true), CancellationToken.None));
        Assert.Equal("HR-ORG-UNIT-PARENT-CYCLE", ex.Code);

        // Self-parenting is rejected the same way, even before the cycle walk.
        var selfEx = await Assert.ThrowsAsync<BusinessRuleException>(() => updateHandler.Handle(
            new UpdateOrgUnitCommand(rootId, "الجذر", "Root", ParentId: rootId, null, null, null, true), CancellationToken.None));
        Assert.Equal("HR-ORG-UNIT-SELF-PARENT", selfEx.Code);

        // A valid reassignment (no cycle) still works.
        await updateHandler.Handle(new UpdateOrgUnitCommand(childId, "فرعي", "Child", ParentId: null, null, null, null, true), CancellationToken.None);
    }

    [Fact]
    public async Task DeleteOrgUnit_is_rejected_while_it_still_has_a_child_org_unit()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var codeGenerator = new CodeGenerator(context, company);
        var createHandler = new CreateOrgUnitCommandHandler(context, company, codeGenerator);

        var parentId = await createHandler.Handle(new CreateOrgUnitCommand("PAR", "أب", "Parent", null, null, null, null), CancellationToken.None);
        var childId = await createHandler.Handle(new CreateOrgUnitCommand("CH", "ابن", "Child", parentId, null, null, null), CancellationToken.None);

        var deleteHandler = new DeleteOrgUnitCommandHandler(context);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => deleteHandler.Handle(new DeleteOrgUnitCommand(parentId), CancellationToken.None));
        Assert.Equal("HR-ORG-UNIT-HAS-CHILDREN", ex.Code);

        // Deleting the leaf (no children) succeeds, and then the now-childless parent can be deleted too.
        await deleteHandler.Handle(new DeleteOrgUnitCommand(childId), CancellationToken.None);
        await deleteHandler.Handle(new DeleteOrgUnitCommand(parentId), CancellationToken.None);

        Assert.Equal(0, await context.OrgUnits.CountAsync());
    }
}
