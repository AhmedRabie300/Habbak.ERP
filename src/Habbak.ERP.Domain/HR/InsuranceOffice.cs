using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.HR;

/// <summary>
/// System-wide reference catalog — a Social Insurance office is a real government location, the same
/// for every company (Docs/Implementation/HR-Core-Plan.md §1.1). Unlike the other system-wide HR
/// lookups it gets a full List/Edit screen (Hybrid decision 2026-09-26), since new offices can open.
/// Consumed by EmployeeSocialInsurance (Payroll module, later phase — this entity is a lookup only).
/// </summary>
public class InsuranceOffice : AuditableEntity, ILookupEntity
{
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    /// <summary>The office's own official government registration code — distinct from our internal <see cref="Code"/>.</summary>
    public string? OfficialCode { get; set; }
    public string? Address { get; set; }
}
