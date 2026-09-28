using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Accounts.Commands.DeleteAccount;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens
/// (same soft-delete pattern as DeletePurchaseExpenseCommand). No child-account or posted-journal-
/// line check: soft delete only flags IsDeleted, it never breaks existing FKs on historical
/// JournalEntryLines/etc., same as every other soft delete in this codebase.</summary>
public sealed record DeleteAccountCommand(long Id) : IRequest;

public sealed class DeleteAccountCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteAccountCommand>
{
    public async Task Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.Accounts.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Account), request.Id);

        db.Accounts.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
