using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Warehouses.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Warehouses.Queries.GetWarehousesList;

/// <summary>Not a paginated GetList screen: a company's warehouse count is always small.</summary>
public sealed record GetWarehousesListQuery : IRequest<IReadOnlyList<WarehouseDto>>;

public sealed class GetWarehousesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetWarehousesListQuery, IReadOnlyList<WarehouseDto>>
{
    public async Task<IReadOnlyList<WarehouseDto>> Handle(GetWarehousesListQuery request, CancellationToken cancellationToken)
    {
        return await db.Warehouses
            .AsNoTracking()
            .OrderBy(w => w.Code)
            .Select(w => new WarehouseDto
            {
                Id = w.Id,
                BranchId = w.BranchId,
                Code = w.Code,
                NameAr = w.NameAr,
                NameEn = w.NameEn,
                WarehouseType = w.WarehouseType.ToString(),
                AllowNegativeBalance = w.AllowNegativeBalance,
                IsActive = w.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
