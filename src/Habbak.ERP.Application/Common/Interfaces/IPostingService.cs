using Habbak.ERP.Application.Common.Models;

namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>
/// The central posting engine (00-Project-Overview.md, section 11). Every module — including
/// this one's own Vouchers/TreasuryTransfers/CustodySettlements — creates journal entries only
/// through this interface, never by inserting a JournalEntry directly.
///
/// None of these methods call SaveChangesAsync: they add/modify tracked entities on the shared
/// DbContext and return them, so the calling command handler can make its own additional
/// changes (e.g. set Voucher.Status/JournalEntry) and persist everything in a single
/// transaction (01-Module-Accounting.md, rule 5).
/// </summary>
public interface IPostingService
{
    /// <summary>
    /// Builds a new journal entry from scratch, validates it (balance, postable accounts,
    /// mandatory dimensions, open period), and either posts it immediately or leaves it in
    /// Draft pending an approval workflow it has just started.
    /// Throws <see cref="Exceptions.PostingValidationException"/> when validation fails.
    /// </summary>
    Task<PostingResult> PostAsync(PostingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the same validation as <see cref="PostAsync"/> against an already-persisted Draft
    /// entry (e.g. one entered manually on the Journal Entries screen) and posts it, or leaves
    /// it Draft pending approval.
    /// </summary>
    Task<PostingResult> PostDraftEntryAsync(long journalEntryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the mirrored reversal of an already-posted entry (rule 16: same lines with
    /// debit/credit swapped, description carried over with "عكس قيد رقم X" appended). The
    /// reversal is created as Draft, pending an explicit posting action — it is not posted
    /// automatically (matches the reference mockup's reversal flow).
    /// </summary>
    Task<PostingResult> ReverseAsync(long journalEntryId, CancellationToken cancellationToken = default);
}
