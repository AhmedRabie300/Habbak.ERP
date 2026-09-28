using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Application.Inventory.Settings;

/// <summary>
/// Rule 17 (03-Inventory-Module.md): the production/sales model is resolved by scope priority
/// Item &gt; POS &gt; Branch &gt; Company. Until this existed the setting was stored and edited but never
/// read by anything, so configuring it had no effect anywhere.
///
/// Callers pass the whole settings list rather than querying per item: a check has a handful of
/// lines and the table holds one row per configured scope, so one read beats a query per line.
/// </summary>
internal static class ProductionSalesModeResolver
{
    /// <summary>
    /// The model used when no scope matches. Deliberately <see cref="ProductionSalesMode.Stocked"/>:
    /// turning real-time consumption on changes what a sale does to inventory and can start
    /// refusing sales (rule 18), so it is an explicit decision — add one Company-scope row to
    /// switch a whole company over.
    /// </summary>
    public const ProductionSalesMode Default = ProductionSalesMode.Stocked;

    public static ProductionSalesMode Resolve(
        IReadOnlyCollection<ProductionSalesModeSetting> settings,
        long itemId,
        long posTerminalId,
        long? branchId)
    {
        return Match(settings, SettingScopeType.Item, itemId)
            ?? Match(settings, SettingScopeType.POS, posTerminalId)
            ?? (branchId is { } branch ? Match(settings, SettingScopeType.Branch, branch) : null)
            ?? Match(settings, SettingScopeType.Company, null)
            ?? Default;
    }

    private static ProductionSalesMode? Match(
        IReadOnlyCollection<ProductionSalesModeSetting> settings, SettingScopeType scopeType, long? scopeId)
    {
        foreach (var setting in settings)
        {
            if (setting.ScopeType == scopeType && setting.ScopeId == scopeId)
            {
                return setting.Mode;
            }
        }

        return null;
    }
}
