using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmploymentContracts.Commands.DeleteEmploymentContract;

/// <summary>
/// My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens. No manual
/// children check: a contract that another contract's PreviousContractId points to (a renewal chain)
/// is protected by that FK's own DeleteBehavior.Restrict (Batch B3), same as every other FK-protected
/// soft delete in this codebase.
/// </summary>
public sealed record DeleteEmploymentContractCommand(long Id) : IRequest;

public sealed class DeleteEmploymentContractCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteEmploymentContractCommand>
{
    public async Task Handle(DeleteEmploymentContractCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.EmploymentContracts.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(EmploymentContract), request.Id);

        db.EmploymentContracts.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
