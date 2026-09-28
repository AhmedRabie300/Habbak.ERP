using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Inventory.Settings.Commands.CreateProductionSalesModeSetting;
using Habbak.ERP.Application.Inventory.Settings.Commands.DeleteProductionSalesModeSetting;
using Habbak.ERP.Application.Inventory.Settings.Commands.UpdateInventorySettings;
using Habbak.ERP.Application.Inventory.Settings.Commands.UpdateProductionSalesModeSetting;
using Habbak.ERP.Application.Inventory.Settings.Commands.UpdateShortagePolicy;
using Habbak.ERP.Application.Inventory.Settings.Queries.GetInventorySettings;
using Habbak.ERP.Application.Inventory.Settings.Queries.GetProductionSalesModeSettingsList;
using Habbak.ERP.Application.Inventory.Settings.Queries.GetShortagePolicy;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/settings/* — screens #21-23 (02-Module-Inventory-Manufacturing.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_SETTINGS_SHORTAGE_POLICY", "INVENTORY_SETTINGS_GENERAL", "INVENTORY_SETTINGS_SALES_MODE", LookupReads = true)]
[Route("api/v1/inventory/settings")]
public class InventorySettingsController(ISender mediator) : ControllerBase
{
    public sealed record UpdateShortagePolicyRequest(bool AllowOverrideOnShortage, bool RequiresApprovalForOverride);
    public sealed record UpdateInventorySettingsRequest(int SlowMovingThresholdDays);
    public sealed record CreateProductionSalesModeSettingRequest(SettingScopeType ScopeType, long? ScopeId, ProductionSalesMode Mode);
    public sealed record UpdateProductionSalesModeSettingRequest(ProductionSalesMode Mode);

    [HttpGet("shortage-policy")]
    public async Task<IActionResult> GetShortagePolicy(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetShortagePolicyQuery(), cancellationToken));

    [HttpPut("shortage-policy")]
    public async Task<IActionResult> UpdateShortagePolicy([FromBody] UpdateShortagePolicyRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateShortagePolicyCommand(request.AllowOverrideOnShortage, request.RequiresApprovalForOverride), cancellationToken);
        return NoContent();
    }

    [HttpGet("inventory-settings")]
    public async Task<IActionResult> GetInventorySettings(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetInventorySettingsQuery(), cancellationToken));

    [HttpPut("inventory-settings")]
    public async Task<IActionResult> UpdateInventorySettings([FromBody] UpdateInventorySettingsRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateInventorySettingsCommand(request.SlowMovingThresholdDays), cancellationToken);
        return NoContent();
    }

    [HttpGet("production-sales-mode")]
    public async Task<IActionResult> GetProductionSalesModeSettings(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetProductionSalesModeSettingsListQuery(), cancellationToken));

    [HttpPost("production-sales-mode")]
    public async Task<IActionResult> CreateProductionSalesModeSetting([FromBody] CreateProductionSalesModeSettingRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateProductionSalesModeSettingCommand
        {
            ScopeType = request.ScopeType,
            ScopeId = request.ScopeId,
            Mode = request.Mode
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("production-sales-mode/{id:long}")]
    public async Task<IActionResult> UpdateProductionSalesModeSetting(long id, [FromBody] UpdateProductionSalesModeSettingRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateProductionSalesModeSettingCommand(id, request.Mode), cancellationToken);
        return NoContent();
    }

    [HttpDelete("production-sales-mode/{id:long}")]
    public async Task<IActionResult> DeleteProductionSalesModeSetting(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteProductionSalesModeSettingCommand(id), cancellationToken);
        return NoContent();
    }
}
