namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>
/// Shared by IPostingService and the manual "create draft" command handler so a journal entry
/// gets its real yearly-sequence number (01-Module-Accounting.md, JournalEntry.EntryNumber) at
/// creation time regardless of whether it starts Draft or Posted — matching the reference
/// mockup, where even Draft/automated rows already carry a proper "JV-xxxx" number.
/// </summary>
public interface IJournalEntryNumberGenerator
{
    Task<string> GenerateAsync(long companyId, int year, CancellationToken cancellationToken = default);
}
