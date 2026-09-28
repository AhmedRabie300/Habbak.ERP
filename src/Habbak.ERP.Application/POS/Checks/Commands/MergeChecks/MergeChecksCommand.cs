using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.MergeChecks;

/// <summary>قاعدة 9: الشيكات المصدر تتحول لـMerged وتُربَط بـMergedIntoCheckId، وكل بنودها تُنسخ
/// (مش تُنقل) للشيك الهدف — كل نسخة بتاخد رقم سطر جديد وSentToKitchenAt تتصفّر (إرسال البند
/// للمطبخ فعل جديد على الشيك المدموج فيه، مش امتداد لحالته القديمة).</summary>
public sealed record MergeChecksCommand(long TargetCheckId, IReadOnlyList<long> SourceCheckIds) : IRequest;

public sealed class MergeChecksCommandValidator : AbstractValidator<MergeChecksCommand>
{
    public MergeChecksCommandValidator()
    {
        RuleFor(x => x.TargetCheckId).GreaterThan(0);
        RuleFor(x => x.SourceCheckIds).NotEmpty();
    }
}

public sealed class MergeChecksCommandHandler(IApplicationDbContext db) : IRequestHandler<MergeChecksCommand>
{
    public async Task Handle(MergeChecksCommand request, CancellationToken cancellationToken)
    {
        if (request.SourceCheckIds.Contains(request.TargetCheckId))
        {
            throw new BusinessRuleException("POS-CHECK-MERGE-SELF", "لا يمكن دمج شيك في نفسه.");
        }

        var target = await db.Checks.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.TargetCheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.TargetCheckId);

        if (target.Status != CheckStatus.Open && target.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن الدمج في شيك منتهٍ.");
        }

        var sources = await db.Checks
            .Include(c => c.Lines)
            .Include(c => c.Table)
            .Where(c => request.SourceCheckIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        if (sources.Count != request.SourceCheckIds.Count)
        {
            throw new NotFoundException(nameof(Check), string.Join(",", request.SourceCheckIds));
        }

        if (sources.Any(c => c.Status != CheckStatus.Open && c.Status != CheckStatus.Held))
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن دمج شيك منتهٍ بالفعل.");
        }

        var nextLineNumber = target.Lines.Count == 0 ? 1 : target.Lines.Max(l => l.LineNumber) + 1;

        foreach (var source in sources)
        {
            foreach (var line in source.Lines)
            {
                target.Lines.Add(new CheckLine
                {
                    LineNumber = nextLineNumber++,
                    ItemId = line.ItemId,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    DiscountAmount = line.DiscountAmount,
                    IsPriceManuallyOverridden = line.IsPriceManuallyOverridden,
                    Note = line.Note
                });
            }

            source.Status = CheckStatus.Merged;
            source.MergedIntoCheckId = target.Id;

            if (source.Table is { Status: TableStatus.Busy } table)
            {
                table.Status = TableStatus.Cleaning;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
