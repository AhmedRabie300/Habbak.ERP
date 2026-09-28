using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices.Queries.GetPayableInvoicesList;

public sealed record PayableInvoiceDto
{
    public required long Id { get; init; }
    public required string InvoiceNumber { get; init; }
    public required decimal TotalAmount { get; init; }
    public required decimal AmountPaid { get; init; }
    public required decimal RemainingAmount { get; init; }
    public required string CurrencyCode { get; init; }
}

/// <summary>Feeds the Supplier Payments (screen #9) invoice picker — Posted/PartiallyPaid invoices
/// for one supplier, with the balance still owed.</summary>
public sealed record GetPayableInvoicesListQuery(long SupplierId) : IRequest<IReadOnlyList<PayableInvoiceDto>>;

public sealed class GetPayableInvoicesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPayableInvoicesListQuery, IReadOnlyList<PayableInvoiceDto>>
{
    public async Task<IReadOnlyList<PayableInvoiceDto>> Handle(GetPayableInvoicesListQuery request, CancellationToken cancellationToken) =>
        await db.PurchaseInvoices
            .AsNoTracking()
            .Where(i => i.SupplierId == request.SupplierId
                && (i.Status == PurchaseInvoiceStatus.Posted || i.Status == PurchaseInvoiceStatus.PartiallyPaid))
            .OrderBy(i => i.DueDate)
            .Select(i => new PayableInvoiceDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                TotalAmount = i.TotalAmount,
                AmountPaid = i.AmountPaid,
                RemainingAmount = i.TotalAmount - i.AmountPaid,
                CurrencyCode = i.CurrencyCode
            })
            .ToListAsync(cancellationToken);
}
