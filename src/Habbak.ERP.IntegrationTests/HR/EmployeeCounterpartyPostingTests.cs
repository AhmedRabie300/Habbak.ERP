using Habbak.ERP.Application.Accounting.Vouchers.Commands.CreateVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.PostVoucher;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests.HR;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.3 — the one new branch in
/// CounterpartyAccountResolver.cs: a Voucher against CounterpartyType.Employee resolves through the
/// company-wide CompanyAccountMapping role EmployeeReceivable (not a per-employee account, unlike
/// Customer/Supplier), and fails clearly (not the generic ACC-COUNTERPARTY-RESOLUTION-PENDING) when
/// that role isn't mapped yet. No change to PostVoucherCommand itself — same pattern as
/// VoucherAndJournalEntryCommandTests.cs, but with the real CounterpartyAccountResolver instead of
/// NotImplementedCounterpartyAccountResolver, since this is exactly the branch under test.
/// </summary>
public sealed class EmployeeCounterpartyPostingTests(PostingServiceFixture fixture) : IClassFixture<PostingServiceFixture>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private async Task<(long TreasuryAccountId, long EmployeeReceivableAccountId)> SeedAccountsAsync(long companyId)
    {
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));

        var treasury = new Account
        {
            CompanyId = companyId, Code = "1-1-01", NameAr = "الخزينة", NameEn = "Cash",
            AccountType = AccountType.Asset, Nature = AccountNature.Debit, IsPostable = true, IsActive = true
        };
        var employeeReceivable = new Account
        {
            CompanyId = companyId, Code = "1-2-01", NameAr = "ذمم الموظفين", NameEn = "Employee Receivable",
            AccountType = AccountType.Asset, Nature = AccountNature.Debit, IsPostable = true, IsActive = true
        };
        db.Accounts.AddRange(treasury, employeeReceivable);
        await db.SaveChangesAsync();

        return (treasury.Id, employeeReceivable.Id);
    }

    private async Task<long> SeedEmployeeAsync(long companyId)
    {
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));

        var orgUnit = new OrgUnit { CompanyId = companyId, Code = $"OU{Guid.NewGuid():N}"[..8], NameAr = "إدارة", NameEn = "Dept" };
        var jobGrade = new JobGrade { CompanyId = companyId, Code = $"JG{Guid.NewGuid():N}"[..8], NameAr = "أ", NameEn = "A", Level = 1 };
        db.OrgUnits.Add(orgUnit);
        db.JobGrades.Add(jobGrade);
        await db.SaveChangesAsync();

        var jobPosition = new JobPosition { CompanyId = companyId, Code = $"JP{Guid.NewGuid():N}"[..8], NameAr = "محاسب", NameEn = "Accountant", OrgUnitId = orgUnit.Id };
        db.JobPositions.Add(jobPosition);
        await db.SaveChangesAsync();

        var employee = new Employee
        {
            CompanyId = companyId, BranchId = 1, Code = $"E{Guid.NewGuid():N}"[..8], NameAr = "موظف", NameEn = "Employee",
            OrgUnitId = orgUnit.Id, JobPositionId = jobPosition.Id, JobGradeId = jobGrade.Id,
            HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee.Id;
    }

    private async Task<long> CreateEmployeeVoucherAsync(long companyId, long treasuryAccountId, long employeeId, decimal amount)
    {
        await using var createDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var createHandler = new CreateVoucherCommandHandler(
            createDb, new TestCurrentCompanyContext(companyId), new VoucherNumberGenerator(new CodeGenerator(createDb, new TestCurrentCompanyContext(companyId))));

        return await createHandler.Handle(new CreateVoucherCommand
        {
            VoucherType = VoucherType.Payment,
            VoucherDate = new DateOnly(2026, 9, 1),
            BranchId = 1,
            TreasuryAccountId = treasuryAccountId,
            CounterpartyType = CounterpartyType.Employee,
            CounterpartyId = employeeId,
            Amount = amount,
            CurrencyCode = "EGP",
            ExchangeRate = 1m,
            BaseCurrencyAmount = amount
        }, CancellationToken.None);
    }

    [Fact]
    public async Task PostVoucher_EmployeeCounterpartyWithMappedAccount_PostsThroughTheRealResolver()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, employeeReceivableAccountId) = await SeedAccountsAsync(companyId);
        var employeeId = await SeedEmployeeAsync(companyId);

        await using (var mappingDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            mappingDb.CompanyAccountMappings.Add(new CompanyAccountMapping
            {
                CompanyId = companyId, Role = CompanyAccountRole.EmployeeReceivable, AccountId = employeeReceivableAccountId
            });
            await mappingDb.SaveChangesAsync();
        }

        var voucherId = await CreateEmployeeVoucherAsync(companyId, treasuryAccountId, employeeId, 750m);

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var postingService = new PostingService(db, new Habbak.ERP.Application.Approvals.Services.ApprovalWorkflowService(db, new Habbak.ERP.Application.Approvals.Services.ApprovalStepResolutionService(db), new Habbak.ERP.Application.Notifications.NotificationService(db, new TestCurrentCompanyContext(companyId))), new JournalEntryNumberGenerator(new CodeGenerator(db, new TestCurrentCompanyContext(companyId))));
        var postHandler = new PostVoucherCommandHandler(db, postingService, new CounterpartyAccountResolver(db));

        var result = await postHandler.Handle(new PostVoucherCommand(voucherId), CancellationToken.None);

        Assert.Equal(nameof(JournalEntryStatus.Posted), result.Status);

        await using var verifyDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var voucher = await verifyDb.Vouchers.Include(v => v.JournalEntry).ThenInclude(j => j!.Lines)
            .SingleAsync(v => v.Id == voucherId);

        Assert.Equal(VoucherStatus.Posted, voucher.Status);
        Assert.Equal(750m, voucher.JournalEntry!.TotalDebit);
        Assert.Equal(750m, voucher.JournalEntry.TotalCredit);

        // Payment: debit the offset (employee receivable) account, credit the treasury account.
        var employeeLine = voucher.JournalEntry.Lines.Single(l => l.AccountId == employeeReceivableAccountId);
        var treasuryLine = voucher.JournalEntry.Lines.Single(l => l.AccountId == treasuryAccountId);
        Assert.Equal(750m, employeeLine.DebitAmount);
        Assert.Equal(750m, treasuryLine.CreditAmount);
    }

    [Fact]
    public async Task PostVoucher_EmployeeCounterpartyWithoutMappedAccount_ThrowsAClearBusinessRule()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, _) = await SeedAccountsAsync(companyId);
        var employeeId = await SeedEmployeeAsync(companyId);

        // No CompanyAccountMapping for EmployeeReceivable is seeded — the finance manager never linked it.
        var voucherId = await CreateEmployeeVoucherAsync(companyId, treasuryAccountId, employeeId, 500m);

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var postingService = new PostingService(db, new Habbak.ERP.Application.Approvals.Services.ApprovalWorkflowService(db, new Habbak.ERP.Application.Approvals.Services.ApprovalStepResolutionService(db), new Habbak.ERP.Application.Notifications.NotificationService(db, new TestCurrentCompanyContext(companyId))), new JournalEntryNumberGenerator(new CodeGenerator(db, new TestCurrentCompanyContext(companyId))));
        var postHandler = new PostVoucherCommandHandler(db, postingService, new CounterpartyAccountResolver(db));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => postHandler.Handle(new PostVoucherCommand(voucherId), CancellationToken.None));
        Assert.Equal("ACC-EMPLOYEE-ACCOUNT-NOT-MAPPED", ex.Code);
    }
}
