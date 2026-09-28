using Habbak.ERP.API.Auth;
using Habbak.ERP.API.Contracts;
using Habbak.ERP.Application.Purchasing.GoodsReceipts.Commands.CancelGoodsReceipt;
using Habbak.ERP.Application.Purchasing.GoodsReceipts.Commands.CreateGoodsReceipt;
using Habbak.ERP.Application.Purchasing.GoodsReceipts.Commands.PostGoodsReceipt;
using Habbak.ERP.Application.Purchasing.GoodsReceipts.Dtos;
using Habbak.ERP.Application.Purchasing.GoodsReceipts.Queries.GetGoodsReceiptById;
using Habbak.ERP.Application.Purchasing.GoodsReceipts.Queries.GetGoodsReceiptsList;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Queries.GetPostablePurchaseOrdersList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/goods-receipts — screen #6 (03-Module-Purchasing.md, section 8).</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_GOODS_RECEIPTS")]
[Route("api/v1/purchasing/goods-receipts")]
public class GoodsReceiptsController(ISender mediator) : ControllerBase
{
    public sealed record CreateGoodsReceiptRequest(
        long? BranchId, long WarehouseId, DateOnly ReceiptDate, long? PurchaseOrderId, long? PurchaseInvoiceId, string? Notes,
        IReadOnlyList<GoodsReceiptLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetGoodsReceiptsListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    /// <summary>Feeds the create form's "receive against order" picker.</summary>
    [HttpGet("postable-orders")]
    public async Task<IActionResult> GetPostableOrders(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPostablePurchaseOrdersListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetGoodsReceiptByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGoodsReceiptRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateGoodsReceiptCommand
        {
            BranchId = request.BranchId,
            WarehouseId = request.WarehouseId,
            ReceiptDate = request.ReceiptDate,
            PurchaseOrderId = request.PurchaseOrderId,
            PurchaseInvoiceId = request.PurchaseInvoiceId,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new PostGoodsReceiptCommand(id, IdempotencyHeader.Read(this)), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelGoodsReceiptCommand(id), cancellationToken);
        return NoContent();
    }
}
