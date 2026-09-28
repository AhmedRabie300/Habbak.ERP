using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.FireCheckLinesToKitchen;

/// <summary>يعادل فعل "طباعة كشف مطبخ" (screen #5) — مستقل عن الفاتورة نفسها. يختم كل بند لسه ما
/// اتبعتش بوقت الإرسال، عشان يفرّق لاحقًا بين حذف بند قبل/بعد الإرسال (قرار جلسة الاستشارة رقم 8).
/// لا يفعل حاجة (204 بدون تأثير) لو كل البنود اتبعتت بالفعل.</summary>
public sealed record FireCheckLinesToKitchenCommand(long CheckId) : IRequest;

public sealed class FireCheckLinesToKitchenCommandHandler(IApplicationDbContext db) : IRequestHandler<FireCheckLinesToKitchenCommand>
{
    public async Task Handle(FireCheckLinesToKitchenCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.CheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.CheckId);

        if (check.Status != CheckStatus.Open && check.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن إرسال بنود شيك منتهٍ للمطبخ.");
        }

        var now = DateTime.UtcNow;
        foreach (var line in check.Lines.Where(l => l.SentToKitchenAt is null))
        {
            line.SentToKitchenAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
