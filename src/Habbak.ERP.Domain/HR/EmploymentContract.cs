using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.HR;

/// <summary>
/// Docs/Modules/10-Module-HR-Payroll.md §2.1, Batch B3. First real use of IEmployeeScopedEntity
/// (built in Phase 0.1, unused until now) — a future Self-service screen gets its "only my own
/// records" filter for free from AppDbContext.OnModelCreating's generic reflection loop, no extra
/// code (Docs/Implementation/HR-Core-Plan.md §1.1 design note). PreviousContractId links a renewal
/// to its predecessor — a plain chain, not a hierarchy, so unlike OrgUnit.ParentId/Employee.ManagerId
/// it needs no cycle check. ApprovalInstanceId is schema-only for now, same as
/// PurchaseRequest/Employee (risk 6 — NullApprovalWorkflowService, no approval engine yet).
/// BranchId is required at the database level (.IsRequired() in configuration) but typed nullable in
/// C# to satisfy IBranchScopedEntity, mirroring Employee/Shift/POSTerminal.
/// </summary>
public class EmploymentContract : AuditableEntity, ICompanyScopedEntity, IBranchScopedEntity, IEmployeeScopedEntity
{
    public long? CompanyId { get; set; }
    public long? BranchId { get; set; }

    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public ContractType ContractType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly? ProbationEndDate { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal InsurableWage { get; set; }
    public int WorkingHoursPerDay { get; set; }
    public EmploymentContractStatus Status { get; set; } = EmploymentContractStatus.Draft;

    public long? PreviousContractId { get; set; }
    public EmploymentContract? PreviousContract { get; set; }

    public long? ApprovalInstanceId { get; set; }

    /// <summary>Phase 3C, Remarks8 item 2 — same single-scalar-FK pattern as EmployeeDocument.AttachmentId,
    /// but nullable: the contract is created before any file exists and gets one attached later via
    /// SetEmploymentContractAttachmentCommand, not through the general Update command.</summary>
    public long? AttachmentId { get; set; }
    public Attachment? Attachment { get; set; }

    /// <summary>Phase 3C, Remarks8 items 1 &amp; 4 — allowances/deductions on top of BasicSalary (e.g.
    /// housing, transport). Replace-All on every Create/Update/Renew, same as BranchRequestLine.</summary>
    public ICollection<EmploymentContractLine> Lines { get; set; } = new List<EmploymentContractLine>();
}
