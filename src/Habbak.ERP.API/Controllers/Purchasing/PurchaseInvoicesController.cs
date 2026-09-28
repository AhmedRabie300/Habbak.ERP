using Habbak.ERP.API.Auth;
using Habbak.ERP.API.Contracts;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Commands.CancelPurchaseInvoice;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Commands.CreatePurchaseInvoice;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Commands.PostPurchaseInvoice;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Commands.RejectPurchaseInvoice;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Commands.SubmitPurchaseInvoice;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Commands.UpdatePurchaseInvoice;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Dtos;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Queries.GetPayableInvoicesList;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Queries.GetPurchaseInvoiceById;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Queries.GetPurchaseInvoicesList;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Queries.GetPurchaseInvoiceSourceOrder;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/purchase-invoices — screen #5 (03-Module-Purchasing.md, section 8).</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_PURCHASE_INVOICES")]
[Route("api/v1/purchasing/purchase-invoices")]
public class PurchaseInvoicesController(ISender mediator) : ControllerBase
{
    public sealed record CreatePurchaseInvoiceRequest(
        long? BranchId, DateOnly InvoiceDate, DateOnly DueDate, long SupplierId, string? SupplierInvoiceNumber,
        long? PurchaseOrderId, long? GoodsReceiptId, long? WarehouseId, string CurrencyCode, decimal ExchangeRate, SupplierPaymentTerms? PaymentTerms,
        decimal TaxAmount, decimal? DiscountAmount, string? DiscountReason, decimal AdditionalCosts,
        CostAllocationMethod? AdditionalCostAllocationMethod, decimal? CommissionRate, decimal? CommissionAmount,
        long? CommissionAccountId, string? Notes, IReadOnlyList<PurchaseInvoiceLineInput> Lines);

    public sealed record UpdatePurchaseInvoiceRequest(
        string RowVersion, long? BranchId, DateOnly InvoiceDate, DateOnly DueDate, long SupplierId, string? SupplierInvoiceNumber,
        long? PurchaseOrderId, long? GoodsReceiptId, string CurrencyCode, decimal ExchangeRate, SupplierPaymentTerms? PaymentTerms,
        decimal TaxAmount, decimal? DiscountAmount, string? DiscountReason, decimal AdditionalCosts,
        CostAllocationMethod? AdditionalCostAllocationMethod, decimal? CommissionRate, decimal? CommissionAmount,
        long? CommissionAccountId, string? Notes, IReadOnlyList<PurchaseInvoiceLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetPurchaseInvoicesListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseInvoiceByIdQuery(id), cancellationToken));

    /// <summary>The order behind this invoice: ordered vs received vs invoiced, per line (Remarks4, item 5).</summary>
    [HttpGet("{id:long}/source-order")]
    public async Task<IActionResult> GetSourceOrder(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseInvoiceSourceOrderQuery(id), cancellationToken));

    [HttpGet("payable")]
    public async Task<IActionResult> GetPayable([FromQuery] long supplierId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPayableInvoicesListQuery(supplierId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseInvoiceRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePurchaseInvoiceCommand
        {
            BranchId = request.BranchId,
            InvoiceDate = request.InvoiceDate,
            DueDate = request.DueDate,
            SupplierId = request.SupplierId,
            SupplierInvoiceNumber = request.SupplierInvoiceNumber,
            PurchaseOrderId = request.PurchaseOrderId,
            GoodsReceiptId = request.GoodsReceiptId,
            WarehouseId = request.WarehouseId,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            PaymentTerms = request.PaymentTerms,
            TaxAmount = request.TaxAmount,
            DiscountAmount = request.DiscountAmount,
            DiscountReason = request.DiscountReason,
            AdditionalCosts = request.AdditionalCosts,
            AdditionalCostAllocationMethod = request.AdditionalCostAllocationMethod,
            CommissionRate = request.CommissionRate,
            CommissionAmount = request.CommissionAmount,
            CommissionAccountId = request.CommissionAccountId,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdatePurchaseInvoiceRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePurchaseInvoiceCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            InvoiceDate = request.InvoiceDate,
            DueDate = request.DueDate,
            SupplierId = request.SupplierId,
            SupplierInvoiceNumber = request.SupplierInvoiceNumber,
            PurchaseOrderId = request.PurchaseOrderId,
            GoodsReceiptId = request.GoodsReceiptId,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            PaymentTerms = request.PaymentTerms,
            TaxAmount = request.TaxAmount,
            DiscountAmount = request.DiscountAmount,
            DiscountReason = request.DiscountReason,
            AdditionalCosts = request.AdditionalCosts,
            AdditionalCostAllocationMethod = request.AdditionalCostAllocationMethod,
            CommissionRate = request.CommissionRate,
            CommissionAmount = request.CommissionAmount,
            CommissionAccountId = request.CommissionAccountId,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [ScreenButton("PURCHASING_PURCHASE_INVOICES", "Submit")]
    [HttpPost("{id:long}/submit")]
    public async Task<IActionResult> Submit(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new SubmitPurchaseInvoiceCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("PURCHASING_PURCHASE_INVOICES", "Post")]
    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new PostPurchaseInvoiceCommand(id, IdempotencyHeader.Read(this)), cancellationToken);
        return NoContent();
    }

    [ScreenButton("PURCHASING_PURCHASE_INVOICES", "Reject")]
    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectPurchaseInvoiceCommand(id), cancellationToken);
        return NoContent();
    }

    [ScreenButton("PURCHASING_PURCHASE_INVOICES", "Cancel")]
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelPurchaseInvoiceCommand(id, IdempotencyHeader.Read(this)), cancellationToken);
        return NoContent();
    }
}
