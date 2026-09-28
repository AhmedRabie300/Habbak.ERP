using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Application.Settings.Users;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Shifts.Commands.OpenShift;

public sealed record DenominationCountInput(decimal DenominationValue, int Count);

/// <summary>قاعدة 1 (فتح الوردية يتطلب عدّ فئات نقدية) + قاعدة 2 (مفيش وردية Open تانية على نفس
/// الجهاز، مؤكَّدة على مستوى الـDB عبر فهرس فريد مفلتَر في ShiftConfiguration). قاعدة 31: الكاشير
/// اللي متسكّن على الجهاز النهارده (ShiftAssignment) بس اللي يفتح ورديته. فتح وردية لكاشير مش
/// متسكّن، أو لحد غيرك، محتاج صلاحية الاعتماد على شاشة فتح/إغلاق الوردية (المشرف).
/// قاعدة 21/34: فتح الوردية معاملة قابلة للمزامنة من جهاز Offline — IdempotencyKey اختياري
/// (null في أي نداء أونلاين عادي)، بيتولّد على الجهاز نفسه وقت الإنشاء الفعلي لو الجهاز كان
/// Offline، فلو نفس الطلب اتبعت تاني (Retry أو مزامنة مكرَّرة)، IdempotencyBehavior بيرجّع نفس
/// نتيجة أول مرة بدل ما يفتح وردية تانية بالغلط.</summary>
public sealed record OpenShiftCommand : IRequest<long>, IIdempotentRequest
{
    public required long POSTerminalId { get; init; }
    /// <summary>The cashier the shift belongs to — the signed-in user when left out.</summary>
    public long? CashierUserId { get; init; }
    public required IReadOnlyList<DenominationCountInput> OpeningCounts { get; init; }
    public Guid? IdempotencyKey { get; init; }
}

public sealed class OpenShiftCommandValidator : AbstractValidator<OpenShiftCommand>
{
    public OpenShiftCommandValidator()
    {
        RuleFor(x => x.POSTerminalId).GreaterThan(0);
        RuleFor(x => x.CashierUserId).GreaterThan(0).When(x => x.CashierUserId is not null);
        RuleFor(x => x.OpeningCounts).NotEmpty().WithMessage("فتح الوردية يتطلب عدّ الفئات النقدية.");

        RuleForEach(x => x.OpeningCounts).ChildRules(line =>
        {
            line.RuleFor(l => l.DenominationValue).GreaterThan(0);
            line.RuleFor(l => l.Count).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class OpenShiftCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, IUserAccessService access, ITimeEntryFeedService timeEntryFeed)
    : IRequestHandler<OpenShiftCommand, long>
{
    public async Task<long> Handle(OpenShiftCommand request, CancellationToken cancellationToken)
    {
        var terminal = await db.POSTerminals.FirstOrDefaultAsync(t => t.Id == request.POSTerminalId, cancellationToken)
            ?? throw new NotFoundException(nameof(POSTerminal), request.POSTerminalId);

        if (!terminal.IsActive)
        {
            throw new BusinessRuleException("POS-TERMINAL-INACTIVE", "لا يمكن فتح وردية على جهاز نقطة بيع غير مفعّل.");
        }

        var hasOpenShift = await db.Shifts.AnyAsync(
            s => s.POSTerminalId == request.POSTerminalId && s.Status == ShiftStatus.Open, cancellationToken);
        if (hasOpenShift)
        {
            throw new BusinessRuleException("POS-SHIFT-ALREADY-OPEN", "يوجد وردية مفتوحة بالفعل على هذا الجهاز.");
        }

        var cashierUserId = request.CashierUserId ?? current.UserId;
        await CompanyUsers.EnsureWorksHereAsync(db, cashierUserId, terminal.CompanyId!.Value, cancellationToken);

        // قاعدة 31 — the terminal's local day, as the assignment screen records it.
        var today = DateOnly.FromDateTime(DateTime.Now);
        var assigned = await db.ShiftAssignments.AnyAsync(
            a => a.POSTerminalId == terminal.Id && a.UserId == cashierUserId && a.AssignedDate == today, cancellationToken);
        if (!assigned || cashierUserId != current.UserId)
        {
            var rights = await access.GetCurrentAsync(cancellationToken);
            if (!rights.Can(ScreenAction.Approve, [ShiftScreens.Console]))
            {
                throw !assigned
                    ? new BusinessRuleException("POS-SHIFT-NOT-ASSIGNED", "الكاشير مش متسكّن على الجهاز ده النهارده — التسكين من شاشة تعيين الكاشير، أو مشرف يفتح الوردية.")
                    : new BusinessRuleException("POS-SHIFT-OPEN-FOR-OTHER", "فتح وردية لكاشير تاني محتاج صلاحية مشرف.");
            }
        }

        var openingCashAmount = request.OpeningCounts.Sum(c => c.DenominationValue * c.Count);

        var shift = new Shift
        {
            CompanyId = terminal.CompanyId,
            BranchId = terminal.BranchId,
            POSTerminalId = request.POSTerminalId,
            CashierUserId = cashierUserId,
            Status = ShiftStatus.Open,
            OpenedAtUtc = DateTime.UtcNow,
            OpeningCashAmount = openingCashAmount
        };

        foreach (var line in request.OpeningCounts)
        {
            shift.DenominationCounts.Add(new ShiftDenominationCount
            {
                CountType = DenominationCountType.Opening,
                DenominationValue = line.DenominationValue,
                Count = line.Count
            });
        }

        db.Shifts.Add(shift);
        await db.SaveChangesAsync(cancellationToken);

        // Phase 3 POS Integration — نداء مباشر بعد الحفظ، صفر تعديل على Shift نفسه (Phase-3-Research.md §3.1).
        await timeEntryFeed.SuggestEntryForShiftOpenAsync(shift, cancellationToken);

        return shift.Id;
    }
}

public static class ShiftScreens
{
    public const string Console = "POS_SHIFT_CONSOLE";
}
