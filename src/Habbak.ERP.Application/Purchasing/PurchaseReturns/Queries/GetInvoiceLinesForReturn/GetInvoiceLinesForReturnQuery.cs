using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseReturns.Queries.GetInvoiceLinesForReturn;

/// <summary>
/// The lines of an invoice, with how much of each has already gone back (Remarks4, item 8). This is
/// what the return screen fills itself from: the returner picks an invoice and edits quantities,
/// instead of typing item rows that may never have been bought from that supplier at all.
/// </summary>
public sealed record InvoiceLineForReturnDto
{
    public required long PurchaseInvoiceLineId { get; init; }
    public required long ItemId { get; init; }
    public required string ItemCode { get; init; }
    public required string ItemNameAr { get; init; }
    public required decimal InvoicedQuantity { get; init; }
    public required long UnitId { get; init; }
    public required string UnitCode { get; init; }
    public required decimal UnitCost { get; init; }

    /// <summary>Already returned on this line's earlier returns (Draft ones included — they are about to go).</summary>
    public required decimal ReturnedQuantity { get; init; }
    public required decimal ReturnableQuantity { get; init; }
}

public sealed record GetInvoiceLinesForReturnQuery(long InvoiceId, long? ExcludeReturnId = null)
    : IRequest<IReadOnlyList<InvoiceLineForReturnDto>>;

public sealed class GetInvoiceLinesForReturnQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetInvoiceLinesForReturnQuery, IReadOnlyList<InvoiceLineForReturnDto>>
{
    public async Task<IReadOnlyList<InvoiceLineForReturnDto>> Handle(GetInvoiceLinesForReturnQuery request, CancellationToken cancellationToken)
    {
        var invoice = await db.PurchaseInvoices.AsNoTracking()
                          .Include(i => i.Lines).ThenInclude(l => l.Item)
                          .Include(i => i.Lines).ThenInclude(l => l.Unit)
                          .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
                      ?? throw new NotFoundException(nameof(PurchaseInvoice), request.InvoiceId);

        var returned = await db.PurchaseReturnLines.AsNoTracking()
            .Where(l => l.PurchaseInvoiceLineId != null
                        && l.PurchaseReturn!.PurchaseInvoiceId == request.InvoiceId
                        && l.PurchaseReturn.Status != PurchaseReturnStatus.Cancelled
                        && (request.ExcludeReturnId == null || l.PurchaseReturnId != request.ExcludeReturnId))
            .GroupBy(l => l.PurchaseInvoiceLineId!.Value)
            .Select(g => new { LineId = g.Key, BaseQuantity = g.Sum(l => l.BaseQuantity) })
            .ToDictionaryAsync(x => x.LineId, x => x.BaseQuantity, cancellationToken);

        return invoice.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l =>
            {
                // Returned quantities are summed in base units, then shown in the invoice line's own unit.
                var returnedInLineUnit = l.UnitFactor == 0 ? 0m : Math.Round(returned.GetValueOrDefault(l.Id) / l.UnitFactor, 4);
                return new InvoiceLineForReturnDto
                {
                    PurchaseInvoiceLineId = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    InvoicedQuantity = l.Quantity,
                    UnitId = l.UnitId,
                    UnitCode = l.Unit!.Code,
                    UnitCost = l.UnitPrice,
                    ReturnedQuantity = returnedInLineUnit,
                    ReturnableQuantity = Math.Max(0m, l.Quantity - returnedInLineUnit)
                };
            })
            .ToList();
    }
}
