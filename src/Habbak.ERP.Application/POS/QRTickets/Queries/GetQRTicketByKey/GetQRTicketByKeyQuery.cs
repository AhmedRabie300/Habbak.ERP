using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.QRTickets.Queries.GetQRTicketByKey;

/// <summary>معاينة تذكرة QR قبل تأكيد المسح (شاشة البيع) — تعرض بنودها وحالتها الحالية.</summary>
public sealed record GetQRTicketByKeyQuery(Guid IdempotencyKey) : IRequest<QRTicketPreviewDto>;

public sealed class QRTicketPreviewLineDto
{
    public required long ItemId { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
}

public sealed class QRTicketPreviewDto
{
    public required long Id { get; init; }
    public required Guid IdempotencyKey { get; init; }
    public required string Status { get; init; }
    public required IReadOnlyList<QRTicketPreviewLineDto> Lines { get; init; }
}

public sealed class GetQRTicketByKeyQueryHandler(IApplicationDbContext db) : IRequestHandler<GetQRTicketByKeyQuery, QRTicketPreviewDto>
{
    public async Task<QRTicketPreviewDto> Handle(GetQRTicketByKeyQuery request, CancellationToken cancellationToken)
    {
        var ticket = await db.QRTickets.AsNoTracking().Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.IdempotencyKey == request.IdempotencyKey, cancellationToken)
            ?? throw new NotFoundException(nameof(QRTicket), request.IdempotencyKey);

        return new QRTicketPreviewDto
        {
            Id = ticket.Id,
            IdempotencyKey = ticket.IdempotencyKey,
            Status = ticket.Status.ToString(),
            Lines = ticket.Lines.OrderBy(l => l.LineNumber).Select(l => new QRTicketPreviewLineDto
            {
                ItemId = l.ItemId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice
            }).ToList()
        };
    }
}
