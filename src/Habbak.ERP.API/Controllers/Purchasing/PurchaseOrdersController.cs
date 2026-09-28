using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.CancelPurchaseOrder;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.ConfirmPurchaseOrder;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.RejectPurchaseOrder;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.SendPurchaseOrder;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.UpdatePurchaseOrder;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Dtos;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Queries.GetOpenPurchaseOrdersBySupplier;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderById;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderLinesForInvoice;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrderSourceRequest;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Queries.GetPurchaseOrdersList;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/purchase-orders — screen #4 (03-Module-Purchasing.md, section 8).</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_PURCHASE_ORDERS")]
[Route("api/v1/purchasing/purchase-orders")]
public class PurchaseOrdersController(ISender mediator) : ControllerBase
{
    public sealed record CreatePurchaseOrderRequest(
        long? BranchId, DateOnly OrderDate, long SupplierId, long? PurchaseRequestId, long? RFQId, string CurrencyCode, decimal ExchangeRate,
        SupplierPaymentTerms? PaymentTerms, PurchaseOrderDeliveryTerms? DeliveryTerms, DateOnly? ExpectedDeliveryDate,
        string? DeliveryAddress, decimal TaxAmount, decimal? DiscountAmount, string? DiscountReason, string? Notes,
        IReadOnlyList<PurchaseOrderLineInput> Lines);

    public sealed record UpdatePurchaseOrderRequest(
        string RowVersion, long? BranchId, DateOnly OrderDate, long SupplierId, string CurrencyCode, decimal ExchangeRate,
        SupplierPaymentTerms PaymentTerms, PurchaseOrderDeliveryTerms? DeliveryTerms, DateOnly? ExpectedDeliveryDate,
        string? DeliveryAddress, decimal TaxAmount, decimal? DiscountAmount, string? DiscountReason, string? Notes,
        IReadOnlyList<PurchaseOrderLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetPurchaseOrdersListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseOrderByIdQuery(id), cancellationToken));

    /// <summary>The supplier's orders that still have something left to bill (Remarks6).</summary>
    [HttpGet("open-for-invoice")]
    public async Task<IActionResult> GetOpenForInvoice([FromQuery] long supplierId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetOpenPurchaseOrdersBySupplierQuery(supplierId), cancellationToken));

    /// <summary>This order's lines with what is still billable on each — what the invoice screen loads (Remarks6).</summary>
    [HttpGet("{id:long}/invoice-lines")]
    public async Task<IActionResult> GetInvoiceLines(long id, [FromQuery] long? excludeInvoiceId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseOrderLinesForInvoiceQuery(id, excludeInvoiceId), cancellationToken));

    /// <summary>What the purchase request behind this order asked for, and how much of it is covered (Remarks4, item 4).</summary>
    [HttpGet("{id:long}/source-request")]
    public async Task<IActionResult> GetSourceRequest(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseOrderSourceRequestQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePurchaseOrderCommand
        {
            BranchId = request.BranchId,
            OrderDate = request.OrderDate,
            SupplierId = request.SupplierId,
            PurchaseRequestId = request.PurchaseRequestId,
            RFQId = request.RFQId,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            PaymentTerms = request.PaymentTerms,
            DeliveryTerms = request.DeliveryTerms,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            DeliveryAddress = request.DeliveryAddress,
            TaxAmount = request.TaxAmount,
            DiscountAmount = request.DiscountAmount,
            DiscountReason = request.DiscountReason,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePurchaseOrderCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            OrderDate = request.OrderDate,
            SupplierId = request.SupplierId,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            PaymentTerms = request.PaymentTerms,
            DeliveryTerms = request.DeliveryTerms,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            DeliveryAddress = request.DeliveryAddress,
            TaxAmount = request.TaxAmount,
            DiscountAmount = request.DiscountAmount,
            DiscountReason = request.DiscountReason,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [ScreenButton("PURCHASING_PURCHASE_ORDERS", "Send")]
    [HttpPost("{id:long}/send")]
    public async Task<IActionResult> Send(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SendPurchaseOrderCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("PURCHASING_PURCHASE_ORDERS", "Confirm")]
    [HttpPost("{id:long}/confirm")]
    public async Task<IActionResult> Confirm(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ConfirmPurchaseOrderCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("PURCHASING_PURCHASE_ORDERS", "Reject")]
    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectPurchaseOrderCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("PURCHASING_PURCHASE_ORDERS", "Cancel")]
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelPurchaseOrderCommand(id), cancellationToken);
        return NoContent();
    }
}
