using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Inventory.Items.Commands.CreateItem;
using Habbak.ERP.Application.Inventory.Items.Commands.DeleteItem;
using Habbak.ERP.Application.Inventory.Items.Commands.UpdateItem;
using Habbak.ERP.Application.Inventory.Items.Dtos;
using Habbak.ERP.Application.Inventory.Items.Queries.GetItemById;
using Habbak.ERP.Application.Inventory.Items.Queries.GetItemsList;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/items (02-Module-Inventory-Manufacturing.md, section 2.1).</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_ITEMS", LookupReads = true)]
[Route("api/v1/inventory/items")]
public class ItemsController(ISender mediator) : ControllerBase
{
    public sealed record CreateItemRequest(
        string? Code, string NameAr, string NameEn, long? ItemGroupId, long? POSCategoryId,
        ItemType ItemType, string? Barcode, long BaseUnitOfMeasureId, long? PurchaseUnitOfMeasureId,
        long? SellUnitOfMeasureId, SaleMethod SaleMethod, CostMethod CostMethod, decimal? DefaultPrice,
        bool IsStocked, bool IsTracked, bool TrackSerial, int? ShelfLifeDays, decimal? StandardCost,
        bool IsPurchasable, bool IsSellable, bool IsManufacturable, bool AllowSubstitutes, ItemStatus Status, string? TaxCode = null);

    public sealed record UpdateItemRequest(
        string NameAr, string NameEn, long? ItemGroupId, long? POSCategoryId,
        ItemType ItemType, string? Barcode, long BaseUnitOfMeasureId, long? PurchaseUnitOfMeasureId,
        long? SellUnitOfMeasureId, SaleMethod SaleMethod, CostMethod CostMethod, decimal? DefaultPrice,
        bool IsStocked, bool IsTracked, bool TrackSerial, int? ShelfLifeDays, decimal? StandardCost,
        bool IsPurchasable, bool IsSellable, bool IsManufacturable, bool AllowSubstitutes, ItemStatus Status,
        IReadOnlyList<ItemUnitConversionInput> UnitConversions,
        IReadOnlyList<ItemWarehouseSettingsInput> WarehouseSettings,
        IReadOnlyList<BranchItemLimitInput> BranchItemLimits, string? TaxCode = null);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetItemsListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetItemByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateItemRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateItemCommand(
            request.Code, request.NameAr, request.NameEn, request.ItemGroupId, request.POSCategoryId,
            request.ItemType, request.Barcode, request.BaseUnitOfMeasureId, request.PurchaseUnitOfMeasureId,
            request.SellUnitOfMeasureId, request.SaleMethod, request.CostMethod, request.DefaultPrice,
            request.IsStocked, request.IsTracked, request.TrackSerial, request.ShelfLifeDays, request.StandardCost,
            request.IsPurchasable, request.IsSellable, request.IsManufacturable, request.AllowSubstitutes,
            request.Status, request.TaxCode), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateItemRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateItemCommand(
            id, request.NameAr, request.NameEn, request.ItemGroupId, request.POSCategoryId,
            request.ItemType, request.Barcode, request.BaseUnitOfMeasureId, request.PurchaseUnitOfMeasureId,
            request.SellUnitOfMeasureId, request.SaleMethod, request.CostMethod, request.DefaultPrice,
            request.IsStocked, request.IsTracked, request.TrackSerial, request.ShelfLifeDays, request.StandardCost,
            request.IsPurchasable, request.IsSellable, request.IsManufacturable, request.AllowSubstitutes,
            request.Status, request.UnitConversions, request.WarehouseSettings, request.BranchItemLimits, request.TaxCode), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteItemCommand(id), cancellationToken);
        return NoContent();
    }
}
