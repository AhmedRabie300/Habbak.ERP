using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Purchasing.Settings;
using Habbak.ERP.Application.Purchasing.Settings.Commands.UpdatePurchaseCycleSettings;
using Habbak.ERP.Application.Purchasing.Settings.Queries.GetPurchaseCycleSettings;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/settings/* — screen #11 (03-Module-Purchasing.md, section 8).</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_CYCLE_SETTINGS", LookupReads = true)]
[Route("api/v1/purchasing/settings")]
public class PurchasingSettingsController(ISender mediator) : ControllerBase
{
    public sealed record UpdatePurchaseCycleSettingsRequest(
        PurchaseCycleType CycleType, bool RequiresPurchaseRequest, bool RequiresQuotation, bool RequiresPurchaseOrder,
        bool RequiresGoodsReceipt, bool AllowInvoiceWithoutOrder, bool AllowReceiptWithoutInvoice,
        bool AutoCreateReceiptOnInvoicePost, bool AutoCreateInvoiceOnReceipt, bool RequiresApprovalForPurchaseOrder,
        bool RequiresApprovalForInvoice, SupplierPaymentTerms DefaultPaymentTerms, bool CapitalizeAdditionalCosts,
        bool AllowManualInvoiceLines = true);

    /// <summary>
    /// What each named cycle requires (Remarks4, item 6) — the settings screen applies the matching
    /// preset when the user picks a type, which is also what the update command validates against.
    /// </summary>
    [HttpGet("purchase-cycle/presets")]
    public IActionResult GetPurchaseCyclePresets() => Ok(PurchaseCyclePresets.All);

    [HttpGet("purchase-cycle")]
    public async Task<IActionResult> GetPurchaseCycleSettings(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseCycleSettingsQuery(), cancellationToken));

    [HttpPut("purchase-cycle")]
    public async Task<IActionResult> UpdatePurchaseCycleSettings([FromBody] UpdatePurchaseCycleSettingsRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePurchaseCycleSettingsCommand
        {
            CycleType = request.CycleType,
            RequiresPurchaseRequest = request.RequiresPurchaseRequest,
            RequiresQuotation = request.RequiresQuotation,
            RequiresPurchaseOrder = request.RequiresPurchaseOrder,
            RequiresGoodsReceipt = request.RequiresGoodsReceipt,
            AllowInvoiceWithoutOrder = request.AllowInvoiceWithoutOrder,
            AllowReceiptWithoutInvoice = request.AllowReceiptWithoutInvoice,
            AutoCreateReceiptOnInvoicePost = request.AutoCreateReceiptOnInvoicePost,
            AutoCreateInvoiceOnReceipt = request.AutoCreateInvoiceOnReceipt,
            RequiresApprovalForPurchaseOrder = request.RequiresApprovalForPurchaseOrder,
            RequiresApprovalForInvoice = request.RequiresApprovalForInvoice,
            DefaultPaymentTerms = request.DefaultPaymentTerms,
            CapitalizeAdditionalCosts = request.CapitalizeAdditionalCosts,
            AllowManualInvoiceLines = request.AllowManualInvoiceLines
        }, cancellationToken);

        return NoContent();
    }
}
