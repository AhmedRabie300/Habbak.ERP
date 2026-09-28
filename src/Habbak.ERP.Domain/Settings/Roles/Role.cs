using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Settings;

/// <summary>A named set of permissions within one company. System roles (<see cref="SystemRoles"/>) are seeded per company and cannot be deleted or edited.</summary>
public class Role : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<ScreenPermission> ScreenPermissions { get; set; } = new List<ScreenPermission>();
    public ICollection<FieldPermission> FieldPermissions { get; set; } = new List<FieldPermission>();
    public ICollection<ButtonPermission> ButtonPermissions { get; set; } = new List<ButtonPermission>();
}

/// <summary>The eight roles every company starts with.</summary>
public static class SystemRoles
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string CompanyAdmin = "COMPANY_ADMIN";
    public const string BranchManager = "BRANCH_MANAGER";
    public const string Cashier = "CASHIER";
    public const string Accountant = "ACCOUNTANT";
    public const string StoreKeeper = "STORE_KEEPER";
    public const string PurchasingOfficer = "PURCHASING_OFFICER";
    public const string SalesRep = "SALES_REP";

    public static readonly IReadOnlyList<(string Code, string NameAr, string NameEn)> All =
    [
        (SuperAdmin, "مدير النظام", "System Administrator"),
        (CompanyAdmin, "مدير الشركة", "Company Administrator"),
        (BranchManager, "مدير فرع", "Branch Manager"),
        (Cashier, "كاشير", "Cashier"),
        (Accountant, "محاسب", "Accountant"),
        (StoreKeeper, "أمين مخزن", "Store Keeper"),
        (PurchasingOfficer, "مسؤول مشتريات", "Purchasing Officer"),
        (SalesRep, "مندوب مبيعات", "Sales Representative")
    ];
}
