using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Purchasing.SupplierEvaluations.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.SupplierEvaluations.Queries.GetSupplierEvaluationsList;

public sealed class GetSupplierEvaluationsListQuery : ListQuery, IRequest<PagedResult<SupplierEvaluationListItemDto>>;

public sealed class GetSupplierEvaluationsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSupplierEvaluationsListQuery, PagedResult<SupplierEvaluationListItemDto>>
{
    public async Task<PagedResult<SupplierEvaluationListItemDto>> Handle(GetSupplierEvaluationsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.SupplierEvaluations.AsNoTracking().Include(e => e.Supplier).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            DateOnly? dateTerm = DateOnly.TryParse(term, out var parsedDate) ? parsedDate : null;

            query = query.Where(e =>
                e.Supplier!.Code.Contains(term) ||
                e.Supplier!.NameAr.Contains(term) ||
                (dateTerm != null && e.EvaluationDate == dateTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "evaluationDate" => descending ? query.OrderByDescending(e => e.EvaluationDate) : query.OrderBy(e => e.EvaluationDate),
            "overallScore" => descending ? query.OrderByDescending(e => e.OverallScore) : query.OrderBy(e => e.OverallScore),
            _ => descending ? query.OrderByDescending(e => e.EvaluationDate) : query.OrderBy(e => e.EvaluationDate)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new SupplierEvaluationListItemDto
            {
                Id = e.Id,
                SupplierId = e.SupplierId,
                SupplierCode = e.Supplier!.Code,
                SupplierNameAr = e.Supplier!.NameAr,
                EvaluationDate = e.EvaluationDate,
                OverallScore = e.OverallScore
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SupplierEvaluationListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
