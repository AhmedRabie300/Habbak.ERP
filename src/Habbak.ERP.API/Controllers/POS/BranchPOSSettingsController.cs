using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.Settings.Commands.UpdateBranchPOSSettings;
using Habbak.ERP.Application.POS.Settings.Queries.GetBranchPOSSettings;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/settings/{branchId} — إعدادات نقطة البيع لكل فرع، قرارات جلسة الاستشارة قبل
/// التنفيذ (05-Module-POS-Shifts.md).</summary>
[ApiController]
[Authorize]
[Screen("POS_BRANCH_SETTINGS", LookupReads = true)]
[Route("api/v1/pos/branch-settings")]
public class BranchPOSSettingsController(ISender mediator) : ControllerBase
{
    public sealed record UpdateBranchPOSSettingsRequest(
        POSOperationMode OperationMode, bool TipsEnabled, bool ServiceChargeEnabled, decimal ServiceChargeRate,
        bool VatEnabled, decimal VatRate,
        bool AllowSplitPayment, bool LoyaltyRedemptionEnabledAtPOS, bool ETAReceiptEnabled,
        decimal CashRoundingIncrement, decimal MaxAllowedShiftCashDifference,
        POSPostingMode? PostingMode = null, decimal? ShiftVarianceEmployeeLiabilityThreshold = null);

    [HttpGet("{branchId:long}")]
    public async Task<IActionResult> Get(long branchId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetBranchPOSSettingsQuery(branchId), cancellationToken));

    [HttpPut("{branchId:long}")]
    public async Task<IActionResult> Update(long branchId, [FromBody] UpdateBranchPOSSettingsRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateBranchPOSSettingsCommand
        {
            BranchId = branchId,
            OperationMode = request.OperationMode,
            TipsEnabled = request.TipsEnabled,
            ServiceChargeEnabled = request.ServiceChargeEnabled,
            ServiceChargeRate = request.ServiceChargeRate,
            VatEnabled = request.VatEnabled,
            VatRate = request.VatRate,
            AllowSplitPayment = request.AllowSplitPayment,
            LoyaltyRedemptionEnabledAtPOS = request.LoyaltyRedemptionEnabledAtPOS,
            ETAReceiptEnabled = request.ETAReceiptEnabled,
            CashRoundingIncrement = request.CashRoundingIncrement,
            MaxAllowedShiftCashDifference = request.MaxAllowedShiftCashDifference,
            PostingMode = request.PostingMode,
            ShiftVarianceEmployeeLiabilityThreshold = request.ShiftVarianceEmployeeLiabilityThreshold
        }, cancellationToken);

        return NoContent();
    }
}
