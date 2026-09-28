using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesInvoices.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesInvoices.Queries.GetSalesInvoiceById;

public sealed record GetSalesInvoiceByIdQuery(long Id) : IRequest<SalesInvoiceDetailDto>;

public sealed class GetSalesInvoiceByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetSalesInvoiceByIdQuery, SalesInvoiceDetailDto>
{
    public async Task<SalesInvoiceDetailDto> Handle(GetSalesInvoiceByIdQuery request, CancellationToken cancellationToken)
    {
        var invoice = await db.SalesInvoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.SourceOrder)
            .Include(i => i.JournalEntry)
            .Include(i => i.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesInvoice), request.Id);

        var reversal = invoice.JournalEntryId is { } entryId
            ? await db.JournalEntries.AsNoTracking()
                .Where(e => e.ReversalOfEntryId == entryId)
                .Select(e => new { e.Id, e.EntryNumber })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new SalesInvoiceDetailDto
        {
            Id = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            InvoiceDate = invoice.InvoiceDate,
            BranchId = invoice.BranchId,
            CustomerId = invoice.CustomerId,
            CustomerNameAr = invoice.Customer!.NameAr,
            SourceOrderId = invoice.SourceOrderId,
            SourceOrderNumber = invoice.SourceOrder?.OrderNumber,
            PaymentType = invoice.PaymentType.ToString(),
            CreditLimitOverrideApproved = invoice.CreditLimitOverrideApproved,
            Subtotal = invoice.Subtotal,
            DiscountAmount = invoice.DiscountAmount,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            AmountPaid = invoice.AmountPaid,
            Status = invoice.Status.ToString(),
            JournalEntryId = invoice.JournalEntryId,
            JournalEntryNumber = invoice.JournalEntry?.EntryNumber,
            ReversalJournalEntryId = reversal?.Id,
            ReversalJournalEntryNumber = reversal?.EntryNumber,
            RowVersion = Convert.ToBase64String(invoice.RowVersion),
            Lines = invoice.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new SalesInvoiceLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    DiscountAmount = l.DiscountAmount,
                    LineTotal = l.LineTotal
                })
                .ToList()
        };
    }
}
