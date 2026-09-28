using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.DeliveryPlatformOrders.Commands.ReceiveDeliveryPlatformOrder;
using Habbak.ERP.Application.POS.DeliveryPlatformOrders.Queries.GetDeliveryOrdersList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/delivery-orders — شاشة #10 (05-Module-POS-Shifts.md). استقبال الطلب هو المسار الوحيد
/// لإنشاء DeliveryPlatformOrder — بيمثّل نقطة الاستقبال اللي منصة توصيل حقيقية (Talabat/Elmenus)
/// هتنادي عليها كـwebhook، ومُستخدَم حاليًا كإدخال يدوي/محاكاة لعدم توفر تكامل API فعلي.</summary>
[ApiController]
[Authorize]
[Screen("POS_DELIVERY_ORDERS")]
[Route("api/v1/pos/delivery-orders")]
public class DeliveryPlatformOrdersController(ISender mediator) : ControllerBase
{
    public sealed record ItemRequest(long ItemId, decimal Quantity, decimal? UnitPrice);

    public sealed record ReceiveOrderRequest(
        long POSTerminalId,
        string PlatformName,
        string PlatformOrderId,
        string? CustomerName,
        string? CustomerPhone,
        string? DeliveryAddress,
        IReadOnlyList<ItemRequest> Items,
        Guid? IdempotencyKey = null);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long? posTerminalId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDeliveryOrdersListQuery(posTerminalId), cancellationToken));

    [HttpPost("receive")]
    public async Task<IActionResult> Receive([FromBody] ReceiveOrderRequest request, CancellationToken cancellationToken)
    {
        var items = request.Items.Select(i => new DeliveryOrderItemInput(i.ItemId, i.Quantity, i.UnitPrice)).ToList();

        var result = await mediator.Send(new ReceiveDeliveryPlatformOrderCommand(
            request.POSTerminalId,
            request.PlatformName,
            request.PlatformOrderId,
            request.CustomerName,
            request.CustomerPhone,
            request.DeliveryAddress,
            items,
            request.IdempotencyKey), cancellationToken);

        return Ok(new { id = result.Id, checkId = result.CheckId });
    }
}
