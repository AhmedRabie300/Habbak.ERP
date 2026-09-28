using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.ApplyManualDiscount;

/// <summary>مراجعة 2026-09-13، بند 3.2 — خصم يدوي على مستوى الفاتورة كاملة (نسبة % أو قيمة ثابتة
/// + سبب إلزامي)، منفصل عن CheckLine.DiscountAmount لكل بند. يُطبَّق فورًا على الشيك المفتوح
/// (يُقرأ لاحقًا وقت الدفع في CompleteCheckPaymentCommand) — تطبيق خصم جديد يستبدل أي خصم سابق.</summary>
public sealed record ApplyManualDiscountCommand(long CheckId, POSManualDiscountType Type, decimal Value, string Reason) : IRequest;

public sealed class ApplyManualDiscountCommandValidator : AbstractValidator<ApplyManualDiscountCommand>
{
    public ApplyManualDiscountCommandValidator()
    {
        RuleFor(x => x.CheckId).GreaterThan(0);
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value).LessThanOrEqualTo(100).When(x => x.Type == POSManualDiscountType.Percentage)
            .WithMessage("نسبة الخصم لا يمكن أن تتجاوز 100%.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("سبب الخصم اليدوي إلزامي.").MaximumLength(500);
    }
}

public sealed class ApplyManualDiscountCommandHandler(IApplicationDbContext db) : IRequestHandler<ApplyManualDiscountCommand>
{
    public async Task Handle(ApplyManualDiscountCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks.FirstOrDefaultAsync(c => c.Id == request.CheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.CheckId);

        if (check.Status != CheckStatus.Open && check.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن تطبيق خصم على شيك منتهٍ.");
        }

        check.ManualDiscountType = request.Type;
        check.ManualDiscountValue = request.Value;
        check.ManualDiscountReason = request.Reason.Trim();

        await db.SaveChangesAsync(cancellationToken);
    }
}
