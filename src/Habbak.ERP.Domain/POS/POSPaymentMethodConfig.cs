using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

/// <summary>إعداد طريقة دفع لنقطة بيع (05-Module-POS-Shifts.md, section 2.3) — يحدّد إيه طرق
/// الدفع المتاحة فعليًا على جهاز بعينه. LinkedTreasuryAccountId مُخزَّن زي ما تنص المواصفة (قاعدة
/// 25) لكن مالوش مستهلك فعلي بعد — نفس تأجيل IPostingService/JournalEntryId المُتَّبع في كل
/// مستندات الموديول لحد الآن.</summary>
public class POSPaymentMethodConfig : AuditableEntity
{
    public long POSTerminalId { get; set; }
    public POSTerminal? POSTerminal { get; set; }

    public long PaymentMethodId { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }

    public bool IsEnabled { get; set; } = true;

    public long LinkedTreasuryAccountId { get; set; }
    public Account? LinkedTreasuryAccount { get; set; }
}
