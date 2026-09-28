using Habbak.ERP.Application.Common.Interfaces;
using MediatR;

namespace Habbak.ERP.Application.Accounting.JournalEntries.Commands.PostJournalEntry;

/// <summary>
/// Posts a manually-created Draft entry (00-Frontend-Specs.md, section 7 — status action button)
/// via the central posting engine (01-Module-Accounting.md, section 3 rules 1/2/3/9/21).
/// </summary>
public sealed record PostJournalEntryCommand(long Id) : IRequest<PostJournalEntryResult>;

public sealed record PostJournalEntryResult(string Status, long? ApprovalInstanceId);

public sealed class PostJournalEntryCommandHandler(IPostingService postingService, IApplicationDbContext db)
    : IRequestHandler<PostJournalEntryCommand, PostJournalEntryResult>
{
    public async Task<PostJournalEntryResult> Handle(PostJournalEntryCommand request, CancellationToken cancellationToken)
    {
        var result = await postingService.PostDraftEntryAsync(request.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new PostJournalEntryResult(result.Status.ToString(), result.ApprovalInstanceId);
    }
}
