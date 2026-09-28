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
}
