using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Returns.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Returns.Queries.GetPOSReturnsList;

public sealed record GetPOSReturnsListQuery : IRequest<IReadOnlyList<POSReturnListItemDto>>;

public sealed class GetPOSReturnsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPOSReturnsListQuery, IReadOnlyList<POSReturnListItemDto>>
{
    public async Task<IReadOnlyList<POSReturnListItemDto>> Handle(GetPOSReturnsListQuery request, CancellationToken cancellationToken)
    {
        var returns = await db.POSReturns
            .AsNoTracking()
            .Include(r => r.SourceInvoice)
            .Include(r => r.Lines)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return returns.Select(r => new POSReturnListItemDto
        {
            Id = r.Id,
            ReturnNumber = r.ReturnNumber,
            ReturnDate = r.ReturnDate.ToString("yyyy-MM-dd"),
            SourceInvoiceNumber = r.SourceInvoice!.InvoiceNumber,
            Reason = r.Reason,
            Total = r.Lines.Sum(l => l.Quantity * l.UnitPrice),
            Status = r.Status.ToString()
        }).ToList();
    }
}
