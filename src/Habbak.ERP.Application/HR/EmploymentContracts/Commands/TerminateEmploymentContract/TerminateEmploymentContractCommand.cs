using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;

namespace Habbak.ERP.Application.HR.EmploymentContracts.Commands.TerminateEmploymentContract;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B6 — closes this ONE contract (Status -&gt;
/// Terminated), independent of TerminateEmployeeCommand (Batch B5, which ends the employee's
/// employment entirely and auto-closes any still-Active contract as a side effect of that). This
/// command is for ending a contract on its own — a disciplinary termination, an early exit — without
/// necessarily terminating the employee's whole record.
/// </summary>
public sealed record TerminateEmploymentContractCommand(long Id) : IRequest;

public sealed class TerminateEmploymentContractCommandHandler(IApplicationDbContext db) : IRequestHandler<TerminateEmploymentContractCommand>
{
    public async Task Handle(TerminateEmploymentContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await db.EmploymentContracts.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(EmploymentContract), request.Id);

        if (contract.Status is EmploymentContractStatus.Terminated or EmploymentContractStatus.Expired)
        {
            throw new BusinessRuleException("HR-CONTRACT-ALREADY-CLOSED", "العقد ده مقفول بالفعل.");
        }

        contract.Status = EmploymentContractStatus.Terminated;
        contract.EndDate ??= DateOnly.FromDateTime(DateTime.UtcNow);

        await db.SaveChangesAsync(cancellationToken);
    }
}
