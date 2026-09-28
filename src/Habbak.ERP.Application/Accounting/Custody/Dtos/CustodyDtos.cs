namespace Habbak.ERP.Application.Accounting.Custody.Dtos;

public sealed class CustodyRegisterListItemDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly IssueDate { get; init; }
    public required string Status { get; init; }
}

public sealed class CustodySettlementLineDto
{
    public required long AccountId { get; init; }
    public required decimal Amount { get; init; }
    public string? Description { get; init; }
}

public sealed class CustodySettlementDto
{
    public required long Id { get; init; }
    public required DateOnly SettlementDate { get; init; }
    public required decimal RemainingAmount { get; init; }
    public long? JournalEntryId { get; init; }
    public required IReadOnlyList<CustodySettlementLineDto> Lines { get; init; }
}

public sealed class CustodyRegisterDetailDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public long? BranchId { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly IssueDate { get; init; }
    public required string Status { get; init; }
    public long? JournalEntryId { get; init; }
    public required IReadOnlyList<CustodySettlementDto> Settlements { get; init; }
}
