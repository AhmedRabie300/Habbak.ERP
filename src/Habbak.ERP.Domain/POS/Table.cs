using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.POS;

/// <summary>حالة الطرابيزة (05-Module-POS-Shifts.md, section 2.2, قاعدة 30) — Busy تتزامن إلزاميًا
/// مع وجود Check غير منتهٍ (Open/Held) مرتبط؛ الانتقال لـCleaning تلقائي لما الـCheck ينتهي،
/// والانتقال لـFree يدوي فقط بتأكيد الخدمة.</summary>
public enum TableStatus
{
    Free = 1,
    Busy = 2,
    Reserved = 3,
    Cleaning = 4
}

public class Table : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, ILookupEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    public TableStatus Status { get; set; } = TableStatus.Free;
}
