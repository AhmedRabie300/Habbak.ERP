using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.EmployeeCertifications.Commands.CreateEmployeeCertification;
using Habbak.ERP.Application.HR.EmployeeCertifications.Commands.SetEmployeeCertificationAttachment;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.CreateEmploymentContract;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.DeleteEmploymentContract;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.RenewEmploymentContract;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.SetEmploymentContractAttachment;
using Habbak.ERP.Application.HR.EmploymentContracts.Commands.UpdateEmploymentContract;
using Habbak.ERP.Application.HR.EmploymentContracts.Dtos;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests.HR;

/// <summary>
/// Docs/Implementation/Phase-3C-Research.md — EmploymentContractLine (Replace-All) + Contract/
/// Certification AttachmentId. Handlers exercised directly, same style as
/// EmploymentFollowersBatch6Tests (Batch B6).
/// </summary>
public sealed class Phase3CContractLinesAndAttachmentsTests : IAsyncLifetime
{
    private readonly string _databaseName = $"HabbakErpTests_HrPhase3C_{Guid.NewGuid():N}";

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

    private static async Task<long> SeedEmployeeAsync(AppDbContext context, long companyId, string code)
    {
        var orgUnit = new OrgUnit { CompanyId = companyId, Code = $"OU{Guid.NewGuid():N}"[..8], NameAr = "إدارة", NameEn = "Dept" };
        var jobGrade = new JobGrade { CompanyId = companyId, Code = $"JG{Guid.NewGuid():N}"[..8], NameAr = "أ", NameEn = "A", Level = 1 };
        context.OrgUnits.Add(orgUnit);
        context.JobGrades.Add(jobGrade);
        await context.SaveChangesAsync();

        var jobPosition = new JobPosition { CompanyId = companyId, Code = $"JP{Guid.NewGuid():N}"[..8], NameAr = "كاشير", NameEn = "Cashier", OrgUnitId = orgUnit.Id };
        context.JobPositions.Add(jobPosition);
        await context.SaveChangesAsync();

        var employee = new Employee
        {
            CompanyId = companyId, BranchId = 1, Code = code, NameAr = "موظف", NameEn = "Employee",
            OrgUnitId = orgUnit.Id, JobPositionId = jobPosition.Id, JobGradeId = jobGrade.Id,
            HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime
        };
        context.Employees.Add(employee);
        await context.SaveChangesAsync();
        return employee.Id;
    }

    private static ContractLineInput HousingAllowance(int order = 0) =>
        new("بدل سكن", "Housing Allowance", 500m, ContractLineType.Earning, IsTaxable: false, IsInsurable: false, Order: order);

    private static ContractLineInput LatePenalty(int order = 1) =>
        new("خصم تأخير", "Late Deduction", 100m, ContractLineType.Deduction, IsTaxable: false, IsInsurable: false, Order: order);

    // ------------------------------------------------------------------ Create / Update Replace-All

    [Fact]
    public async Task CreateEmploymentContract_persists_lines_ordered_by_Order()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        var contractId = await new CreateEmploymentContractCommandHandler(context, company).Handle(new CreateEmploymentContractCommand(
            employeeId, ContractType.Indefinite, new DateOnly(2026, 1, 1), null, null, 5000m, 4500m, 8,
            [LatePenalty(order: 1), HousingAllowance(order: 0)]), CancellationToken.None);

        var lines = await context.EmploymentContractLines
            .Where(l => l.EmploymentContractId == contractId)
            .OrderBy(l => l.Order)
            .ToListAsync();

        Assert.Equal(2, lines.Count);
        Assert.Equal("بدل سكن", lines[0].NameAr);
        Assert.Equal(ContractLineType.Deduction, lines[1].Type);
    }

    [Fact]
    public async Task UpdateEmploymentContract_replaces_all_lines_when_Lines_is_provided()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        var contractId = await new CreateEmploymentContractCommandHandler(context, company).Handle(new CreateEmploymentContractCommand(
            employeeId, ContractType.Indefinite, new DateOnly(2026, 1, 1), null, null, 5000m, 4500m, 8,
            [HousingAllowance()]), CancellationToken.None);

        await new UpdateEmploymentContractCommandHandler(context).Handle(new UpdateEmploymentContractCommand(
            contractId, ContractType.Indefinite, new DateOnly(2026, 1, 1), null, null, 5000m, 4500m, 8,
            [LatePenalty()]), CancellationToken.None);

        var lines = await context.EmploymentContractLines.Where(l => l.EmploymentContractId == contractId).ToListAsync();
        Assert.Single(lines);
        Assert.Equal("خصم تأخير", lines[0].NameAr);
    }

    [Fact]
    public async Task UpdateEmploymentContract_leaves_lines_untouched_when_Lines_is_null()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        var contractId = await new CreateEmploymentContractCommandHandler(context, company).Handle(new CreateEmploymentContractCommand(
            employeeId, ContractType.Indefinite, new DateOnly(2026, 1, 1), null, null, 5000m, 4500m, 8,
            [HousingAllowance()]), CancellationToken.None);

        await new UpdateEmploymentContractCommandHandler(context).Handle(new UpdateEmploymentContractCommand(
            contractId, ContractType.Indefinite, new DateOnly(2026, 1, 1), null, null, 6000m, 5500m, 8), CancellationToken.None);

        Assert.Single(await context.EmploymentContractLines.Where(l => l.EmploymentContractId == contractId).ToListAsync());
    }

    [Fact]
    public async Task RenewEmploymentContract_copies_the_Lines_it_is_given_into_the_new_contract()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        var oldContractId = await new CreateEmploymentContractCommandHandler(context, company).Handle(new CreateEmploymentContractCommand(
            employeeId, ContractType.FixedTerm, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), null, 5000m, 4500m, 8,
            [HousingAllowance()]), CancellationToken.None);

        // Docs/Implementation/Phase-3C-Research.md §5.1 — the frontend copies the previous contract's
        // Lines forward and lets the user edit them before saving; the handler itself just takes
        // whatever Lines it is given, same as Create.
        var newContractId = await new RenewEmploymentContractCommandHandler(context, company).Handle(new RenewEmploymentContractCommand(
            oldContractId, ContractType.Indefinite, new DateOnly(2027, 1, 1), null, null, 5500m, 5000m, 8,
            [HousingAllowance(), LatePenalty()]), CancellationToken.None);

        Assert.Single(await context.EmploymentContractLines.Where(l => l.EmploymentContractId == oldContractId).ToListAsync());
        Assert.Equal(2, await context.EmploymentContractLines.Where(l => l.EmploymentContractId == newContractId).CountAsync());
    }

    // ------------------------------------------------------------------ Cascade delete

    [Fact]
    public async Task Deleting_a_contract_cascades_to_its_lines()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        var contractId = await new CreateEmploymentContractCommandHandler(context, company).Handle(new CreateEmploymentContractCommand(
            employeeId, ContractType.Indefinite, new DateOnly(2026, 1, 1), null, null, 5000m, 4500m, 8,
            [HousingAllowance(), LatePenalty()]), CancellationToken.None);

        await new DeleteEmploymentContractCommandHandler(context).Handle(new DeleteEmploymentContractCommand(contractId), CancellationToken.None);

        Assert.Equal(0, await context.EmploymentContractLines.CountAsync(l => l.EmploymentContractId == contractId));
    }

    // ------------------------------------------------------------------ Attachments

    [Fact]
    public async Task SetEmploymentContractAttachment_sets_clears_and_validates_the_attachment_exists()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        var contractId = await new CreateEmploymentContractCommandHandler(context, company).Handle(new CreateEmploymentContractCommand(
            employeeId, ContractType.Indefinite, new DateOnly(2026, 1, 1), null, null, 5000m, 4500m, 8), CancellationToken.None);

        var notFound = await Assert.ThrowsAsync<NotFoundException>(() => new SetEmploymentContractAttachmentCommandHandler(context)
            .Handle(new SetEmploymentContractAttachmentCommand(contractId, 999_999), CancellationToken.None));
        Assert.NotNull(notFound);

        var attachment = new Attachment
        {
            CompanyId = 1, EntityType = AttachmentEntityTypes.EmploymentContract, EntityId = contractId,
            FileName = "contract.pdf", ContentType = "application/pdf", FileSizeBytes = 3, Content = [1, 2, 3]
        };
        context.Attachments.Add(attachment);
        await context.SaveChangesAsync();

        await new SetEmploymentContractAttachmentCommandHandler(context).Handle(
            new SetEmploymentContractAttachmentCommand(contractId, attachment.Id), CancellationToken.None);
        Assert.Equal(attachment.Id, (await context.EmploymentContracts.SingleAsync(c => c.Id == contractId)).AttachmentId);

        await new SetEmploymentContractAttachmentCommandHandler(context).Handle(
            new SetEmploymentContractAttachmentCommand(contractId, null), CancellationToken.None);
        Assert.Null((await context.EmploymentContracts.SingleAsync(c => c.Id == contractId)).AttachmentId);
    }

    [Fact]
    public async Task SetEmployeeCertificationAttachment_sets_and_validates_the_attachment_exists()
    {
        var company = new TestCurrentCompanyContext(companyId: 1);
        await using var context = CreateContext(company);
        var employeeId = await SeedEmployeeAsync(context, 1, "E1");

        var certificationId = await new CreateEmployeeCertificationCommandHandler(context, company).Handle(new CreateEmployeeCertificationCommand(
            employeeId, "باريستا", "Barista", "SCA", new DateOnly(2026, 1, 1), null, null), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => new SetEmployeeCertificationAttachmentCommandHandler(context)
            .Handle(new SetEmployeeCertificationAttachmentCommand(certificationId, 999_999), CancellationToken.None));

        var attachment = new Attachment
        {
            CompanyId = 1, EntityType = AttachmentEntityTypes.EmployeeCertification, EntityId = certificationId,
            FileName = "cert.pdf", ContentType = "application/pdf", FileSizeBytes = 3, Content = [1, 2, 3]
        };
        context.Attachments.Add(attachment);
        await context.SaveChangesAsync();

        await new SetEmployeeCertificationAttachmentCommandHandler(context).Handle(
            new SetEmployeeCertificationAttachmentCommand(certificationId, attachment.Id), CancellationToken.None);
        Assert.Equal(attachment.Id, (await context.EmployeeCertifications.SingleAsync(c => c.Id == certificationId)).AttachmentId);
    }
}
