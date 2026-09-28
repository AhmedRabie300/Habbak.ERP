using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

/// <summary>قاعدة 18 / 00-Project-Overview.md قسم 14.3: تذكرة ذاتية الاحتواء — الأصناف وكمياتها
/// وأسعارها بتتسجّل هنا وقت الإصدار، والـQR نفسه (في الاستخدام الحقيقي) بيحمل IdempotencyKey
/// التذكرة كاملًا كـPayload مشفّر جوّاه، عشان الجهاز القارئ يقدر يتأكد من عدم الاستخدام المزدوج
/// حتى لو Offline وقت المسح. مفيش موديول "استشاري تصنيع" حقيقي لسه بيولّد التذاكر دي فعليًا —
/// GenerateQRTicketCommand هنا بيمثّل نقطة الإصدار البديلة لحد ما الموديول ده يتبني.</summary>
public enum QRTicketStatus
{
    Active = 1,
    Redeemed = 2
}

public class QRTicket : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public Guid IdempotencyKey { get; set; }
    public QRTicketStatus Status { get; set; } = QRTicketStatus.Active;

    public long? RedeemedByCheckId { get; set; }
    public Check? RedeemedByCheck { get; set; }
    public DateTime? RedeemedAtUtc { get; set; }

    public ICollection<QRTicketLine> Lines { get; set; } = new List<QRTicketLine>();
}

public class QRTicketLine : AuditableEntity
{
    public long QRTicketId { get; set; }
    public QRTicket? QRTicket { get; set; }

    public int LineNumber { get; set; }
    public long ItemId { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
