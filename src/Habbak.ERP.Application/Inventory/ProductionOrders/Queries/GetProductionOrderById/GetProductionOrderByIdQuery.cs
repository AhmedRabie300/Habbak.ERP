using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Inventory.ProductionOrders.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.ProductionOrders.Queries.GetProductionOrderById;

public sealed record GetProductionOrderByIdQuery(long Id) : IRequest<ProductionOrderDetailDto>;

public sealed class GetProductionOrderByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetProductionOrderByIdQuery, ProductionOrderDetailDto>
{
    public async Task<ProductionOrderDetailDto> Handle(GetProductionOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await db.ProductionOrders
            .AsNoTracking()
            .Include(o => o.Warehouse)
            .Include(o => o.Recipe).ThenInclude(r => r!.OutputItem)
            .Include(o => o.Recipe).ThenInclude(r => r!.Lines).ThenInclude(l => l.ComponentItem)
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductionOrder), request.Id);

        var scalingFactor = order.PlannedQuantity / order.Recipe!.OutputQuantity;

        return new ProductionOrderDetailDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            WarehouseId = order.WarehouseId,
            WarehouseCode = order.Warehouse!.Code,
            RecipeId = order.RecipeId,
            RecipeFamilyCode = order.Recipe!.RecipeFamilyCode,
            RecipeVersionNumber = order.Recipe!.VersionNumber,
            OutputItemId = order.Recipe!.OutputItemId,
            OutputItemCode = order.Recipe!.OutputItem!.Code,
            OutputItemNameAr = order.Recipe!.OutputItem!.NameAr,
            RecipeOutputQuantity = order.Recipe!.OutputQuantity,
            PlannedQuantity = order.PlannedQuantity,
            ActualQuantity = order.ActualQuantity,
            Status = order.Status.ToString(),
            StartDate = order.StartDate,
            EndDate = order.EndDate,
            ExecutedByUserId = order.ExecutedByUserId,
            StandardCost = order.StandardCost,
            ActualCost = order.ActualCost,
            ProductionIssueDocumentId = order.ProductionIssueDocumentId,
            ProductionReceiptDocumentId = order.ProductionReceiptDocumentId,
            RowVersion = Convert.ToBase64String(order.RowVersion),
            ComponentsPreview = order.Recipe!.Lines
                .Select(l => new ProductionOrderComponentPreviewDto
                {
                    ComponentItemId = l.ComponentItemId,
                    ComponentItemCode = l.ComponentItem!.Code,
                    ComponentItemNameAr = l.ComponentItem!.NameAr,
                    // In the component's base unit.
                    PlannedConsumption = ItemUnits.ToBase(l.Quantity, l.UnitFactor) * scalingFactor
                })
                .ToList()
        };
    }
}
