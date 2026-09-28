using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.ResumeCheck;

public sealed record ResumeCheckCommand(long Id) : IRequest;

public sealed class ResumeCheckCommandHandler(IApplicationDbContext db) : IRequestHandler<ResumeCheckCommand>
{
    public async Task Handle(ResumeCheckCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.Id);

        if (check.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-HELD", "لا يمكن استرجاع شيك إلا وهو في حالة معلّقة.");
        }

        check.Status = CheckStatus.Open;

        await db.SaveChangesAsync(cancellationToken);
    }
}
