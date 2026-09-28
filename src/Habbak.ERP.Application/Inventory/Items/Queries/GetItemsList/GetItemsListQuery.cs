using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Items.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Items.Queries.GetItemsList;

public sealed record GetItemsListQuery : IRequest<IReadOnlyList<ItemListItemDto>>;

public sealed class GetItemsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetItemsListQuery, IReadOnlyList<ItemListItemDto>>
{
    public async Task<IReadOnlyList<ItemListItemDto>> Handle(GetItemsListQuery request, CancellationToken cancellationToken)
    {
        var items = await db.Items
            .AsNoTracking()
            .OrderBy(i => i.Code)
            .Select(i => new
            {
                i.Id, i.Code, i.NameAr, i.NameEn, i.ItemType, i.BaseUnitOfMeasureId,
                BaseUnitCode = i.BaseUnitOfMeasure.Code,
                BaseUnitNameAr = i.BaseUnitOfMeasure.NameAr,
                Conversions = i.UnitConversions
                    .Select(c => new ItemUnitOptionDto(c.AlternateUnitOfMeasureId, c.AlternateUnitOfMeasure.Code, c.AlternateUnitOfMeasure.NameAr, c.ConversionFactor))
                    .ToList(),
                i.TaxCode, i.Status, i.IsActive, i.POSCategoryId,
                POSCategoryNameAr = i.POSCategory != null ? i.POSCategory.NameAr : null,
                i.DefaultPrice, i.IsSellable, i.Barcode
            })
            .ToListAsync(cancellationToken);

        return items.Select(i => new ItemListItemDto
            {
                Id = i.Id,
                Code = i.Code,
                NameAr = i.NameAr,
                NameEn = i.NameEn,
                ItemType = i.ItemType.ToString(),
                BaseUnitOfMeasureCode = i.BaseUnitCode,
                BaseUnitOfMeasureId = i.BaseUnitOfMeasureId,
                // The units a line of this item may be in: base unit first, then its conversions.
                Units = [new ItemUnitOptionDto(i.BaseUnitOfMeasureId, i.BaseUnitCode, i.BaseUnitNameAr, 1m), .. i.Conversions.OrderBy(c => c.Factor)],
                TaxCode = i.TaxCode,
                Status = i.Status.ToString(),
                IsActive = i.IsActive,
                POSCategoryId = i.POSCategoryId,
                POSCategoryNameAr = i.POSCategoryNameAr,
                DefaultPrice = i.DefaultPrice,
                IsSellable = i.IsSellable,
                Barcode = i.Barcode
            })
            .ToList();
    }
}
