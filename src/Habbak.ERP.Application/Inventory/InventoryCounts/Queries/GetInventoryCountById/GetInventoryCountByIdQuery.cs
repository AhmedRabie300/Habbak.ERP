using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.InventoryCounts.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.InventoryCounts.Queries.GetInventoryCountById;

public sealed record GetInventoryCountByIdQuery(long Id) : IRequest<InventoryCountDetailDto>;

public sealed class GetInventoryCountByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetInventoryCountByIdQuery, InventoryCountDetailDto>
{
    public async Task<InventoryCountDetailDto> Handle(GetInventoryCountByIdQuery request, CancellationToken cancellationToken)
    {
        var count = await db.InventoryCounts
            .AsNoTracking()
            .Include(c => c.Warehouse)
            .Include(c => c.Lines).ThenInclude(l => l.Item).ThenInclude(i => i!.BaseUnitOfMeasure)
            .Include(c => c.Lines).ThenInclude(l => l.Unit)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryCount), request.Id);

        return new InventoryCountDetailDto
        {
            Id = count.Id,
            CountNumber = count.CountNumber,
            CountDate = count.CountDate,
            WarehouseId = count.WarehouseId,
            WarehouseCode = count.Warehouse!.Code,
            CountType = count.CountType.ToString(),
            Status = count.Status.ToString(),
            RowVersion = Convert.ToBase64String(count.RowVersion),
            Lines = count.Lines
                .Select(l => new InventoryCountLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    SystemQuantity = l.SystemQuantity,
                    CountedQuantity = l.CountedQuantity,
                    VarianceQuantity = l.VarianceQuantity,
                    UnitId = l.UnitId,
                    UnitCode = l.Unit?.Code,
                    UnitNameAr = l.Unit?.NameAr,
                    UnitFactor = l.UnitFactor,
                    BaseUnitCode = l.Item!.BaseUnitOfMeasure?.Code,
                    SettlementDecision = l.SettlementDecision.ToString(),
                    SettlementReason = l.SettlementReason
                })
                .ToList()
        };
    }
}
