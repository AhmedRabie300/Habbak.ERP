namespace Habbak.ERP.Application.Settings.Access;

/// <param name="Code">Stable code — what ButtonPermission rows, [ScreenButton] and the frontend name.</param>
/// <param name="FallbackAction">
/// What the button needs when a role has no row for it: the screen permission its endpoint already
/// requires (a test holds the two together), so adding the catalog changed nothing for anyone.
/// </param>
/// <param name="ServerEnforced">
/// False for buttons that never call the server on their own (reprint, split the bill): hiding them
/// is all a permission can do, and the screen says so.
/// </param>
public sealed record ButtonDefinition(string Code, string NameAr, string NameEn, ScreenAction FallbackAction, bool ServerEnforced = true);

/// <summary>
/// The special buttons that get their own permission, per screen (MenuItem code). The source of
/// truth: a button that is not listed here cannot be given a permission. Only buttons that exist in
/// the screens today are listed — a permission for a button nobody can press would be a switch wired
/// to nothing. A screen's plain actions (save, delete, print, export) stay with ScreenPermission.
/// </summary>
public static class ButtonPermissionCatalog
{
    public static readonly IReadOnlyDictionary<string, ButtonDefinition[]> Buttons = new Dictionary<string, ButtonDefinition[]>
    {
        // The check being sold (/pos/checks/:id, reached from the sales screen).
        ["POS_TABLE_BOARD"] =
        [
            new("Hold", "تعليق / استكمال الشيك", "Hold / resume check", ScreenAction.Edit),
            new("Cancel", "إلغاء الشيك", "Cancel check", ScreenAction.Approve),
            new("ApplyManualDiscount", "خصم يدوي", "Manual discount", ScreenAction.Edit),
            new("RedeemLoyalty", "استبدال نقاط الولاء", "Redeem loyalty points", ScreenAction.Edit),
            new("FireToKitchen", "إرسال للمطبخ", "Send to kitchen", ScreenAction.Edit),
            new("EditPrice", "تعديل سعر صنف", "Change an item's price", ScreenAction.Edit),
            new("VoidSentLine", "حذف صنف اتبعت للمطبخ", "Void a line already sent to the kitchen", ScreenAction.Delete),
            new("SplitBill", "تقسيم الفاتورة", "Split the bill", ScreenAction.Edit, ServerEnforced: false),
            new("Reprint", "إعادة طباعة", "Reprint", ScreenAction.Print, ServerEnforced: false)
        ],
        ["POS_CHECKS_OPEN"] = [new("Merge", "دمج المحدد", "Merge selected", ScreenAction.Edit)],
        ["POS_CHECKS_HELD"] = [new("Merge", "دمج المحدد", "Merge selected", ScreenAction.Edit)],
        ["POS_SHIFT_CONSOLE"] = [new("CloseShift", "إقفال الوردية", "Close shift", ScreenAction.Edit)],
        ["POS_SHIFTS"] =
        [
            new("ApproveClose", "اعتماد إقفال الوردية", "Approve shift close", ScreenAction.Approve),
            new("DayClose", "إقفال اليوم", "Close the day", ScreenAction.Approve)
        ],
        ["POS_DRAWER_MOVEMENTS"] =
        [
            new("Approve", "اعتماد الحركة", "Approve movement", ScreenAction.Approve),
            new("Reject", "رفض الحركة", "Reject movement", ScreenAction.Approve)
        ],
        ["SALES_INVOICES"] =
        [
            new("Post", "ترحيل", "Post", ScreenAction.Approve),
            new("Reject", "رفض", "Reject", ScreenAction.Approve),
            new("Cancel", "إلغاء (بقيد عكسي)", "Cancel (reversing entry)", ScreenAction.Approve)
        ],
        ["PURCHASING_PURCHASE_INVOICES"] =
        [
            new("Submit", "إرسال للاعتماد", "Submit for approval", ScreenAction.Edit),
            new("Post", "ترحيل", "Post", ScreenAction.Approve),
            new("Reject", "رفض", "Reject", ScreenAction.Approve),
            new("Cancel", "إلغاء (بقيد عكسي)", "Cancel (reversing entry)", ScreenAction.Approve)
        ],
        ["PURCHASING_PURCHASE_ORDERS"] =
        [
            new("Send", "إرسال للمورد", "Send to supplier", ScreenAction.Edit),
            new("Confirm", "تأكيد", "Confirm", ScreenAction.Edit),
            new("Reject", "رفض", "Reject", ScreenAction.Approve),
            new("Cancel", "إلغاء", "Cancel", ScreenAction.Approve)
        ],
        ["INVENTORY_COUNTS"] =
        [
            new("Start", "بدء الجرد", "Start count", ScreenAction.Edit),
            new("SubmitForSettlement", "إرسال للتسوية", "Submit for settlement", ScreenAction.Edit),
            new("SettleLine", "تسوية سطر", "Settle a line", ScreenAction.Approve),
            new("CompleteSettlement", "إنهاء التسوية", "Complete settlement", ScreenAction.Approve),
            new("Close", "إقفال الجرد", "Close count", ScreenAction.Edit),
            new("Reject", "رفض", "Reject", ScreenAction.Approve),
            new("Cancel", "إلغاء", "Cancel", ScreenAction.Approve)
        ],
        ["INVENTORY_PRODUCTION_ORDERS"] =
        [
            new("Start", "بدء التنفيذ", "Start", ScreenAction.Edit),
            new("Complete", "إكمال", "Complete", ScreenAction.Edit),
            new("Cancel", "إلغاء", "Cancel", ScreenAction.Approve)
        ],
        // Docs/Implementation/HR-Core-Plan.md §1.1b, Batch B5 — HR_REVEAL_PII: revealing an
        // employee's plaintext National ID/IBAN (HrPiiController). Fallback is Approve, the
        // strictest ScreenAction available, so a role gets this only once explicitly granted —
        // never implicitly through ordinary View/Edit rights on HR_EMPLOYEES.
        ["HR_EMPLOYEES"] = [new("RevealPii", "إظهار البيانات الحساسة", "Reveal PII", ScreenAction.Approve)],

        // Docs/Modules/00-Project-Overview.md §12.4 rule 10 — the Manual Fallback: an administrative
        // override that reassigns a stuck ApprovalInstance to a different approver (the original one
        // is on leave, say). Same precedent as HR_EMPLOYEES/RevealPii above — Approve fallback, so a
        // role needs it granted explicitly rather than inheriting it from ordinary screen access.
        ["SETTINGS_APPROVAL_WORKFLOWS"] = [new("ManualReassign", "إعادة تسكين يدوي", "Manual reassign", ScreenAction.Approve)]
    };

    public static ButtonDefinition? Find(string screenCode, string buttonCode) =>
        Buttons.TryGetValue(screenCode, out var buttons) ? buttons.FirstOrDefault(b => b.Code == buttonCode) : null;
}
