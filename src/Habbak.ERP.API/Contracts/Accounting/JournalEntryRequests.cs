using Habbak.ERP.Application.Accounting.JournalEntries.Commands.CreateManualJournalEntry;

namespace Habbak.ERP.API.Contracts.Accounting;

public sealed record CreateJournalEntryRequest(
    long? BranchId,
    DateOnly EntryDate,
    string Description,
    IReadOnlyList<JournalEntryLineInput> Lines);

public sealed record UpdateJournalEntryRequest(
    string RowVersion,
    long? BranchId,
    DateOnly EntryDate,
    string Description,
    IReadOnlyList<JournalEntryLineInput> Lines);
