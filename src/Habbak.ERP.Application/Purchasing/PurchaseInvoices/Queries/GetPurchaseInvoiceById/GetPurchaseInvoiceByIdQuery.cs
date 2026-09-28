using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices.Queries.GetPurchaseInvoiceById;

public sealed record GetPurchaseInvoiceByIdQuery(long Id) : IRequest<PurchaseInvoiceDetailDto>;

public sealed class GetPurchaseInvoiceByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseInvoiceByIdQuery, PurchaseInvoiceDetailDto>
{
    public async Task<PurchaseInvoiceDetailDto> Handle(GetPurchaseInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await db.PurchaseInvoices
            .AsNoTracking()
            .Include(i => i.Supplier)
            .Include(i => i.PurchaseOrder)
            .Include(i => i.GoodsReceipt)
            .Include(i => i.JournalEntry)
            .Include(i => i.Lines).ThenInclude(l => l.Item)
            .Include(i => i.Lines).ThenInclude(l => l.Unit)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseInvoice), request.Id);

        var reversal = invoice.JournalEntryId is { } entryId
            ? await db.JournalEntries
                .AsNoTracking()
                .Where(e => e.ReversalOfEntryId == entryId)
                .Select(e => new { e.Id, e.EntryNumber })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new PurchaseInvoiceDetailDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            BranchId = invoice.BranchId,
            SupplierId = invoice.SupplierId,
            SupplierCode = invoice.Supplier!.Code,
            SupplierNameAr = invoice.Supplier!.NameAr,
            SupplierInvoiceNumber = invoice.SupplierInvoiceNumber,
            PurchaseOrderId = invoice.PurchaseOrderId,
            PurchaseOrderNumber = invoice.PurchaseOrder?.OrderNumber,
            GoodsReceiptId = invoice.GoodsReceiptId,
            GoodsReceiptNumber = invoice.GoodsReceipt?.ReceiptNumber,
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRate = invoice.ExchangeRate,
            PaymentTerms = invoice.PaymentTerms.ToString(),
            Status = invoice.Status.ToString(),
            Subtotal = invoice.Subtotal,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            DiscountAmount = invoice.DiscountAmount,
            DiscountReason = invoice.DiscountReason,
            AmountPaid = invoice.AmountPaid,
            AdditionalCosts = invoice.AdditionalCosts,
            AdditionalCostAllocationMethod = invoice.AdditionalCostAllocationMethod?.ToString(),
            CommissionRate = invoice.CommissionRate,
            CommissionAmount = invoice.CommissionAmount,
            CommissionAccountId = invoice.CommissionAccountId,
            Notes = invoice.Notes,
            JournalEntryId = invoice.JournalEntryId,
            JournalEntryNumber = invoice.JournalEntry?.EntryNumber,
            ReversalJournalEntryId = reversal?.Id,
            ReversalJournalEntryNumber = reversal?.EntryNumber,
            RowVersion = Convert.ToBase64String(invoice.RowVersion),
            Lines = invoice.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new PurchaseInvoiceLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    Quantity = l.Quantity,
                    ReceivedQuantity = l.ReceivedQuantity,
                    UnitPrice = l.UnitPrice,
                    TotalPrice = l.TotalPrice,
                    DiscountAmount = l.DiscountAmount,
                    UnitId = l.UnitId,
                    UnitCode = l.Unit!.Code,
                    UnitFactor = l.UnitFactor,
                    BaseQuantity = l.BaseQuantity,
                    BaseUnitCost = l.BaseUnitCost,
                    AllocatedAdditionalCost = l.AllocatedAdditionalCost,
                    PurchaseOrderLineId = l.PurchaseOrderLineId,
                    AllocationPercentage = l.AllocationPercentage,
                    Weight = l.Weight
                })
                .ToList()
        };
    }
}
