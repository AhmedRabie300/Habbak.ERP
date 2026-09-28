using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseExpenses.Commands.DeletePurchaseExpense;

/// <summary>Soft-deletes a breakdown row — no workflow to protect against, this is just an
/// itemization entry (rule: EF's AuditSaveChangesInterceptor turns Remove into IsDeleted=true for
/// every AuditableEntity, same as everywhere else in this codebase).</summary>
public sealed record DeletePurchaseExpenseCommand(long Id) : IRequest;

public sealed class DeletePurchaseExpenseCommandHandler(IApplicationDbContext db) : IRequestHandler<DeletePurchaseExpenseCommand>
{
    public async Task Handle(DeletePurchaseExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await db.PurchaseExpenses.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseExpense), request.Id);

        db.PurchaseExpenses.Remove(expense);

        await db.SaveChangesAsync(cancellationToken);
    }
}
