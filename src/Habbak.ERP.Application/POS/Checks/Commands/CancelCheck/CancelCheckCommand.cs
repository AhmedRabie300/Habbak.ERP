using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.CancelCheck;

/// <summary>قاعدة 30: لما شيك مرتبط بطرابيزة ينتهي (هنا: يُلغى)، الطرابيزة تتحول لـCleaning تلقائيًا
/// — مش لـFree مباشرة (محتاجة تأكيد يدوي من الخدمة إنها اتنضّفت).</summary>
public sealed record CancelCheckCommand(long Id) : IRequest;

public sealed class CancelCheckCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelCheckCommand>
{
    public async Task Handle(CancelCheckCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks.Include(c => c.Table).FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.Id);

        if (check.Status != CheckStatus.Open && check.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن إلغاء شيك منتهٍ بالفعل.");
        }

        check.Status = CheckStatus.Cancelled;

        if (check.Table is { Status: TableStatus.Busy } table)
        {
            table.Status = TableStatus.Cleaning;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
