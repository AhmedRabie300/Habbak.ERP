using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>
/// طلب توريد فرع (02-Module-Inventory-Manufacturing.md, section 2.4) — دايمًا موجّه لمخزن رئيسي
/// (WarehouseType = Main) ضمنيًا؛ الفرع لا يطلب توريد من فرع آخر مباشرة (قاعدة 5). اعتماده
/// (كليًا أو جزئيًا) بينشئ WarehouseDocument من نوع TransferOrder تلقائيًا (قاعدة 6).
///
/// ملحوظة نطاق: `Fulfilled` (قسم 4.2) بيتحقق بس لما التحويل الناتج يوصل فعليًا (TransferReceipt)
/// — الربط ده مش موجود كحقل رسمي في جدول الطلب نفسه، فالحالة دي معرّفة في الـ enum لاكتمال
/// الـ schema بس مش بتتحول لها تلقائيًا في هذه الدفعة؛ `Approved`/`PartiallyFulfilled`/`Rejected`
/// هي الحالات النهائية اللي بتتحدد فعليًا وقت الاعتماد.
/// </summary>
public class BranchRequest : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public string RequestNumber { get; set; } = null!;
    public DateOnly RequestDate { get; set; }
    public long RequestedByUserId { get; set; }

    public BranchRequestStatus Status { get; set; } = BranchRequestStatus.Draft;
    public long? ApprovalInstanceId { get; set; }

    public ICollection<BranchRequestLine> Lines { get; set; } = new List<BranchRequestLine>();
}
