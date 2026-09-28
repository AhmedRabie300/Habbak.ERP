using Habbak.ERP.Application.Common.Interfaces;
using MediatR;

namespace Habbak.ERP.Application.Accounting.JournalEntries.Commands.ReverseJournalEntry;

/// <summary>
/// "إنشاء قيد عكسي" button (01-Module-Accounting.md, rule 16 and section 5, screen 4). Creates
/// the mirrored reversal entry as Draft — posting it is a separate, explicit action.
/// </summary>
public sealed record ReverseJournalEntryCommand(long Id) : IRequest<long>;

public sealed class ReverseJournalEntryCommandHandler(IPostingService postingService, IApplicationDbContext db)
    : IRequestHandler<ReverseJournalEntryCommand, long>
{
    public async Task<long> Handle(ReverseJournalEntryCommand request, CancellationToken cancellationToken)
    {
        var result = await postingService.ReverseAsync(request.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return result.JournalEntry.Id;
    }
}
