using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Returns.Dtos;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Returns.Queries.GetPOSReturnById;

public sealed record GetPOSReturnByIdQuery(long Id) : IRequest<POSReturnDetailDto>;

public sealed class GetPOSReturnByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPOSReturnByIdQuery, POSReturnDetailDto>
{
    public async Task<POSReturnDetailDto> Handle(GetPOSReturnByIdQuery request, CancellationToken cancellationToken)
    {
        var posReturn = await db.POSReturns
            .AsNoTracking()
            .Include(r => r.SourceInvoice)
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(POSReturn), request.Id);

        var itemIds = posReturn.Lines.Select(l => l.ItemId).Distinct().ToList();
        var items = await db.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Code, i.NameAr })
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var lines = posReturn.Lines
            .OrderBy(l => l.LineNumber)
            .Select(l => new POSReturnLineDto
            {
                ItemId = l.ItemId,
                ItemCode = items.TryGetValue(l.ItemId, out var item) ? item.Code : "",
                ItemNameAr = items.TryGetValue(l.ItemId, out var item2) ? item2.NameAr : "",
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.Quantity * l.UnitPrice
            })
            .ToList();

        return new POSReturnDetailDto
        {
            Id = posReturn.Id,
            ReturnNumber = posReturn.ReturnNumber,
            ReturnDate = posReturn.ReturnDate.ToString("yyyy-MM-dd"),
            SourceInvoiceId = posReturn.SourceInvoiceId,
            SourceInvoiceNumber = posReturn.SourceInvoice!.InvoiceNumber,
            Reason = posReturn.Reason,
            Status = posReturn.Status.ToString(),
            Total = lines.Sum(l => l.LineTotal),
            Lines = lines
        };
    }
}
