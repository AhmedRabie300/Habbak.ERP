using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.EmployeeCertifications.Commands.CreateEmployeeCertification;
using Habbak.ERP.Application.HR.EmployeeCertifications.Commands.DeleteEmployeeCertification;
using Habbak.ERP.Application.HR.EmployeeCertifications.Commands.UpdateEmployeeCertification;
using Habbak.ERP.Application.HR.EmployeeDocuments.Commands.CreateEmployeeDocument;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.CreateEmploymentContract;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.DeleteEmploymentContract;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.RenewEmploymentContract;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.UpdateEmploymentContract;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests.HR;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B6 — the 3 Employee "followers"' Commands.
/// Handlers exercised directly, same style as Batch B4/B5 (HrLookupsBatch4Tests/EmployeeBatch5Tests).
/// </summary>
public sealed class EmploymentFollowersBatch6Tests : IAsyncLifetime
{
    private readonly string _databaseName = $"HabbakErpTests_HrB6_{Guid.NewGuid():N}";

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

    private static async Task<(long OrgUnitId, long JobPositionId, long JobGradeId)> SeedLookupsAsync(AppDbContext context, long companyId)
    {
        var orgUnit = new OrgUnit { CompanyId = companyId, Code = $"OU{Guid.NewGuid():N}"[..8], NameAr = "إدارة", NameEn = "Dept" };
        var jobGrade = new JobGrade { CompanyId = companyId, Code = $"JG{Guid.NewGuid():N}"[..8], NameAr = "أ", NameEn = "A", Level = 1 };
        context.OrgUnits.Add(orgUnit);
        context.JobGrades.Add(jobGrade);
        await context.SaveChangesAsync();

        var jobPosition = new JobPosition { CompanyId = companyId, Code = $"JP{Guid.NewGuid():N}"[..8], NameAr = "كاشير", NameEn = "Cashier", OrgUnitId = orgUnit.Id };
        context.JobPositions.Add(jobPosition);
        await context.SaveChangesAsync();

        return (orgUnit.Id, jobPosition.Id, jobGrade.Id);
    }

    private static async Task<long> SeedEmployeeAsync(AppDbContext context, long companyId, string code)
    {
        var (orgUnitId, jobPositionId, jobGradeId) = await SeedLookupsAsync(context, companyId);
        var employee = new Employee
        {
            CompanyId = companyId, BranchId = 1, Code = code, NameAr = "موظف", NameEn = "Employee",
            OrgUnitId = orgUnitId, JobPositionId = jobPositionId, JobGradeId = jobGradeId,
            HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime
        };
        context.Employees.Add(employee);
        await context.SaveChangesAsync();
        return employee.Id;
    }

    // ------------------------------------------------------------------ EmploymentContract

    [Fact]
    public async Task CreateEmploymentContract_rejects_EndDate_before_StartDate_via_the_validator()
    {
        var validator = new CreateEmploymentContractCommandValidator();
        var result = await validator.ValidateAsync(new CreateEmploymentContractCommand(
            1, ContractType.FixedTerm, new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1), null, 5000m, 4500m, 8));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEmploymentContractCommand.EndDate));
    }

    [Fact]
    public async Task CreateEmploymentContract_rejects_a_second_active_contract_for_the_same_employee()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        var handler = new CreateEmploymentContractCommandHandler(context, company);
        await handler.Handle(new CreateEmploymentContractCommand(
            employeeId, ContractType.Indefinite, new DateOnly(2026, 1, 1), null, null, 5000m, 4500m, 8), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new CreateEmploymentContractCommand(
            employeeId, ContractType.Indefinite, new DateOnly(2026, 2, 1), null, null, 5000m, 4500m, 8), CancellationToken.None));
        Assert.Equal("HR-CONTRACT-ALREADY-ACTIVE", ex.Code);
    }

    [Fact]
    public async Task CreateEmploymentContract_becomes_Active_immediately_when_there_are_no_mandatory_document_types()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        var id = await new CreateEmploymentContractCommandHandler(context, company).Handle(new CreateEmploymentContractCommand(
            employeeId, ContractType.Indefinite, new DateOnly(2026, 1, 1), null, null, 5000m, 4500m, 8), CancellationToken.None);

        Assert.Equal(EmploymentContractStatus.Active, (await context.EmploymentContracts.SingleAsync(c => c.Id == id)).Status);
    }

    [Fact]
    public async Task UpdateEmploymentContract_and_DeleteEmploymentContract_work()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");
        var contractId = await new CreateEmploymentContractCommandHandler(context, company).Handle(new CreateEmploymentContractCommand(
            employeeId, ContractType.Indefinite, new DateOnly(2026, 1, 1), null, null, 5000m, 4500m, 8), CancellationToken.None);

        await new UpdateEmploymentContractCommandHandler(context).Handle(new UpdateEmploymentContractCommand(
            contractId, ContractType.Indefinite, new DateOnly(2026, 1, 1), null, null, 6000m, 5500m, 8), CancellationToken.None);
        Assert.Equal(6000m, (await context.EmploymentContracts.SingleAsync(c => c.Id == contractId)).BasicSalary);

        await new DeleteEmploymentContractCommandHandler(context).Handle(new DeleteEmploymentContractCommand(contractId), CancellationToken.None);
        Assert.Equal(0, await context.EmploymentContracts.CountAsync());
    }

    [Fact]
    public async Task RenewEmploymentContract_expires_the_previous_one_links_the_new_one_and_rejects_a_second_renewal()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");
        var oldContractId = await new CreateEmploymentContractCommandHandler(context, company).Handle(new CreateEmploymentContractCommand(
            employeeId, ContractType.FixedTerm, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), null, 5000m, 4500m, 8), CancellationToken.None);

        var renewHandler = new RenewEmploymentContractCommandHandler(context, company);
        var newContractId = await renewHandler.Handle(new RenewEmploymentContractCommand(
            oldContractId, ContractType.Indefinite, new DateOnly(2027, 1, 1), null, null, 5500m, 5000m, 8), CancellationToken.None);

        var oldContract = await context.EmploymentContracts.SingleAsync(c => c.Id == oldContractId);
        Assert.Equal(EmploymentContractStatus.Expired, oldContract.Status);

        var newContract = await context.EmploymentContracts.SingleAsync(c => c.Id == newContractId);
        Assert.Equal(oldContractId, newContract.PreviousContractId);
        Assert.Equal(EmploymentContractStatus.Active, newContract.Status);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => renewHandler.Handle(new RenewEmploymentContractCommand(
            oldContractId, ContractType.Indefinite, new DateOnly(2027, 6, 1), null, null, 6000m, 5500m, 8), CancellationToken.None));
        Assert.Equal("HR-CONTRACT-ALREADY-RENEWED", ex.Code);
    }

    // ------------------------------------------------------------------ EmployeeDocument

    [Fact]
    public async Task CreateEmployeeDocument_requires_ExpiryDate_when_the_document_type_requires_it_and_validates_the_attachment_exists()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        var mandatoryDocType = new EmployeeDocumentType { CompanyId = 1, Code = "ID", NameAr = "بطاقة", NameEn = "ID Card", RequiresExpiry = true };
        context.EmployeeDocumentTypes.Add(mandatoryDocType);
        var attachment = new Attachment { CompanyId = 1, EntityType = "EmployeeDocument", EntityId = 0, FileName = "id.pdf", ContentType = "application/pdf", FileSizeBytes = 1, Content = [1] };
        context.Attachments.Add(attachment);
        await context.SaveChangesAsync();

        var handler = new CreateEmployeeDocumentCommandHandler(context, company);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new CreateEmployeeDocumentCommand(
            employeeId, mandatoryDocType.Id, new DateOnly(2026, 1, 1), null, attachment.Id, null), CancellationToken.None));
        Assert.Equal("HR-DOCUMENT-EXPIRY-REQUIRED", ex.Code);

        var notFound = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new CreateEmployeeDocumentCommand(
            employeeId, mandatoryDocType.Id, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), 999_999, null), CancellationToken.None));

        var id = await handler.Handle(new CreateEmployeeDocumentCommand(
            employeeId, mandatoryDocType.Id, new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1), attachment.Id, "DOC-1"), CancellationToken.None);
        Assert.True(id > 0);
    }

    // ------------------------------------------------------------------ EmployeeCertification

    [Fact]
    public async Task EmployeeCertification_create_update_delete_all_work()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        var id = await new CreateEmployeeCertificationCommandHandler(context, company).Handle(new CreateEmployeeCertificationCommand(
            employeeId, "باريستا", "Barista", "SCA", new DateOnly(2026, 1, 1), null, null), CancellationToken.None);

        await new UpdateEmployeeCertificationCommandHandler(context).Handle(new UpdateEmployeeCertificationCommand(
            id, "باريستا متقدم", "Advanced Barista", "SCA", new DateOnly(2026, 1, 1), new DateOnly(2028, 1, 1), "CERT-1"), CancellationToken.None);
        var updated = await context.EmployeeCertifications.SingleAsync(c => c.Id == id);
        Assert.Equal("Advanced Barista", updated.NameEn);

        await new DeleteEmployeeCertificationCommandHandler(context).Handle(new DeleteEmployeeCertificationCommand(id), CancellationToken.None);
        Assert.Equal(0, await context.EmployeeCertifications.CountAsync());
    }

    // ------------------------------------------------------------------ Cascade: Employee delete blocked

    [Fact]
    public async Task Deleting_an_employee_is_blocked_while_a_contract_a_document_or_a_certification_still_references_it()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);

        async Task<long> NewEmployeeAsync()
        {
            await using var seed = CreateContext(company);
            return await SeedEmployeeAsync(seed, 1, $"E{Guid.NewGuid():N}"[..6]);
        }

        var employeeWithContract = await NewEmployeeAsync();
        await using (var seed = CreateContext(company))
        {
            await new CreateEmploymentContractCommandHandler(seed, company).Handle(new CreateEmploymentContractCommand(
                employeeWithContract, ContractType.Indefinite, new DateOnly(2026, 1, 1), null, null, 5000m, 4500m, 8), CancellationToken.None);
        }
        await using (var del = CreateContext(company))
        {
            var employee = await del.Employees.SingleAsync(e => e.Id == employeeWithContract);
            del.Employees.Remove(employee);
            await Assert.ThrowsAsync<DbUpdateException>(() => del.SaveChangesAsync());
        }

        var employeeWithCertification = await NewEmployeeAsync();
        await using (var seed = CreateContext(company))
        {
            await new CreateEmployeeCertificationCommandHandler(seed, company).Handle(new CreateEmployeeCertificationCommand(
                employeeWithCertification, "باريستا", "Barista", "SCA", new DateOnly(2026, 1, 1), null, null), CancellationToken.None);
        }
        await using (var del = CreateContext(company))
        {
            var employee = await del.Employees.SingleAsync(e => e.Id == employeeWithCertification);
            del.Employees.Remove(employee);
            await Assert.ThrowsAsync<DbUpdateException>(() => del.SaveChangesAsync());
        }
    }
}
