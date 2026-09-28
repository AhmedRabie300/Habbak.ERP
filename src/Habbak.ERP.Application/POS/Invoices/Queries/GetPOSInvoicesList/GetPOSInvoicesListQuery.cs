using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Invoices.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Invoices.Queries.GetPOSInvoicesList;

public sealed record GetPOSInvoicesListQuery(long? POSTerminalId, long? ShiftId) : IRequest<IReadOnlyList<POSInvoiceListItemDto>>;

public sealed class GetPOSInvoicesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPOSInvoicesListQuery, IReadOnlyList<POSInvoiceListItemDto>>
{
    public async Task<IReadOnlyList<POSInvoiceListItemDto>> Handle(GetPOSInvoicesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.POSInvoices.AsNoTracking().AsQueryable();

        if (request.POSTerminalId is { } terminalId)
        {
            query = query.Where(i => i.POSTerminalId == terminalId);
        }

        if (request.ShiftId is { } shiftId)
        {
            query = query.Where(i => i.ShiftId == shiftId);
        }

        return await query
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new POSInvoiceListItemDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate.ToString("yyyy-MM-dd"),
                OrderType = i.OrderType.ToString(),
                Total = i.Total,
                Status = i.Status.ToString()
            })
            .ToListAsync(cancellationToken);
    }
}
