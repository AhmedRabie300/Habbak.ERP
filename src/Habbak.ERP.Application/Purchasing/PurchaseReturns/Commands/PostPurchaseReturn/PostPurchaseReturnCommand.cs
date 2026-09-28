using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseReturns.Commands.PostPurchaseReturn;

/// <summary>
/// Draft → Posted (section 6, rule 7 "يُقلل المخزون"). For each line, an outbound
/// TransactionType.PurchaseReturn movement decreases the return's WarehouseId, and the return's
/// entry (rule 7's "قيداً محاسبياً عكسياً") comes from the PURCHASING_PURCHASE_RETURN template when
/// one is active.
///
/// The supplier is credited at the return price, but the goods leave the warehouse at its average
/// cost (rule 41) — the two rarely match, and the gap is a price variance rather than something to
/// hide in the inventory account. Tax follows the source invoice's own rate; a return with no
/// invoice behind it carries none, since there is no rate on record to apply.
/// </summary>
public sealed record PostPurchaseReturnCommand(long Id) : IRequest;

public sealed class PostPurchaseReturnCommandHandler(
    IApplicationDbContext db,
    IStockMovementService stockMovementService,
    ICurrentCompanyContext currentCompanyContext,
    IPostingTemplateEngine postingEngine)
    : IRequestHandler<PostPurchaseReturnCommand>
{
    public async Task Handle(PostPurchaseReturnCommand request, CancellationToken cancellationToken)
    {
        var purchaseReturn = await db.PurchaseReturns
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseReturn), request.Id);

        if (purchaseReturn.Status != PurchaseReturnStatus.Draft)
        {
            throw new BusinessRuleException("PUR-RETURN-NOT-DRAFT", "لا يمكن ترحيل مردود المشتريات إلا وهو في حالة مسودة.");
        }

        var costAmount = 0m;
        foreach (var line in purchaseReturn.Lines)
        {
            var applied = await stockMovementService.ApplyMovementAsync(new StockMovementRequest
            {
                CompanyId = currentCompanyContext.CompanyId,
                WarehouseId = purchaseReturn.WarehouseId,
                ItemId = line.ItemId,
                TransactionType = TransactionType.PurchaseReturn,
                // Stock is kept in base units; the line is in its own unit.
                Quantity = line.BaseQuantity,
                UnitCost = line.BaseUnitCost,
                TransactionDate = purchaseReturn.ReturnDate,
                BatchNumber = line.BatchNumber,
                SourceDocumentType = "PurchaseReturn",
                SourceDocumentId = purchaseReturn.Id
            }, cancellationToken);
            costAmount += line.BaseQuantity * applied.UnitCost;
        }

        purchaseReturn.Status = PurchaseReturnStatus.Posted;
        purchaseReturn.JournalEntry = await PostJournalEntryAsync(purchaseReturn, Math.Round(costAmount, 2), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<JournalEntry?> PostJournalEntryAsync(PurchaseReturn purchaseReturn, decimal costAmount, CancellationToken cancellationToken)
    {
        var returnValue = Math.Round(purchaseReturn.Lines.Sum(l => l.Quantity * l.UnitCost), 2);

        var taxRate = 0m;
        if (purchaseReturn.PurchaseInvoiceId is { } invoiceId)
        {
            var invoice = await db.PurchaseInvoices
                .Where(i => i.Id == invoiceId)
                .Select(i => new { i.TaxAmount, Net = i.Subtotal - (i.DiscountAmount ?? 0m) })
                .FirstOrDefaultAsync(cancellationToken);
            if (invoice is { Net: > 0 })
            {
                taxRate = invoice.TaxAmount / invoice.Net;
            }
        }

        var taxAmount = Math.Round(returnValue * taxRate, 2);
        var companyId = currentCompanyContext.CompanyId;

        return await postingEngine.PostIfConfiguredAsync(new TemplatePostingRequest
        {
            CompanyId = companyId,
            BranchId = purchaseReturn.BranchId,
            ScreenCode = PostingScreenCatalog.PurchaseReturn,
            SourceModule = SourceModule.Purchasing,
            SourceDocumentType = SourceDocumentType.Return,
            SourceDocumentId = purchaseReturn.Id,
            EntryDate = purchaseReturn.ReturnDate,
            Description = $"مرتجع مشتريات {purchaseReturn.ReturnNumber}",
            IdempotencyKey = PostingKeys.For(companyId, "PurchaseReturn.Post", purchaseReturn.Id),
            Context = PostingContext.Create(new Dictionary<string, object?>
            {
                ["SupplierId"] = purchaseReturn.SupplierId,
                ["BranchId"] = purchaseReturn.BranchId,
                ["WarehouseId"] = purchaseReturn.WarehouseId,
                ["ReturnValue"] = returnValue,
                ["TaxAmount"] = taxAmount,
                ["TotalAmount"] = returnValue + taxAmount,
                ["CostAmount"] = costAmount,
                [PostingScreenCatalog.HasStockMovementField] = costAmount > 0,
                ["PriceVarianceGain"] = Math.Max(returnValue - costAmount, 0m),
                ["PriceVarianceLoss"] = Math.Max(costAmount - returnValue, 0m)
            })
        }, cancellationToken);
    }
}
