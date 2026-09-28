using Habbak.ERP.Domain.Purchasing;

namespace Habbak.ERP.Application.Purchasing.Settings;

/// <summary>
/// What each named purchase cycle (03-Module-Purchasing.md, section 2.2) actually means, as the set
/// of documents it requires.
///
/// Before Remarks4 item 6, CycleType was a label stored next to flags that could contradict it — a
/// company could pick "دورة مباشرة" and still be told an invoice needs an order. The type now owns
/// the four document flags: picking a type fills them (the settings screen applies the preset), and
/// saving a combination that contradicts the type is refused. The behavioural flags around them
/// (auto-create, approvals, payment terms, capitalising costs) stay free — they are choices within
/// a cycle, not part of its definition.
/// </summary>
public sealed record PurchaseCyclePreset(
    PurchaseCycleType CycleType,
    bool RequiresPurchaseRequest,
    bool RequiresQuotation,
    bool RequiresPurchaseOrder,
    bool RequiresGoodsReceipt,
    bool AllowInvoiceWithoutOrder,
    bool AllowReceiptWithoutInvoice);

public static class PurchaseCyclePresets
{
    public static readonly IReadOnlyList<PurchaseCyclePreset> All =
    [
        // الدورة الكاملة: طلب → عروض أسعار → أمر → استلام → فاتورة.
        new(PurchaseCycleType.Full, true, true, true, true, false, true),

        // شراء مباشر: فاتورة (واستلام) من غير أمر شراء.
        new(PurchaseCycleType.Direct, false, false, false, true, true, true),

        // بالأمر: أمر شراء واستلام، من غير طلب ولا عروض أسعار.
        new(PurchaseCycleType.OrderBased, false, false, true, true, false, true),

        // بالطلب: طلب شراء ثم أمر واستلام، من غير عروض أسعار.
        new(PurchaseCycleType.RequestBased, true, false, true, true, false, true),

        // مبسّطة: فاتورة بس — مافيش أمر ولا إذن إضافة إلزامي.
        new(PurchaseCycleType.Simplified, false, false, false, false, true, true)
    ];

    public static PurchaseCyclePreset For(PurchaseCycleType type) =>
        All.FirstOrDefault(p => p.CycleType == type) ?? All[0];

    /// <summary>The document flags that do not match the named cycle, by their property name.</summary>
    public static IReadOnlyList<string> Conflicts(
        PurchaseCycleType type,
        bool requiresPurchaseRequest,
        bool requiresQuotation,
        bool requiresPurchaseOrder,
        bool requiresGoodsReceipt,
        bool allowInvoiceWithoutOrder,
        bool allowReceiptWithoutInvoice)
    {
        var preset = For(type);
        var conflicts = new List<string>();
        if (preset.RequiresPurchaseRequest != requiresPurchaseRequest) conflicts.Add(nameof(preset.RequiresPurchaseRequest));
        if (preset.RequiresQuotation != requiresQuotation) conflicts.Add(nameof(preset.RequiresQuotation));
        if (preset.RequiresPurchaseOrder != requiresPurchaseOrder) conflicts.Add(nameof(preset.RequiresPurchaseOrder));
        if (preset.RequiresGoodsReceipt != requiresGoodsReceipt) conflicts.Add(nameof(preset.RequiresGoodsReceipt));
        if (preset.AllowInvoiceWithoutOrder != allowInvoiceWithoutOrder) conflicts.Add(nameof(preset.AllowInvoiceWithoutOrder));
        if (preset.AllowReceiptWithoutInvoice != allowReceiptWithoutInvoice) conflicts.Add(nameof(preset.AllowReceiptWithoutInvoice));
        return conflicts;
    }
}
