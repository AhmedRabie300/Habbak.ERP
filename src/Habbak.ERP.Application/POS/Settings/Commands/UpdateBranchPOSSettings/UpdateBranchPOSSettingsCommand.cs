using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Settings.Commands.UpdateBranchPOSSettings;

/// <summary>Upserts the single BranchPOSSettings row for the given branch.</summary>
public sealed record UpdateBranchPOSSettingsCommand : IRequest
{
    public required long BranchId { get; init; }
    public required POSOperationMode OperationMode { get; init; }
    public required bool TipsEnabled { get; init; }
    public required bool ServiceChargeEnabled { get; init; }
    public required decimal ServiceChargeRate { get; init; }
    public required bool VatEnabled { get; init; }
    public required decimal VatRate { get; init; }
    public required bool AllowSplitPayment { get; init; }
    public required bool LoyaltyRedemptionEnabledAtPOS { get; init; }
    public required bool ETAReceiptEnabled { get; init; }
    public required decimal CashRoundingIncrement { get; init; }
    public required decimal MaxAllowedShiftCashDifference { get; init; }

    /// <summary>Optional so a client that predates the posting engine cannot wipe them by omission.</summary>
    public POSPostingMode? PostingMode { get; init; }
    public decimal? ShiftVarianceEmployeeLiabilityThreshold { get; init; }
}

public sealed class UpdateBranchPOSSettingsCommandValidator : AbstractValidator<UpdateBranchPOSSettingsCommand>
{
    public UpdateBranchPOSSettingsCommandValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.ServiceChargeRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.VatRate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CashRoundingIncrement).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxAllowedShiftCashDifference).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PostingMode).IsInEnum().When(x => x.PostingMode is not null);
        RuleFor(x => x.ShiftVarianceEmployeeLiabilityThreshold).GreaterThanOrEqualTo(0).When(x => x.ShiftVarianceEmployeeLiabilityThreshold is not null);
    }
}

public sealed class UpdateBranchPOSSettingsCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdateBranchPOSSettingsCommand>
{
    public async Task Handle(UpdateBranchPOSSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Branches.AnyAsync(b => b.Id == request.BranchId, cancellationToken))
        {
            throw new NotFoundException("Branch", request.BranchId);
        }

        var settings = await db.BranchPOSSettingsRows
            .FirstOrDefaultAsync(s => s.BranchId == request.BranchId, cancellationToken);

        if (settings is null)
        {
            settings = new BranchPOSSettings { CompanyId = currentCompanyContext.CompanyId, BranchId = request.BranchId };
            db.BranchPOSSettingsRows.Add(settings);
        }

        settings.OperationMode = request.OperationMode;
        settings.TipsEnabled = request.TipsEnabled;
        settings.ServiceChargeEnabled = request.ServiceChargeEnabled;
        settings.ServiceChargeRate = request.ServiceChargeRate;
        settings.VatEnabled = request.VatEnabled;
        settings.VatRate = request.VatRate;
        settings.AllowSplitPayment = request.AllowSplitPayment;
        settings.LoyaltyRedemptionEnabledAtPOS = request.LoyaltyRedemptionEnabledAtPOS;
        settings.ETAReceiptEnabled = request.ETAReceiptEnabled;
        settings.CashRoundingIncrement = request.CashRoundingIncrement;
        settings.MaxAllowedShiftCashDifference = request.MaxAllowedShiftCashDifference;
        settings.PostingMode = request.PostingMode ?? settings.PostingMode;
        settings.ShiftVarianceEmployeeLiabilityThreshold = request.ShiftVarianceEmployeeLiabilityThreshold ?? settings.ShiftVarianceEmployeeLiabilityThreshold;

        await db.SaveChangesAsync(cancellationToken);
    }
}
