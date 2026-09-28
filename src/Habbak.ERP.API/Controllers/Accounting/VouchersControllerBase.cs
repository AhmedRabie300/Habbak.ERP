using Habbak.ERP.API.Contracts.Accounting;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.CancelVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.CreateVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.PostVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.ReverseVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.UpdateVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Queries.GetVoucherById;
using Habbak.ERP.Application.Accounting.Vouchers.Queries.GetVouchersList;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>
/// Shared implementation for the Receipt Vouchers and Payment Vouchers screens
/// (00-Frontend-Specs.md, section 5, screens 5-6) — same underlying Voucher entity, VoucherType
/// fixed by which concrete route was hit, never by client input.
/// </summary>
[ApiController]
[Authorize]
public abstract class VouchersControllerBase(ISender mediator) : ControllerBase
{
    protected abstract VoucherType FixedVoucherType { get; }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetVouchersListQuery
        {
            VoucherType = FixedVoucherType,
            Search = query.Search,
            Page = query.Page,
            PageSize = query.PageSize,
            SortBy = query.SortBy,
            SortDir = query.SortDir
        }, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var voucher = await mediator.Send(new GetVoucherByIdQuery(id), cancellationToken);

        // A payment voucher's Id doesn't exist from the receipt-vouchers screen's perspective
        // (and vice versa) — same generic 404 either way, no existence leaked across the type boundary.
        if (!string.Equals(voucher.VoucherType, FixedVoucherType.ToString(), StringComparison.Ordinal))
        {
            return NotFound();
        }

        return Ok(voucher);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVoucherRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateVoucherCommand
        {
            VoucherType = FixedVoucherType,
            BranchId = request.BranchId,
            VoucherDate = request.VoucherDate,
            TreasuryAccountId = request.TreasuryAccountId,
            Description = request.Description,
            CounterpartyType = request.CounterpartyType,
            CounterpartyId = request.CounterpartyId,
            DirectAccountId = request.DirectAccountId,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            BaseCurrencyAmount = request.BaseCurrencyAmount,
            RelatedInvoiceId = request.RelatedInvoiceId
        }, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateVoucherRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateVoucherCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            VoucherDate = request.VoucherDate,
            TreasuryAccountId = request.TreasuryAccountId,
            Description = request.Description,
            CounterpartyType = request.CounterpartyType,
            CounterpartyId = request.CounterpartyId,
            DirectAccountId = request.DirectAccountId,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            ExchangeRate = request.ExchangeRate,
            BaseCurrencyAmount = request.BaseCurrencyAmount,
            RelatedInvoiceId = request.RelatedInvoiceId
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new PostVoucherCommand(id), cancellationToken));
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelVoucherCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>"زر عكس" — reverses the voucher's journal entry; the voucher itself stays Posted.</summary>
    [HttpPost("{id:long}/reverse")]
    public async Task<IActionResult> Reverse(long id, CancellationToken cancellationToken)
    {
        var reversalEntryId = await mediator.Send(new ReverseVoucherCommand(id), cancellationToken);
        return Ok(new { reversalJournalEntryId = reversalEntryId });
    }
}
