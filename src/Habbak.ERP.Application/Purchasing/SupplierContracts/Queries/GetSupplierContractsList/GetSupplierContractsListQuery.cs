using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Purchasing.SupplierContracts.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.SupplierContracts.Queries.GetSupplierContractsList;

public sealed class GetSupplierContractsListQuery : ListQuery, IRequest<PagedResult<SupplierContractListItemDto>>;

public sealed class GetSupplierContractsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSupplierContractsListQuery, PagedResult<SupplierContractListItemDto>>
{
    public async Task<PagedResult<SupplierContractListItemDto>> Handle(GetSupplierContractsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.SupplierContracts.AsNoTracking().Include(c => c.Supplier).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<SupplierContractStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            query = query.Where(c =>
                c.ContractNumber.Contains(term) ||
                c.Supplier!.Code.Contains(term) ||
                c.Supplier!.NameAr.Contains(term) ||
                matchingStatuses.Contains(c.Status));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "startDate" => descending ? query.OrderByDescending(c => c.StartDate) : query.OrderBy(c => c.StartDate),
            "endDate" => descending ? query.OrderByDescending(c => c.EndDate) : query.OrderBy(c => c.EndDate),
            "status" => descending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            _ => descending ? query.OrderByDescending(c => c.ContractNumber) : query.OrderBy(c => c.ContractNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new SupplierContractListItemDto
            {
                Id = c.Id,
                ContractNumber = c.ContractNumber,
                SupplierId = c.SupplierId,
                SupplierCode = c.Supplier!.Code,
                SupplierNameAr = c.Supplier!.NameAr,
                StartDate = c.StartDate,
                EndDate = c.EndDate,
                ItemCount = c.Items.Count,
                Status = c.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SupplierContractListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
