using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.Checks.Commands.AddCheckLine;
using Habbak.ERP.Application.POS.Checks.Commands.ApplyManualDiscount;
using Habbak.ERP.Application.POS.Checks.Commands.CancelCheck;
using Habbak.ERP.Application.POS.Checks.Commands.ChangeCheckOrderType;
using Habbak.ERP.Application.POS.Checks.Commands.FireCheckLinesToKitchen;
using Habbak.ERP.Application.POS.Checks.Commands.HoldCheck;
using Habbak.ERP.Application.POS.Checks.Commands.MergeChecks;
using Habbak.ERP.Application.POS.Checks.Commands.OpenCheckForTable;
using Habbak.ERP.Application.POS.Checks.Commands.OpenCheckStandalone;
using Habbak.ERP.Application.POS.Checks.Commands.RemoveCheckLine;
using Habbak.ERP.Application.POS.Checks.Commands.RemoveManualDiscount;
using Habbak.ERP.Application.POS.Checks.Commands.ResumeCheck;
using Habbak.ERP.Application.POS.Checks.Commands.SetCheckCustomer;
using Habbak.ERP.Application.POS.Checks.Commands.SetLoyaltyPointsRedemption;
using Habbak.ERP.Application.POS.Checks.Commands.UpdateCheckLine;
using Habbak.ERP.Application.POS.Checks.Queries.GetCheckById;
using Habbak.ERP.Application.POS.Checks.Queries.GetChecksList;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/checks — الشاشة الرئيسية للبيع وشاشات #7/#9 (05-Module-POS-Shifts.md، section
/// 2.2). الدفع/الترحيل (POSPayment/POSInvoice) مرحلة لاحقة — Check بيوصل هنا لحد Open/Held/Cancelled
/// /Merged بس.</summary>
[ApiController]
[Authorize]
[Screen("POS_TABLE_BOARD", "POS_CHECKS_OPEN", "POS_CHECKS_HELD")]
[MaskFields("POSPayment")]
[Route("api/v1/pos/checks")]
public class ChecksController(ISender mediator) : ControllerBase
{
    public sealed record OpenForTableRequest(long POSTerminalId, long TableId);
    public sealed record OpenStandaloneRequest(long POSTerminalId, CheckOrderType OrderType, long? CustomerId);
    public sealed record ChangeOrderTypeRequest(CheckOrderType OrderType);
    public sealed record MergeRequest(long TargetCheckId, IReadOnlyList<long> SourceCheckIds);
    public sealed record AddLineRequest(long ItemId, decimal Quantity, decimal? UnitPrice, decimal DiscountAmount, string? Note);
    public sealed record UpdateLineRequest(decimal Quantity, decimal UnitPrice, decimal DiscountAmount, bool IsPriceManuallyOverridden, string? Note);
    public sealed record RemoveLineRequest(string? VoidReason);
    public sealed record ApplyDiscountRequest(POSManualDiscountType Type, decimal Value, string Reason);
    public sealed record SetCustomerRequest(long? CustomerId);
    public sealed record SetLoyaltyRedemptionRequest(decimal Points);

    [HttpGet("open")]
    public async Task<IActionResult> GetOpen([FromQuery] long? posTerminalId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetChecksListQuery(posTerminalId, CheckStatus.Open), cancellationToken));

    [HttpGet("held")]
    public async Task<IActionResult> GetHeld([FromQuery] long? posTerminalId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetChecksListQuery(posTerminalId, CheckStatus.Held), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCheckByIdQuery(id), cancellationToken));

    [HttpPost("open-for-table")]
    public async Task<IActionResult> OpenForTable([FromBody] OpenForTableRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new OpenCheckForTableCommand(request.POSTerminalId, request.TableId), cancellationToken);
        return Ok(new { id });
    }

    [HttpPost("open-standalone")]
    public async Task<IActionResult> OpenStandalone([FromBody] OpenStandaloneRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new OpenCheckStandaloneCommand(request.POSTerminalId, request.OrderType, request.CustomerId), cancellationToken);
        return Ok(new { id });
    }

    [ScreenButton("POS_TABLE_BOARD", "Hold")]
    [HttpPost("{id:long}/hold")]
    public async Task<IActionResult> Hold(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new HoldCheckCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("POS_TABLE_BOARD", "Hold")]
    [HttpPost("{id:long}/resume")]
    public async Task<IActionResult> Resume(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ResumeCheckCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("POS_TABLE_BOARD", "Cancel")]
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelCheckCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("POS_TABLE_BOARD", "ApplyManualDiscount")]
    [HttpPost("{id:long}/apply-discount")]
    public async Task<IActionResult> ApplyDiscount(long id, [FromBody] ApplyDiscountRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ApplyManualDiscountCommand(id, request.Type, request.Value, request.Reason), cancellationToken);
        return NoContent();
    }

    [ScreenButton("POS_TABLE_BOARD", "ApplyManualDiscount")]
    [HttpPost("{id:long}/remove-discount")]
    public async Task<IActionResult> RemoveDiscount(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveManualDiscountCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/customer")]
    public async Task<IActionResult> SetCustomer(long id, [FromBody] SetCustomerRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetCheckCustomerCommand(id, request.CustomerId), cancellationToken);
        return NoContent();
    }

    [ScreenButton("POS_TABLE_BOARD", "RedeemLoyalty")]
    [HttpPost("{id:long}/loyalty-redemption")]
    public async Task<IActionResult> SetLoyaltyRedemption(long id, [FromBody] SetLoyaltyRedemptionRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetLoyaltyPointsRedemptionCommand(id, request.Points), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/order-type")]
    public async Task<IActionResult> ChangeOrderType(long id, [FromBody] ChangeOrderTypeRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ChangeCheckOrderTypeCommand(id, request.OrderType), cancellationToken);
        return NoContent();
    }

    [ScreenButton("POS_CHECKS_OPEN", "Merge")]
    [ScreenButton("POS_CHECKS_HELD", "Merge")]
    [HttpPost("merge")]
    public async Task<IActionResult> Merge([FromBody] MergeRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new MergeChecksCommand(request.TargetCheckId, request.SourceCheckIds), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/lines")]
    public async Task<IActionResult> AddLine(long id, [FromBody] AddLineRequest request, CancellationToken cancellationToken)
    {
        var lineId = await mediator.Send(new AddCheckLineCommand(id, request.ItemId, request.Quantity, request.UnitPrice, request.DiscountAmount, request.Note), cancellationToken);
        return Ok(new { id = lineId });
    }

    [HttpPut("{id:long}/lines/{lineId:long}")]
    public async Task<IActionResult> UpdateLine(long id, long lineId, [FromBody] UpdateLineRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateCheckLineCommand(id, lineId, request.Quantity, request.UnitPrice, request.DiscountAmount, request.IsPriceManuallyOverridden, request.Note), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}/lines/{lineId:long}")]
    public async Task<IActionResult> RemoveLine(long id, long lineId, [FromBody] RemoveLineRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveCheckLineCommand(id, lineId, request.VoidReason), cancellationToken);
        return NoContent();
    }

    [ScreenButton("POS_TABLE_BOARD", "FireToKitchen")]
    [HttpPost("{id:long}/fire-to-kitchen")]
    public async Task<IActionResult> FireToKitchen(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new FireCheckLinesToKitchenCommand(id), cancellationToken);
        return NoContent();
    }
}
