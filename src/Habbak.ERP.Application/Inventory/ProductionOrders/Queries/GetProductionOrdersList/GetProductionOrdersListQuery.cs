using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Inventory.ProductionOrders.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.ProductionOrders.Queries.GetProductionOrdersList;

public sealed class GetProductionOrdersListQuery : ListQuery, IRequest<PagedResult<ProductionOrderListItemDto>>;

public sealed class GetProductionOrdersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetProductionOrdersListQuery, PagedResult<ProductionOrderListItemDto>>
{
    public async Task<PagedResult<ProductionOrderListItemDto>> Handle(
        GetProductionOrdersListQuery request, CancellationToken cancellationToken)
    {
        var query = db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.Warehouse)
            .Include(o => o.Recipe).ThenInclude(r => r!.OutputItem)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<ProductionOrderStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            query = query.Where(o =>
                o.OrderNumber.Contains(term) ||
                o.Recipe!.OutputItem!.Code.Contains(term) ||
                o.Recipe!.OutputItem!.NameAr.Contains(term) ||
                matchingStatuses.Contains(o.Status));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "status" => descending ? query.OrderByDescending(o => o.Status) : query.OrderBy(o => o.Status),
            _ => descending ? query.OrderByDescending(o => o.OrderNumber) : query.OrderBy(o => o.OrderNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new ProductionOrderListItemDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                WarehouseId = o.WarehouseId,
                WarehouseCode = o.Warehouse!.Code,
                RecipeFamilyCode = o.Recipe!.RecipeFamilyCode,
                RecipeVersionNumber = o.Recipe!.VersionNumber,
                OutputItemCode = o.Recipe!.OutputItem!.Code,
                OutputItemNameAr = o.Recipe!.OutputItem!.NameAr,
                PlannedQuantity = o.PlannedQuantity,
                ActualQuantity = o.ActualQuantity,
                Status = o.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductionOrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
