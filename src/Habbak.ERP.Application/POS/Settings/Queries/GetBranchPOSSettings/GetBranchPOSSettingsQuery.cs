using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Settings.Dtos;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Settings.Queries.GetBranchPOSSettings;

/// <summary>Screen واحد لكل فرع؛ يرجع القيم الافتراضية (كل الأعلام معطّلة) لو مفيش صف اتسجل لسه.</summary>
public sealed record GetBranchPOSSettingsQuery(long BranchId) : IRequest<BranchPOSSettingsDto>;

public sealed class GetBranchPOSSettingsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBranchPOSSettingsQuery, BranchPOSSettingsDto>
{
    public async Task<BranchPOSSettingsDto> Handle(GetBranchPOSSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await db.BranchPOSSettingsRows
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.BranchId == request.BranchId, cancellationToken)
            ?? new BranchPOSSettings { BranchId = request.BranchId };

        return new BranchPOSSettingsDto
        {
            BranchId = request.BranchId,
            OperationMode = settings.OperationMode.ToString(),
            TipsEnabled = settings.TipsEnabled,
            ServiceChargeEnabled = settings.ServiceChargeEnabled,
            ServiceChargeRate = settings.ServiceChargeRate,
            VatEnabled = settings.VatEnabled,
            VatRate = settings.VatRate,
            AllowSplitPayment = settings.AllowSplitPayment,
            LoyaltyRedemptionEnabledAtPOS = settings.LoyaltyRedemptionEnabledAtPOS,
            ETAReceiptEnabled = settings.ETAReceiptEnabled,
            CashRoundingIncrement = settings.CashRoundingIncrement,
            MaxAllowedShiftCashDifference = settings.MaxAllowedShiftCashDifference,
            PostingMode = settings.PostingMode.ToString(),
            ShiftVarianceEmployeeLiabilityThreshold = settings.ShiftVarianceEmployeeLiabilityThreshold
        };
    }
}
