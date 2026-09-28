namespace Habbak.ERP.Shared.Enums;

/// <summary>
/// The module that originated a journal entry (01-Module-Accounting.md, JournalEntry.SourceModule).
/// Lives in Shared because every module references it when posting through IPostingService.
/// </summary>
public enum SourceModule
{
    Manual = 1,
    Sales = 2,
    Purchasing = 3,
    POS = 4,
    Inventory = 5,
    Payroll = 6,
    FixedAssets = 7
}
