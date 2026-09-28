using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.RemoveCheckLine;

/// <summary>قرار جلسة الاستشارة قبل التنفيذ رقم 8: حذف بند قبل إرساله للمطبخ بدون احتكاك؛ حذف بند
/// بعد الإرسال يتطلب سبب إلزامي ويُوثَّق دايمًا في CheckLineVoid (بغض النظر عن أي إعداد تفعيل Audit
/// عام — نفس صرامة قاعدة 13 الخاصة بـVoid/Refund على الدفعات).</summary>
public sealed record RemoveCheckLineCommand(long CheckId, long LineId, string? VoidReason) : IRequest;

public sealed class RemoveCheckLineCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, IUserAccessService access)
    : IRequestHandler<RemoveCheckLineCommand>
{
    public async Task Handle(RemoveCheckLineCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks.FirstOrDefaultAsync(c => c.Id == request.CheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.CheckId);

        if (check.Status != CheckStatus.Open && check.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن حذف بند من شيك منتهٍ.");
        }

        var line = await db.CheckLines.FirstOrDefaultAsync(l => l.Id == request.LineId && l.CheckId == request.CheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(CheckLine), request.LineId);

        if (line.SentToKitchenAt is not null)
        {
            if (string.IsNullOrWhiteSpace(request.VoidReason))
            {
                throw new BusinessRuleException("POS-CHECK-LINE-VOID-REASON-REQUIRED", "حذف بند اتبعت للمطبخ يتطلب سبب إلزاميًا.");
            }

            // Voiding a line the kitchen already has is the VoidSentLine button.
            await ButtonGuard.RequireAsync(access, "POS_TABLE_BOARD", "VoidSentLine", cancellationToken);
            await ButtonGuard.AuditAsync(db, access, currentCompanyContext, "POS_TABLE_BOARD", "VoidSentLine", nameof(CheckLine), line.Id, cancellationToken);

            db.CheckLineVoids.Add(new CheckLineVoid
            {
                CheckId = check.Id,
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                Reason = request.VoidReason.Trim(),
                VoidedByUserId = currentCompanyContext.UserId,
                VoidedAtUtc = DateTime.UtcNow
            });
        }

        db.CheckLines.Remove(line);
        await db.SaveChangesAsync(cancellationToken);
    }
}
