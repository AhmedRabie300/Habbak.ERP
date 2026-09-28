using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.OrgUnits.Commands.DeleteOrgUnit;

/// <summary>
/// My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens. Unlike most
/// other soft deletes in this codebase (e.g. DeleteItemGroupCommand, DeleteAccountCommand — which
/// deliberately do NOT check for children, since a soft delete never breaks existing FKs), this one
/// DOES reject deleting an OrgUnit that still has child OrgUnits — an explicit ask for this entity
/// specifically (Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4), not a codebase-wide rule.
/// </summary>
public sealed record DeleteOrgUnitCommand(long Id) : IRequest;

public sealed class DeleteOrgUnitCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteOrgUnitCommand>
{
    public async Task Handle(DeleteOrgUnitCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.OrgUnits.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(OrgUnit), request.Id);

        if (await db.OrgUnits.AnyAsync(u => u.ParentId == request.Id, cancellationToken))
        {
            throw new BusinessRuleException("HR-ORG-UNIT-HAS-CHILDREN", "لا يمكن حذف وحدة تنظيمية لها وحدات فرعية.");
        }

        db.OrgUnits.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
