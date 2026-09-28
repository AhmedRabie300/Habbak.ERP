using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Posting;
using Habbak.ERP.Application.POS.Shifts.Commands.OpenShift;
using Habbak.ERP.Domain.POS;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Shifts.Commands.CloseShift;

/// <summary>قاعدة 6: ExpectedClosingCashAmount = OpeningCashAmount + إجمالي المبيعات النقدية −
/// إجمالي Drop المعتمدة − إجمالي Pickup المعتمدة − إجمالي DrawerExpense. "المبيعات النقدية" هنا
/// = مجموع POSPayment.Amount لأي دفعة فيها AmountTendered (نفس إشارة "الدفعة نقدية" المُتَّبعة في
/// مرحلة الدفع — مفيش عمود IsCash على PaymentMethod، الإدخال الفعلي هو الإشارة). قاعدة 32: Drop/
/// Pickup لازم تكون Approved عشان تدخل الحساب — Recorded لسه معلّقة بتتجاهل تمامًا. قاعدة 5: فرق
/// أكبر من الحد المسموح به (BranchPOSSettings.MaxAllowedShiftCashDifference) يحوّل الوردية
/// لـPendingCloseApproval بدل Closed مباشرة.
///
/// القيود (Docs/Posting-Engine-Implementation-Plan.md، قسم 9): فواتير الوردية اللي لسه ماترحّلتش
/// بتترحّل هنا في وضع PerShift، وفي وضع PerTransaction كمان (كنس لأي فاتورة اتعملت وقت ما الوضع
/// كان مختلف) — في وضع PerDay لأ، دي مستنية زرار إقفال اليوم. قيد فرق الخزينة منفصل ويترحّل في كل
/// الأوضاع، بس لما الإقفال يبقى نهائي: لو الفرق محتاج اعتماد، بيترحّل عند الاعتماد (قاعدة 21).</summary>
public sealed record CloseShiftCommand : IRequest, IIdempotentRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public required IReadOnlyList<DenominationCountInput> ClosingCounts { get; init; }

    /// <summary>قاعدة 21/34 — نفس مبدأ OpenShiftCommand، null في أي نداء أونلاين عادي.</summary>
    public Guid? IdempotencyKey { get; init; }
}

public sealed class CloseShiftCommandValidator : AbstractValidator<CloseShiftCommand>
{
    public CloseShiftCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ClosingCounts).NotEmpty().WithMessage("إغلاق الوردية يتطلب عدّ الفئات النقدية.");

        RuleForEach(x => x.ClosingCounts).ChildRules(line =>
        {
            line.RuleFor(l => l.DenominationValue).GreaterThan(0);
            line.RuleFor(l => l.Count).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class CloseShiftCommandHandler(IApplicationDbContext db, IPOSPostingService posPosting, ICurrentCompanyContext current, ITimeEntryFeedService timeEntryFeed)
    : IRequestHandler<CloseShiftCommand>
{
    public async Task Handle(CloseShiftCommand request, CancellationToken cancellationToken)
    {
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Shift), request.Id);

        if (shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleException("POS-SHIFT-NOT-OPEN", "لا يمكن إغلاق الوردية إلا وهي في حالة مفتوحة.");
        }

        var threshold = await db.BranchPOSSettingsRows
            .Where(s => s.BranchId == shift.BranchId)
            .Select(s => (decimal?)s.MaxAllowedShiftCashDifference)
            .FirstOrDefaultAsync(cancellationToken) ?? new BranchPOSSettings().MaxAllowedShiftCashDifference;

        db.Entry(shift).Property(nameof(Shift.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        var cashSalesTotal = await db.POSPayments
            .Where(p => p.POSInvoice!.ShiftId == shift.Id && p.Status == POSPaymentStatus.Completed && p.AmountTendered != null)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var approvedDropTotal = await db.DrawerMovements
            .Where(m => m.ShiftId == shift.Id && m.MovementType == DrawerMovementType.Drop && m.Status == DrawerMovementStatus.Approved)
            .SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;

        var approvedPickupTotal = await db.DrawerMovements
            .Where(m => m.ShiftId == shift.Id && m.MovementType == DrawerMovementType.Pickup && m.Status == DrawerMovementStatus.Approved)
            .SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;

        var drawerExpenseTotal = await db.DrawerExpenses
            .Where(e => e.ShiftId == shift.Id)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;

        var actualClosingCashAmount = request.ClosingCounts.Sum(c => c.DenominationValue * c.Count);
        var expectedClosingCashAmount = shift.OpeningCashAmount + cashSalesTotal - approvedDropTotal - approvedPickupTotal - drawerExpenseTotal;
        var differenceAmount = actualClosingCashAmount - expectedClosingCashAmount;

        shift.ExpectedClosingCashAmount = expectedClosingCashAmount;
        shift.ActualClosingCashAmount = actualClosingCashAmount;
        shift.DifferenceAmount = differenceAmount;
        shift.ClosedAtUtc = DateTime.UtcNow;
        // Whoever closes it — the signed-in user, never a number typed into the form.
        shift.ClosedByUserId = current.UserId;
        shift.Status = Math.Abs(differenceAmount) > threshold ? ShiftStatus.PendingCloseApproval : ShiftStatus.Closed;

        foreach (var line in request.ClosingCounts)
        {
            shift.DenominationCounts.Add(new ShiftDenominationCount
            {
                CountType = DenominationCountType.Closing,
                DenominationValue = line.DenominationValue,
                Count = line.Count
            });
        }

        if (await posPosting.GetModeAsync(shift.BranchId, cancellationToken) != POSPostingMode.PerDay)
        {
            await posPosting.PostShiftInvoicesAsync(shift, cancellationToken);
        }

        if (shift.Status == ShiftStatus.Closed)
        {
            await posPosting.PostShiftVarianceAsync(shift, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        // Phase 3 POS Integration — نداء مباشر بعد الحفظ، صفر تعديل على Shift نفسه (Phase-3-Research.md §3.1).
        await timeEntryFeed.SuggestEntryForShiftCloseAsync(shift, cancellationToken);
    }
}
