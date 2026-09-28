using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;

namespace Habbak.ERP.Domain.Inventory;

/// <summary>
/// مسؤول عهدة نقل مخزني (02-Module-Inventory-Manufacturing.md, section 2.3) — المسؤولية عن بضاعة
/// فعلية أثناء النقل بين مخزنين، لغرض المساءلة عن أي عجز يحصل في الطريق. مفهوم مختلف تمامًا عن
/// CustodyRegister في موديول الحسابات (اللي بيدير العُهد النقدية).
///
/// الكيان اتبنى الأول بنفس شكل ILookupEntity القياسي (Code/NameAr/NameEn) قبل ما موديول الموارد
/// البشرية يوجد؛ الاسم فضل مخزن مباشرة بدل ما يتجاب من جدول موظفين، عشان أي عهدة قديمة تفضل قابلة
/// للقراءة حتى لو الربط بالموظف لسه ما اتعملش. <see cref="EmployeeId"/> اتضاف في Phase 1.2
/// (HR-MASTER-PLAN.md §Phase 1.2) كعمود اختياري جنب الاسم النصي، مش بديل عنه — الربط يدوي أو
/// بمطابقة اسم بيقين بس (FreeFieldDiagnosticReport)، مفيش مسح أو استبدال لأي بيانات قديمة.
/// </summary>
public class CustodyOfficer : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    /// <summary>Optional link to the real employee record — set manually, or by an exact-name auto-match (Phase 1.2). Null until then.</summary>
    public long? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
}
