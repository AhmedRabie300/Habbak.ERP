using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Sales.SalesReturns.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesReturns.Queries.GetSalesReturnsList;

public sealed class GetSalesReturnsListQuery : ListQuery, IRequest<PagedResult<SalesReturnListItemDto>>;

public sealed class GetSalesReturnsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSalesReturnsListQuery, PagedResult<SalesReturnListItemDto>>
{
    public async Task<PagedResult<SalesReturnListItemDto>> Handle(GetSalesReturnsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.SalesReturns.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<SalesReturnStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            query = query.Where(r =>
                r.ReturnNumber.Contains(term) ||
                r.Customer!.NameAr.Contains(term) ||
                matchingStatuses.Contains(r.Status));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "returnDate" => descending ? query.OrderByDescending(r => r.ReturnDate) : query.OrderBy(r => r.ReturnDate),
            "status" => descending ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            _ => descending ? query.OrderByDescending(r => r.ReturnNumber) : query.OrderBy(r => r.ReturnNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new SalesReturnListItemDto
            {
                Id = r.Id,
                ReturnNumber = r.ReturnNumber,
                ReturnDate = r.ReturnDate,
                BranchId = r.BranchId,
                CustomerId = r.CustomerId,
                CustomerNameAr = r.Customer!.NameAr,
                WarehouseId = r.WarehouseId,
                WarehouseNameAr = r.Warehouse!.NameAr,
                SourceInvoiceId = r.SourceInvoiceId,
                Reason = r.Reason,
                LineCount = r.Lines.Count,
                Status = r.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SalesReturnListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
