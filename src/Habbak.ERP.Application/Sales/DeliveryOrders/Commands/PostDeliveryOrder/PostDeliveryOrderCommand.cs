using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Sales;
using Habbak.ERP.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.DeliveryOrders.Commands.PostDeliveryOrder;

/// <summary>
/// Posts a Draft delivery order (screen #8) — this is where Sales finally moves real stock: each
/// line posts an outbound TransactionType.SalesDelivery movement into the order's own WarehouseId,
/// via IStockMovementService, same engine GoodsReceipt uses on the Purchasing side (rule 17).
/// The goods leave at the warehouse's average cost (rule 41), and that cost is what the
/// SALES_DELIVERY_ORDER template posts as cost of sales — this is the first moment it is known.
///
/// When linked to a SourceOrderId, also updates SalesOrderLine.DeliveredQuantity and recomputes the
/// order's own Status to PartiallyDelivered/Delivered (section 4.2) — the first place those two
/// enum values actually become reachable, mirroring PostGoodsReceiptCommand's equivalent role for
/// PurchaseOrderStatus.PartiallyReceived/FullyReceived.
/// </summary>
public sealed record PostDeliveryOrderCommand(long Id, Guid? IdempotencyKey = null) : IRequest, IIdempotentRequest;

public sealed class PostDeliveryOrderCommandHandler(
    IApplicationDbContext db,
    ICurrentCompanyContext currentCompanyContext,
    IStockMovementService stockMovementService,
    IPostingTemplateEngine postingEngine)
    : IRequestHandler<PostDeliveryOrderCommand>
{
    public async Task Handle(PostDeliveryOrderCommand request, CancellationToken cancellationToken)
    {
        var deliveryOrder = await db.DeliveryOrders
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliveryOrder), request.Id);

        if (deliveryOrder.Status != DeliveryOrderStatus.Draft)
        {
            throw new BusinessRuleException("SALES-DELIVERY-ORDER-NOT-DRAFT", "لا يمكن ترحيل أمر التسليم إلا وهو في حالة مسودة.");
        }

        SalesOrder? sourceOrder = null;
        if (deliveryOrder.SourceOrderId is { } sourceOrderId)
        {
            sourceOrder = await db.SalesOrders
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.Id == sourceOrderId, cancellationToken);
        }

        // Rule 42: a sales invoice names no warehouse, so despatch is the first moment its lines
        // have a real cost to freeze. Loaded here so the movement loop can stamp each one.
        SalesInvoice? sourceInvoice = null;
        if (deliveryOrder.SourceInvoiceId is { } sourceInvoiceId)
        {
            sourceInvoice = await db.SalesInvoices
                .Include(i => i.Lines)
                .FirstOrDefaultAsync(i => i.Id == sourceInvoiceId, cancellationToken);
        }

        var costAmount = 0m;
        foreach (var line in deliveryOrder.Lines)
        {
            var item = await db.Items.FirstOrDefaultAsync(i => i.Id == line.ItemId, cancellationToken)
                ?? throw new NotFoundException(nameof(Item), line.ItemId);

            var applied = await stockMovementService.ApplyMovementAsync(new StockMovementRequest
            {
                CompanyId = currentCompanyContext.CompanyId,
                WarehouseId = deliveryOrder.WarehouseId,
                ItemId = line.ItemId,
                TransactionType = TransactionType.SalesDelivery,
                Quantity = line.Quantity,
                // Outbound: costed from the warehouse's average by StockMovementService (rule 41).
                UnitCost = 0m,
                TransactionDate = deliveryOrder.DeliveryDate,
                BatchNumber = line.BatchNumber,
                SourceDocumentType = "DeliveryOrder",
                SourceDocumentId = deliveryOrder.Id
            }, cancellationToken);

            costAmount += line.Quantity * applied.UnitCost;

            // Freeze the despatch cost on the invoice line this delivery fulfils. A partial second
            // delivery would overwrite it; matching that properly needs a delivered-quantity
            // weighted average, which is only worth building once partial despatch is actually used.
            var invoiceLine = sourceInvoice?.Lines.FirstOrDefault(l => l.ItemId == line.ItemId);
            if (invoiceLine is not null)
            {
                invoiceLine.UnitCost = applied.UnitCost;
            }

            var orderLine = sourceOrder?.Lines.FirstOrDefault(l => l.ItemId == line.ItemId);
            if (orderLine is not null)
            {
                orderLine.DeliveredQuantity += line.Quantity;
            }
        }

        if (sourceOrder is not null)
        {
            sourceOrder.Status = sourceOrder.Lines.All(l => l.DeliveredQuantity >= l.Quantity)
                ? SalesOrderStatus.Delivered
                : SalesOrderStatus.PartiallyDelivered;
        }

        deliveryOrder.Status = DeliveryOrderStatus.Posted;

        // Goods that cost nothing (a free sample booked at zero) leave no cost of sales to post; the
        // engine skips a stock-triggered template when HasStockMovement is false.
        costAmount = Math.Round(costAmount, 2);
        var companyId = currentCompanyContext.CompanyId;
        deliveryOrder.JournalEntry = await postingEngine.PostIfConfiguredAsync(new TemplatePostingRequest
        {
            CompanyId = companyId,
            BranchId = deliveryOrder.BranchId,
            ScreenCode = PostingScreenCatalog.DeliveryOrder,
            SourceModule = SourceModule.Sales,
            SourceDocumentType = SourceDocumentType.DeliveryOrder,
            SourceDocumentId = deliveryOrder.Id,
            EntryDate = deliveryOrder.DeliveryDate,
            Description = $"تكلفة مبيعات — أمر تسليم {deliveryOrder.DeliveryNumber}",
            IdempotencyKey = PostingKeys.For(companyId, "DeliveryOrder.Post", deliveryOrder.Id),
            Context = PostingContext.Create(new Dictionary<string, object?>
            {
                ["CustomerId"] = deliveryOrder.CustomerId,
                ["BranchId"] = deliveryOrder.BranchId,
                ["WarehouseId"] = deliveryOrder.WarehouseId,
                ["CostAmount"] = costAmount,
                [PostingScreenCatalog.HasStockMovementField] = costAmount > 0
            })
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
