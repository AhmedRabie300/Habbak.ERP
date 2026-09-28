namespace Habbak.ERP.Domain.Common;

/// <summary>00-Project-Overview.md, section 14.1 — كل عملية حساسة معرَّضة لإعادة الإرسال (Retry
/// تلقائي، أو مزامنة معاملة Offline) تحمل IdempotencyKey يتولّد وقت الإنشاء الأول فقط. لو نفس
/// المفتاح وصل تاني، السيرفر يرجّع نفس النتيجة المخزَّنة هنا بدل إعادة التنفيذ. الاحتفاظ 7 أيام
/// (قسم 14.1) — تنظيف المنتهي مؤجَّل (يحتاج مهمة مجدولة، قسم 21 غير مبني بعد لأي موديول).</summary>
public class ProcessedIdempotencyKey : AuditableEntity
{
    public Guid IdempotencyKey { get; set; }
    public string OperationType { get; set; } = null!;
    public string ResultJson { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
}
