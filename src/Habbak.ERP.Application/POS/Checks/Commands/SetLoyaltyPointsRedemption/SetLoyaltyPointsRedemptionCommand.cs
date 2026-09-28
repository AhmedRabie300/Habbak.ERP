using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.SetLoyaltyPointsRedemption;

/// <summary>مراجعة 2026-09-13، قاعدة 12 — يسجّل نية استبدال نقاط على شيك مفتوح (معاينة فقط؛ الخصم
/// الفعلي من رصيد العميل يحصل في CompleteCheckPaymentCommand). Points = 0 يلغي الاستبدال. يتطلب
/// BranchPOSSettings.LoyaltyRedemptionEnabledAtPOS مفعَّل على فرع الشيك، وعميل مربوط برصيد كافٍ.</summary>
public sealed record SetLoyaltyPointsRedemptionCommand(long CheckId, decimal Points) : IRequest;

public sealed class SetLoyaltyPointsRedemptionCommandValidator : AbstractValidator<SetLoyaltyPointsRedemptionCommand>
{
    public SetLoyaltyPointsRedemptionCommandValidator()
    {
        RuleFor(x => x.CheckId).GreaterThan(0);
        RuleFor(x => x.Points).GreaterThanOrEqualTo(0);
    }
}

public sealed class SetLoyaltyPointsRedemptionCommandHandler(IApplicationDbContext db) : IRequestHandler<SetLoyaltyPointsRedemptionCommand>
{
    public async Task Handle(SetLoyaltyPointsRedemptionCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks.FirstOrDefaultAsync(c => c.Id == request.CheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.CheckId);

        if (check.Status != CheckStatus.Open && check.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن استبدال نقاط على شيك منتهٍ.");
        }

        if (request.Points == 0)
        {
            check.LoyaltyPointsToRedeem = null;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var settings = await db.BranchPOSSettingsRows.FirstOrDefaultAsync(s => s.BranchId == check.BranchId, cancellationToken)
            ?? new BranchPOSSettings { BranchId = check.BranchId };

        if (!settings.LoyaltyRedemptionEnabledAtPOS)
        {
            throw new BusinessRuleException("POS-LOYALTY-REDEMPTION-DISABLED", "استبدال نقاط الولاء غير مفعَّل على هذا الفرع.");
        }

        if (check.CustomerId is not { } customerId)
        {
            throw new BusinessRuleException("POS-CHECK-NO-CUSTOMER", "لا يمكن استبدال نقاط ولاء بدون ربط عميل بالشيك أولًا.");
        }

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), customerId);

        if (request.Points > customer.LoyaltyPointsBalance)
        {
            throw new BusinessRuleException("POS-LOYALTY-INSUFFICIENT-BALANCE", $"رصيد العميل الحالي {customer.LoyaltyPointsBalance:0.##} نقطة فقط.");
        }

        check.LoyaltyPointsToRedeem = request.Points;

        await db.SaveChangesAsync(cancellationToken);
    }
}
