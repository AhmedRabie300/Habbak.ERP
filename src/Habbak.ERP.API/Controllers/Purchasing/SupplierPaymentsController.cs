using Habbak.ERP.API.Auth;
using Habbak.ERP.API.Contracts.Purchasing;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.CancelVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.CreateVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.PostVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.ReverseVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.UpdateVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Queries.GetVoucherById;
using Habbak.ERP.Application.Accounting.Vouchers.Queries.GetVouchersList;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Purchasing.SupplierPayments;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>
/// /purchasing/supplier-payments — screen #9 (03-Module-Purchasing.md, section 8). No dedicated
/// SupplierPayment entity: the doc never defines one (section 4 stops at 4.10, "سداد الموردين" is
/// only named in the screens table), so this is a thin Purchasing-flavored wrapper — fixed
/// VoucherType.Payment + CounterpartyType.Supplier — around the Voucher screen already built for
/// 01-Module-Accounting.md section 2.3. PostVoucherCommand/ReverseVoucherCommand (see their own XML
/// docs) apply/un-apply the payment against PurchaseInvoice.AmountPaid.
/// </summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_SUPPLIER_PAYMENTS")]
[Route("api/v1/purchasing/supplier-payments")]
public class SupplierPaymentsController(ISender mediator) : ControllerBase
{
    private const VoucherType FixedVoucherType = VoucherType.Payment;
    private const CounterpartyType FixedCounterpartyType = CounterpartyType.Supplier;

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetVouchersListQuery
        {
            VoucherType = FixedVoucherType,
            CounterpartyType = FixedCounterpartyType,
            Search = query.Search,
            Page = query.Page,
            PageSize = query.PageSize,
            SortBy = query.SortBy,
            SortDir = query.SortDir
        }, cancellationToken);

        return Ok(result);
    }

    /// <summary>How this payment is split across the supplier's invoices (Remarks4, item 7).</summary>
    [HttpGet("{id:long}/allocations")]
    public async Task<IActionResult> GetAllocations(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetSupplierPaymentAllocationsQuery(id), cancellationToken));

    /// <summary>The oldest-first split the screen proposes for an amount, before the user edits it.</summary>
    [HttpGet("allocation-proposal")]
    public async Task<IActionResult> ProposeAllocation(
        [FromQuery] long supplierId, [FromQuery] decimal amount, [FromQuery] string currencyCode, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ProposeSupplierPaymentAllocationQuery(supplierId, amount, currencyCode), cancellationToken));

    /// <summary>Replaces the whole split of a Draft payment; the sum must equal the payment.</summary>
    [HttpPut("{id:long}/allocations")]
    public async Task<IActionResult> SetAllocations(long id, [FromBody] SetAllocationsRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new AllocateSupplierPaymentCommand(id, request.Allocations), cancellationToken);
        return NoContent();
    }

    public sealed record SetAllocationsRequest(IReadOnlyList<SupplierPaymentAllocationInput> Allocations);

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var voucher = await mediator.Send(new GetVoucherByIdQuery(id), cancellationToken);

        if (!string.Equals(voucher.VoucherType, FixedVoucherType.ToString(), StringComparison.Ordinal)
            || !string.Equals(voucher.CounterpartyType, FixedCounterpartyType.ToString(), StringComparison.Ordinal))
        {
            return NotFound();
        }

        return Ok(voucher);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierPaymentRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateVoucherCommand
        {
            VoucherType = FixedVoucherType,
            BranchId = request.BranchId,
            VoucherDate = request.VoucherDate,
            TreasuryAccountId = request.TreasuryAccountId,
            Description = request.Description,
            CounterpartyType = FixedCounterpartyType,
            CounterpartyId = request.SupplierId,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            BaseCurrencyAmount = request.BaseCurrencyAmount,
            RelatedInvoiceId = request.PurchaseInvoiceId
        }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSupplierPaymentRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateVoucherCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            VoucherDate = request.VoucherDate,
            TreasuryAccountId = request.TreasuryAccountId,
            Description = request.Description,
            CounterpartyType = FixedCounterpartyType,
            CounterpartyId = request.SupplierId,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            BaseCurrencyAmount = request.BaseCurrencyAmount,
            RelatedInvoiceId = request.PurchaseInvoiceId
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new PostVoucherCommand(id), cancellationToken));

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelVoucherCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reverse")]
    public async Task<IActionResult> Reverse(long id, CancellationToken cancellationToken)
    {
        var reversalEntryId = await mediator.Send(new ReverseVoucherCommand(id), cancellationToken);
        return Ok(new { reversalJournalEntryId = reversalEntryId });
    }
}
