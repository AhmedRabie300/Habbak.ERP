using Habbak.ERP.Domain.HR;

namespace Habbak.ERP.Application.HR.EmploymentContracts.Dtos;

public sealed class EmploymentContractDto
{
    public required long Id { get; init; }
    public required long EmployeeId { get; init; }
    public long? BranchId { get; init; }
    public required ContractType ContractType { get; init; }
    public required DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public DateOnly? ProbationEndDate { get; init; }
    public required decimal BasicSalary { get; init; }
    public required decimal InsurableWage { get; init; }
    public required int WorkingHoursPerDay { get; init; }
    public required EmploymentContractStatus Status { get; init; }
    public long? PreviousContractId { get; init; }
    public long? ApprovalInstanceId { get; init; }
    public long? AttachmentId { get; init; }

    /// <summary>Phase 3C, §3.1 — populated by GetById; left empty by the paginated list query, same
    /// as BranchRequestListItemDto's LineCount-only choice for its own grid.</summary>
    public required IReadOnlyList<ContractLineDto> Lines { get; init; }
}

public sealed class ContractLineDto
{
    public required long Id { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required decimal Amount { get; init; }
    public required ContractLineType Type { get; init; }
    public required bool IsTaxable { get; init; }
    public required bool IsInsurable { get; init; }
    public required int Order { get; init; }
}

/// <summary>Input for Create/Update/Renew's Replace-All (Docs/Implementation/Phase-3C-Research.md §2) —
/// same pattern as BranchRequestLineInput.</summary>
public sealed record ContractLineInput(
    string NameAr, string NameEn, decimal Amount, ContractLineType Type, bool IsTaxable, bool IsInsurable, int Order);

public static class ContractLineInputExtensions
{
    public static EmploymentContractLine ToEntity(this ContractLineInput input) => new()
    {
        NameAr = input.NameAr, NameEn = input.NameEn, Amount = input.Amount,
        Type = input.Type, IsTaxable = input.IsTaxable, IsInsurable = input.IsInsurable, Order = input.Order
    };
}
