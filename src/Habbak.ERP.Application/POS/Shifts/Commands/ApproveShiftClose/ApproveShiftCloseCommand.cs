using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Posting;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Shifts.Commands.ApproveShiftClose;

/// <summary>اعتماد يدوي مباشر لإغلاق وردية في حالة PendingCloseApproval (فرق تجاوز الحد المسموح) —
/// بدون محرك موافقات حقيقي مربوط بعد (نفس نمط ApprovalInstanceId المُعطَّل في كل الموديولات
/// السابقة). لا يوجد مسار رفض مقابل: Rejected مُعلَنة لاكتمال المخطط فقط وغير قابلة للوصول في هذا
/// البناء (تعليق Shift.cs).</summary>
public sealed record ApproveShiftCloseCommand(long Id) : IRequest;

public sealed class ApproveShiftCloseCommandHandler(IApplicationDbContext db, IPOSPostingService posPosting) : IRequestHandler<ApproveShiftCloseCommand>
{
    public async Task Handle(ApproveShiftCloseCommand request, CancellationToken cancellationToken)
    {
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Shift), request.Id);

        if (shift.Status != ShiftStatus.PendingCloseApproval)
        {
            throw new BusinessRuleException("POS-SHIFT-NOT-PENDING-APPROVAL", "لا يمكن اعتماد الإغلاق إلا لوردية في حالة انتظار الاعتماد.");
        }

        shift.Status = ShiftStatus.Closed;

        // Rule 21: the difference posts once the close is final, whatever its size.
        await posPosting.PostShiftVarianceAsync(shift, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
