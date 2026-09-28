using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseReturns.Queries.GetPurchaseReturnsList;

public sealed class GetPurchaseReturnsListQuery : ListQuery, IRequest<PagedResult<PurchaseReturnListItemDto>>;

public sealed class GetPurchaseReturnsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseReturnsListQuery, PagedResult<PurchaseReturnListItemDto>>
{
    public async Task<PagedResult<PurchaseReturnListItemDto>> Handle(GetPurchaseReturnsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.PurchaseReturns.AsNoTracking().Include(r => r.Supplier).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingReasons = Enum.GetValues<PurchaseReturnReason>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var matchingStatuses = Enum.GetValues<PurchaseReturnStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            DateOnly? dateTerm = DateOnly.TryParse(term, out var parsedDate) ? parsedDate : null;

            query = query.Where(r =>
                r.ReturnNumber.Contains(term) ||
                r.Supplier!.Code.Contains(term) ||
                r.Supplier!.NameAr.Contains(term) ||
                matchingReasons.Contains(r.Reason) ||
                matchingStatuses.Contains(r.Status) ||
                (dateTerm != null && r.ReturnDate == dateTerm));
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
            .Select(r => new PurchaseReturnListItemDto
            {
                Id = r.Id,
                ReturnNumber = r.ReturnNumber,
                ReturnDate = r.ReturnDate,
                SupplierId = r.SupplierId,
                SupplierCode = r.Supplier!.Code,
                SupplierNameAr = r.Supplier!.NameAr,
                Reason = r.Reason.ToString(),
                LineCount = r.Lines.Count,
                Status = r.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseReturnListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
