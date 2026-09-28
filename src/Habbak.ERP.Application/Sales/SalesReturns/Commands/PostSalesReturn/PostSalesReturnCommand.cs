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

namespace Habbak.ERP.Application.Sales.SalesReturns.Commands.PostSalesReturn;

/// <summary>
/// Posts a Draft sales return (screen #9) — for each line, an inbound TransactionType.SalesReturn
/// movement increases the return's own WarehouseId (قاعدة 19), via IStockMovementService, mirroring
/// PostPurchaseReturnCommand's role on the other side of the cycle. Rule 19's "قيد عكسي" comes from
/// the SALES_RETURN template when one is active: revenue and tax back from the customer, and
/// the goods back into stock at the cost they re-enter the warehouse at. Tax follows the source
/// invoice's own rate; a return with no invoice behind it carries none.
///
/// Does not touch SourceInvoice.AmountPaid/TotalAmount — PurchaseReturn doesn't reverse
/// PurchaseInvoice.AmountPaid either, so this stays a pure inventory-side document until a real
/// credit-note/AR-reversal mechanism is designed.
/// </summary>
public sealed record PostSalesReturnCommand(long Id, Guid? IdempotencyKey = null) : IRequest, IIdempotentRequest;

public sealed class PostSalesReturnCommandHandler(
    IApplicationDbContext db,
    ICurrentCompanyContext currentCompanyContext,
    IStockMovementService stockMovementService,
    IPostingTemplateEngine postingEngine)
    : IRequestHandler<PostSalesReturnCommand>
{
    public async Task Handle(PostSalesReturnCommand request, CancellationToken cancellationToken)
    {
        var salesReturn = await db.SalesReturns
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesReturn), request.Id);

        if (salesReturn.Status != SalesReturnStatus.Draft)
        {
            throw new BusinessRuleException("SALES-RETURN-NOT-DRAFT", "لا يمكن ترحيل مرتجع المبيعات إلا وهو في حالة مسودة.");
        }

        var costAmount = 0m;
        foreach (var line in salesReturn.Lines)
        {
            var item = await db.Items.FirstOrDefaultAsync(i => i.Id == line.ItemId, cancellationToken)
                ?? throw new NotFoundException(nameof(Item), line.ItemId);

            var applied = await stockMovementService.ApplyMovementAsync(new StockMovementRequest
            {
                CompanyId = currentCompanyContext.CompanyId,
                WarehouseId = salesReturn.WarehouseId,
                ItemId = line.ItemId,
                TransactionType = TransactionType.SalesReturn,
                Quantity = line.Quantity,
                UnitCost = await stockMovementService.ResolveInboundCostAsync(line.ItemId, salesReturn.WarehouseId, cancellationToken),
                TransactionDate = salesReturn.ReturnDate,
                BatchNumber = line.BatchNumber,
                SourceDocumentType = "SalesReturn",
                SourceDocumentId = salesReturn.Id
            }, cancellationToken);
            costAmount += line.Quantity * applied.UnitCost;
        }

        salesReturn.Status = SalesReturnStatus.Posted;
        salesReturn.JournalEntry = await PostJournalEntryAsync(salesReturn, Math.Round(costAmount, 2), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<JournalEntry?> PostJournalEntryAsync(SalesReturn salesReturn, decimal costAmount, CancellationToken cancellationToken)
    {
        var returnValue = Math.Round(salesReturn.Lines.Sum(l => l.Quantity * l.UnitPrice), 2);

        var taxRate = 0m;
        if (salesReturn.SourceInvoiceId is { } invoiceId)
        {
            var invoice = await db.SalesInvoices
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
            BranchId = salesReturn.BranchId,
            ScreenCode = PostingScreenCatalog.SalesReturn,
            SourceModule = SourceModule.Sales,
            SourceDocumentType = SourceDocumentType.Return,
            SourceDocumentId = salesReturn.Id,
            EntryDate = salesReturn.ReturnDate,
            Description = $"مرتجع مبيعات {salesReturn.ReturnNumber}",
            IdempotencyKey = PostingKeys.For(companyId, "SalesReturn.Post", salesReturn.Id),
            Context = PostingContext.Create(new Dictionary<string, object?>
            {
                ["CustomerId"] = salesReturn.CustomerId,
                ["BranchId"] = salesReturn.BranchId,
                ["WarehouseId"] = salesReturn.WarehouseId,
                ["ReturnValue"] = returnValue,
                ["TaxAmount"] = taxAmount,
                ["TotalAmount"] = returnValue + taxAmount,
                ["CostAmount"] = costAmount,
                [PostingScreenCatalog.HasStockMovementField] = costAmount > 0
            })
        }, cancellationToken);
    }
}
