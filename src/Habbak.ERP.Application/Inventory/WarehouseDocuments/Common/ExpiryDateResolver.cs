using Habbak.ERP.Domain.Inventory;

namespace Habbak.ERP.Application.Inventory.WarehouseDocuments.Common;

/// <summary>My Remarks/Remarks2.md, remark 3.5 — "يتم حساب تاريخ الانتهاء تلقائياً عند إضافة رصيد
/// أو استلام": an inbound line whose item is batch/expiry-tracked (Item.IsTracked) and carries a
/// ShelfLifeDays gets its ExpiryDate computed from the document date when the caller didn't supply
/// one explicitly — matching StockTransaction.ExpiryDate's own class doc, which already documented
/// this as the intended default but nothing computed it until now. An explicit caller-supplied
/// ExpiryDate always wins (rule 32's "قابل للتعديل يدويًا").</summary>
public static class ExpiryDateResolver
{
    public static DateOnly? Resolve(DateOnly documentDate, DateOnly? explicitExpiryDate, Item item) =>
        explicitExpiryDate
        ?? (item.IsTracked && item.ShelfLifeDays is { } shelfLifeDays ? documentDate.AddDays(shelfLifeDays) : null);
}
