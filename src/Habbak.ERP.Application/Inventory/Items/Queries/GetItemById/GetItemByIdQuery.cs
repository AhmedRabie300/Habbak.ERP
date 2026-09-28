using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Items.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Items.Queries.GetItemById;

public sealed record GetItemByIdQuery(long Id) : IRequest<ItemDetailDto>;

public sealed class GetItemByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetItemByIdQuery, ItemDetailDto>
{
    public async Task<ItemDetailDto> Handle(GetItemByIdQuery request, CancellationToken cancellationToken)
    {
        var item = await db.Items
            .AsNoTracking()
            .Include(i => i.UnitConversions).ThenInclude(c => c.AlternateUnitOfMeasure)
            .Include(i => i.WarehouseSettings).ThenInclude(w => w.Warehouse)
            .Include(i => i.BranchItemLimits)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Item), request.Id);

        return new ItemDetailDto
        {
            Id = item.Id,
            Code = item.Code,
            NameAr = item.NameAr,
            NameEn = item.NameEn,
            ItemGroupId = item.ItemGroupId,
            POSCategoryId = item.POSCategoryId,
            ItemType = item.ItemType.ToString(),
            Barcode = item.Barcode,
            TaxCode = item.TaxCode,
            BaseUnitOfMeasureId = item.BaseUnitOfMeasureId,
            PurchaseUnitOfMeasureId = item.PurchaseUnitOfMeasureId,
            SellUnitOfMeasureId = item.SellUnitOfMeasureId,
            SaleMethod = item.SaleMethod.ToString(),
            CostMethod = item.CostMethod.ToString(),
            DefaultPrice = item.DefaultPrice,
            IsStocked = item.IsStocked,
            IsTracked = item.IsTracked,
            TrackSerial = item.TrackSerial,
            ShelfLifeDays = item.ShelfLifeDays,
            StandardCost = item.StandardCost,
            IsPurchasable = item.IsPurchasable,
            IsSellable = item.IsSellable,
            IsManufacturable = item.IsManufacturable,
            AllowSubstitutes = item.AllowSubstitutes,
            Status = item.Status.ToString(),
            IsActive = item.IsActive,
            UnitConversions = item.UnitConversions.Select(c => new ItemUnitConversionDto
            {
                Id = c.Id,
                AlternateUnitOfMeasureId = c.AlternateUnitOfMeasureId,
                AlternateUnitOfMeasureCode = c.AlternateUnitOfMeasure.Code,
                ConversionFactor = c.ConversionFactor
            }).ToList(),
            WarehouseSettings = item.WarehouseSettings.Select(w => new ItemWarehouseSettingsDto
            {
                Id = w.Id,
                WarehouseId = w.WarehouseId,
                WarehouseCode = w.Warehouse.Code,
                MinStockLevel = w.MinStockLevel,
                MaxStockLevel = w.MaxStockLevel,
                ReorderPoint = w.ReorderPoint
            }).ToList(),
            BranchItemLimits = item.BranchItemLimits.Select(l => new BranchItemLimitDto
            {
                Id = l.Id,
                BranchId = l.BranchId,
                MinRequestQuantity = l.MinRequestQuantity,
                MaxRequestQuantity = l.MaxRequestQuantity
            }).ToList()
        };
    }
}
