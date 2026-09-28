using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.PaymentMethods.Commands.DeletePaymentMethod;

/// <summary>My Remarks/Remarks2.md, bug 1.1 — unified delete button across List/Edit screens
/// (same soft-delete pattern as DeletePurchaseExpenseCommand).</summary>
public sealed record DeletePaymentMethodCommand(long Id) : IRequest;

public sealed class DeletePaymentMethodCommandHandler(IApplicationDbContext db) : IRequestHandler<DeletePaymentMethodCommand>
{
    public async Task Handle(DeletePaymentMethodCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.PaymentMethods.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentMethod), request.Id);

        db.PaymentMethods.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }
}
