using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.HoldCheck;

/// <summary>قاعدة 8: التعليق متاح لصالة وتيك أواي ودليفري بنفس الآلية — الطرابيزة (لو DineIn)
/// تفضل Busy، الشيك بس اللي يتحول لـHeld.</summary>
public sealed record HoldCheckCommand(long Id) : IRequest;

public sealed class HoldCheckCommandHandler(IApplicationDbContext db) : IRequestHandler<HoldCheckCommand>
{
    public async Task Handle(HoldCheckCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.Id);

        if (check.Status != CheckStatus.Open)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-OPEN", "لا يمكن تعليق شيك إلا وهو في حالة مفتوحة.");
        }

        check.Status = CheckStatus.Held;

        await db.SaveChangesAsync(cancellationToken);
    }
}
