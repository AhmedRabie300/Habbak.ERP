using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.DeclineRFQSupplier;

/// <summary>Marks one invited supplier as Declined (won't be quoting) — Pending → Declined.</summary>
public sealed record DeclineRFQSupplierCommand(long RFQSupplierId) : IRequest;

public sealed class DeclineRFQSupplierCommandHandler(IApplicationDbContext db) : IRequestHandler<DeclineRFQSupplierCommand>
{
    public async Task Handle(DeclineRFQSupplierCommand request, CancellationToken cancellationToken)
    {
        var rfqSupplier = await db.RFQSuppliers.FirstOrDefaultAsync(s => s.Id == request.RFQSupplierId, cancellationToken)
            ?? throw new NotFoundException(nameof(RFQSupplier), request.RFQSupplierId);

        if (rfqSupplier.Status != RFQSupplierStatus.Pending)
        {
            throw new BusinessRuleException("PUR-RFQ-SUPPLIER-NOT-PENDING", "هذا المورد له رد مسجَّل بالفعل.");
        }

        rfqSupplier.Status = RFQSupplierStatus.Declined;
        rfqSupplier.ResponseDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await db.SaveChangesAsync(cancellationToken);
    }
}
