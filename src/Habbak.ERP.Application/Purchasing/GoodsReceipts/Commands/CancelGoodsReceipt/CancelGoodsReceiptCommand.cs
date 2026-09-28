using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.GoodsReceipts.Commands.CancelGoodsReceipt;

/// <summary>Draft → Cancelled — safe before posting, since no stock has moved yet.</summary>
public sealed record CancelGoodsReceiptCommand(long Id) : IRequest;

public sealed class CancelGoodsReceiptCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelGoodsReceiptCommand>
{
    public async Task Handle(CancelGoodsReceiptCommand request, CancellationToken cancellationToken)
    {
        var receipt = await db.GoodsReceipts.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(GoodsReceipt), request.Id);

        if (receipt.Status != GoodsReceiptStatus.Draft)
        {
            throw new BusinessRuleException("PUR-RECEIPT-NOT-DRAFT", "لا يمكن إلغاء إذن الإضافة إلا وهو في حالة مسودة.");
        }

        receipt.Status = GoodsReceiptStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
    }
}
