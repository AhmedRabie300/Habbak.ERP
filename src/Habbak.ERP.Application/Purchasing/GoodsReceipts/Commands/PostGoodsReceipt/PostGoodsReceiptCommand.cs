using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Dtos;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.GoodsReceipts.Commands.PostGoodsReceipt;

/// <summary>
/// Posts a Draft goods receipt (screen #6) — this is where Purchasing finally moves real stock:
/// AcceptedQuantity posts into the receipt's own WarehouseId, RejectedQuantity posts into each
/// line's own RejectedWarehouseId (rule: rejected goods physically arrived too, just quarantined
/// elsewhere rather than added to sellable stock) — both via IStockMovementService (TransactionType
/// .Purchase), same engine every other module's stock movement goes through.
///
/// Also updates PurchaseOrderLine.ReceivedQuantity (rule 10) and recomputes the order's own Status
/// to PartiallyReceived or FullyReceived (section 6.3) — the first place those two enum values
/// actually become reachable.
///
/// When PurchaseCycleSettings.AutoCreateInvoiceOnReceipt is on, posting also drafts the matching
/// supplier invoice (see CreateInvoiceForReceiptAsync). The flag existed and was editable from the
/// settings screen long before anything read it; this is where it finally takes effect.
/// </summary>
public sealed record PostGoodsReceiptCommand(long Id, Guid? IdempotencyKey = null) : IRequest, IIdempotentRequest;

public sealed class PostGoodsReceiptCommandHandler(
    IApplicationDbContext db,
    ICurrentCompanyContext currentCompanyContext,
    IStockMovementService stockMovementService,
    ICodeGenerator codeGenerator)
    : IRequestHandler<PostGoodsReceiptCommand>
{
    public async Task Handle(PostGoodsReceiptCommand request, CancellationToken cancellationToken)
    {
        var receipt = await db.GoodsReceipts
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(GoodsReceipt), request.Id);

        if (receipt.Status != GoodsReceiptStatus.Draft)
        {
            throw new BusinessRuleException("PUR-RECEIPT-NOT-DRAFT", "لا يمكن ترحيل إذن الإضافة إلا وهو في حالة مسودة.");
        }

        // Remarks4 item 6 — PurchaseCycleSettings.CapitalizeAdditionalCosts: freight, customs and
        // the rest were already spread across the invoice's lines; when the company capitalises
        // them, they belong in what the stock is worth, not in a period expense. Costs land per
        // base unit of the invoice line, so a partial receipt carries its share and no more.
        var capitalizedByItemId = await AdditionalCostPerBaseUnitAsync(receipt, cancellationToken);

        // Null for an invoice-sourced receipt (the "invoice only" cycle raises no order at all).
        var purchaseOrder = receipt.PurchaseOrderId is { } orderId
            ? await db.PurchaseOrders
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
              ?? throw new NotFoundException(nameof(PurchaseOrder), orderId)
            : null;

        foreach (var line in receipt.Lines)
        {
            if (line.AcceptedQuantity > 0)
            {
                await stockMovementService.ApplyMovementAsync(new StockMovementRequest
                {
                    CompanyId = currentCompanyContext.CompanyId,
                    WarehouseId = receipt.WarehouseId,
                    ItemId = line.ItemId,
                    TransactionType = TransactionType.Purchase,
                    // Stock is kept in base units; the line is in its own unit.
                    Quantity = ItemUnits.ToBase(line.AcceptedQuantity, line.UnitFactor),
                    UnitCost = line.BaseUnitCost + capitalizedByItemId.GetValueOrDefault(line.ItemId),
                    TransactionDate = receipt.ReceiptDate,
                    BatchNumber = line.BatchNumber,
                    ExpiryDate = line.ExpiryDate,
                    SourceDocumentType = "GoodsReceipt",
                    SourceDocumentId = receipt.Id
                }, cancellationToken);
            }

            if (line.RejectedQuantity > 0)
            {
                await stockMovementService.ApplyMovementAsync(new StockMovementRequest
                {
                    CompanyId = currentCompanyContext.CompanyId,
                    WarehouseId = line.RejectedWarehouseId!.Value,
                    ItemId = line.ItemId,
                    TransactionType = TransactionType.Purchase,
                    Quantity = ItemUnits.ToBase(line.RejectedQuantity, line.UnitFactor),
                    UnitCost = line.BaseUnitCost + capitalizedByItemId.GetValueOrDefault(line.ItemId),
                    TransactionDate = receipt.ReceiptDate,
                    BatchNumber = line.BatchNumber,
                    ExpiryDate = line.ExpiryDate,
                    SourceDocumentType = "GoodsReceipt",
                    SourceDocumentId = receipt.Id
                }, cancellationToken);
            }

            if (purchaseOrder is not null)
            {
                var orderLine = purchaseOrder.Lines.First(l => l.ItemId == line.ItemId);
                // Through base units: the order line may be in another unit than the receipt line.
                orderLine.ReceivedQuantity += Math.Round(line.BaseQuantity / orderLine.UnitFactor, 4);
            }
        }

        if (purchaseOrder is not null)
        {
            purchaseOrder.Status = purchaseOrder.Lines.All(l => l.ReceivedQuantity >= l.Quantity)
                ? PurchaseOrderStatus.FullyReceived
                : PurchaseOrderStatus.PartiallyReceived;
        }

        receipt.Status = GoodsReceiptStatus.Posted;

        var cycleSettings = await db.PurchaseCycleSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        if (cycleSettings?.AutoCreateInvoiceOnReceipt == true)
        {
            await CreateInvoiceForReceiptAsync(receipt, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The invoice's allocated additional cost per base unit, per item — empty when the receipt has
    /// no invoice behind it, when the company does not capitalise these costs, or when there were
    /// none. A line whose invoiced base quantity is zero contributes nothing rather than dividing by it.
    /// </summary>
    private async Task<Dictionary<long, decimal>> AdditionalCostPerBaseUnitAsync(GoodsReceipt receipt, CancellationToken cancellationToken)
    {
        if (receipt.PurchaseInvoiceId is not { } invoiceId)
        {
            return [];
        }

        var capitalize = await db.PurchaseCycleSettingsRows
            .Where(s => s.CompanyId == currentCompanyContext.CompanyId)
            .Select(s => (bool?)s.CapitalizeAdditionalCosts)
            .FirstOrDefaultAsync(cancellationToken);
        if (capitalize != true)
        {
            return [];
        }

        return await db.PurchaseInvoiceLines
            .Where(l => l.PurchaseInvoiceId == invoiceId && l.AllocatedAdditionalCost != 0 && l.BaseQuantity > 0)
            .GroupBy(l => l.ItemId)
            .Select(g => new { ItemId = g.Key, PerBaseUnit = g.Sum(l => l.AllocatedAdditionalCost) / g.Sum(l => l.BaseQuantity) })
            .ToDictionaryAsync(x => x.ItemId, x => x.PerBaseUnit, cancellationToken);
    }

    /// <summary>
    /// Drafts the supplier invoice that matches a freshly posted receipt.
    ///
    /// Deliberately created as <see cref="PurchaseInvoiceStatus.Draft"/>, never posted: the goods
    /// arriving is a fact the warehouse observed, but what the supplier actually bills is a separate
    /// document that still needs a human to reconcile against the paper invoice. Auto-posting would
    /// book a payable nobody checked.
    ///
    /// Priced at AcceptedQuantity × UnitCost — rejected goods physically arrived but were
    /// quarantined, so they do not belong on a payable by default. A line with nothing accepted is
    /// skipped entirely, and a receipt where everything was rejected produces no invoice at all
    /// rather than an empty one.
    /// </summary>
    private async Task CreateInvoiceForReceiptAsync(GoodsReceipt receipt, CancellationToken cancellationToken)
    {
        // Posting is Draft-only, so this cannot normally run twice for one receipt; the guard
        // covers a receipt that was invoiced manually before the flag was switched on.
        if (await db.PurchaseInvoices.AnyAsync(i => i.GoodsReceiptId == receipt.Id, cancellationToken))
        {
            return;
        }

        var billable = receipt.Lines
            .Where(l => l.AcceptedQuantity > 0)
            .Select(l => new PurchaseInvoiceLineInput(
                ItemId: l.ItemId,
                Quantity: l.AcceptedQuantity,
                ReceivedQuantity: l.AcceptedQuantity,
                UnitPrice: l.UnitCost,
                DiscountAmount: null,
                UnitId: l.UnitId,   // the invoice line re-checks and snapshots the unit
                AllocationPercentage: null,
                Weight: null))
            .ToList();

        if (billable.Count == 0)
        {
            return;
        }

        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == receipt.SupplierId, cancellationToken)
            ?? throw new NotFoundException(nameof(Supplier), receipt.SupplierId);

        var invoiceNumber = await codeGenerator.ResolveCodeAsync("PURCHASING_PURCHASE_INVOICE", null, cancellationToken);
        var units = await ItemUnits.LoadAsync(db, billable.Select(l => l.ItemId), cancellationToken);
        var (lines, subtotal) = PurchaseInvoiceLineBuilder.Build(billable, additionalCosts: 0m, allocationMethod: null, units);

        var invoice = new PurchaseInvoice
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = receipt.BranchId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = receipt.ReceiptDate,
            DueDate = receipt.ReceiptDate.AddDays(DueDays(supplier.PaymentTerms)),
            SupplierId = receipt.SupplierId,
            PurchaseOrderId = receipt.PurchaseOrderId,
            GoodsReceiptId = receipt.Id,
            CurrencyCode = supplier.CurrencyCode,
            ExchangeRate = 1m,
            PaymentTerms = supplier.PaymentTerms,
            Status = PurchaseInvoiceStatus.Draft,
            Subtotal = subtotal,
            TaxAmount = 0m,
            TotalAmount = subtotal,
            AdditionalCosts = 0m,
            Notes = $"فاتورة تلقائية من إذن الإضافة {receipt.ReceiptNumber}"
        };

        foreach (var line in lines)
        {
            invoice.Lines.Add(line);
        }

        db.PurchaseInvoices.Add(invoice);
    }

    private static int DueDays(SupplierPaymentTerms terms) => terms switch
    {
        SupplierPaymentTerms.Net15 => 15,
        SupplierPaymentTerms.Net30 => 30,
        SupplierPaymentTerms.Net60 => 60,
        _ => 0   // Cash
    };
}
