namespace Habbak.ERP.Application.Accounting.Reports.Dtos;

public sealed class TrialBalanceLineDto
{
    public required long AccountId { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required decimal Debit { get; init; }
    public required decimal Credit { get; init; }
}

public sealed class AccountStatementLineDto
{
    public required DateOnly Date { get; init; }
    public required string EntryNumber { get; init; }
    public required string Description { get; init; }
    public required decimal Debit { get; init; }
    public required decimal Credit { get; init; }
    public required decimal RunningBalance { get; init; }
}

public sealed class AccountStatementDto
{
    public required decimal OpeningBalance { get; init; }
    public required decimal ClosingBalance { get; init; }
    public required IReadOnlyList<AccountStatementLineDto> Lines { get; init; }
}

public sealed class IncomeStatementLineDto
{
    public required long AccountId { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required decimal Amount { get; init; }
}

public sealed class IncomeStatementDto
{
    public required IReadOnlyList<IncomeStatementLineDto> Revenue { get; init; }
    public required IReadOnlyList<IncomeStatementLineDto> Expenses { get; init; }
    public required decimal TotalRevenue { get; init; }
    public required decimal TotalExpenses { get; init; }
    public required decimal NetIncome { get; init; }
}

public sealed class BalanceSheetDto
{
    public required IReadOnlyList<IncomeStatementLineDto> Assets { get; init; }
    public required IReadOnlyList<IncomeStatementLineDto> Liabilities { get; init; }
    public required IReadOnlyList<IncomeStatementLineDto> Equity { get; init; }
    public required decimal TotalAssets { get; init; }
    public required decimal TotalLiabilities { get; init; }
    public required decimal TotalEquity { get; init; }
}

public sealed class CashFlowDto
{
    public required decimal OpeningBalance { get; init; }
    public required decimal TotalDebit { get; init; }
    public required decimal TotalCredit { get; init; }
    public required decimal ClosingBalance { get; init; }
}

public sealed class TreasuryPositionLineDto
{
    public required long AccountId { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required decimal Balance { get; init; }
}

public sealed class CostCenterExpenseLineDto
{
    public required long DimensionValueId { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required decimal Amount { get; init; }
}

public sealed class JournalBookLineDto
{
    public required long AccountId { get; init; }
    public required string AccountCode { get; init; }
    public required string AccountName { get; init; }
    public required decimal Debit { get; init; }
    public required decimal Credit { get; init; }
}

public sealed class JournalBookEntryDto
{
    public required long Id { get; init; }
    public required string EntryNumber { get; init; }
    public required DateOnly EntryDate { get; init; }
    public required string Description { get; init; }
    public required string Status { get; init; }
    public required decimal TotalDebit { get; init; }
    public required decimal TotalCredit { get; init; }
    public required IReadOnlyList<JournalBookLineDto> Lines { get; init; }
}

public sealed class TreasuryAccountStatementDto
{
    public required long AccountId { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required decimal OpeningBalance { get; init; }
    public required decimal ClosingBalance { get; init; }
    public required IReadOnlyList<AccountStatementLineDto> Lines { get; init; }
}

public sealed class CustodyReportSettlementLineDto
{
    public required long AccountId { get; init; }
    public required string AccountCode { get; init; }
    public required string AccountName { get; init; }
    public required decimal Amount { get; init; }
    public string? Description { get; init; }
}

public sealed class CustodyReportSettlementDto
{
    public required DateOnly SettlementDate { get; init; }
    public required decimal TotalAmount { get; init; }
    public required IReadOnlyList<CustodyReportSettlementLineDto> Lines { get; init; }
}

public sealed class CustodyReportLineDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public long? BranchId { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly IssueDate { get; init; }
    public required string Status { get; init; }
    public required decimal TotalSettled { get; init; }
    public required IReadOnlyList<CustodyReportSettlementDto> Settlements { get; init; }
}

public sealed class BankReconciliationReportLineDto
{
    public required long RunId { get; init; }
    public required DateOnly PeriodFrom { get; init; }
    public required DateOnly PeriodTo { get; init; }
    public required string RunStatus { get; init; }
    public string? SystemTransactionType { get; init; }
    public long? SystemTransactionId { get; init; }
    public long? BankStatementLineId { get; init; }
    public required decimal MatchedAmount { get; init; }
    public required bool IsAutoMatched { get; init; }
}
