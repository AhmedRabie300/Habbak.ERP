namespace Habbak.ERP.Domain.HR;

// Docs/Modules/10-Module-HR-Payroll.md §2.6 + Docs/Implementation/HR-Core-Plan.md §1.1 (Gender/
// MaritalStatus are plain enums, not ILookupEntity — no real scenario for adding a value later).

public enum EmployeeStatus { Draft = 1, Active = 2, OnLeave = 3, Suspended = 4, Terminated = 5 }

public enum EmploymentType { FullTime = 1, PartTime = 2, Temporary = 3 }

public enum Gender { Male = 1, Female = 2 }

public enum MaritalStatus { Single = 1, Married = 2, Divorced = 3, Widowed = 4 }

// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B3 — EmploymentContract's own type/status, distinct
// from EmployeeStatus (that one tracks the person; this one tracks the contract document).
public enum ContractType { FixedTerm = 1, Indefinite = 2 }

// Rejected = 5 added Docs/Implementation/Phase-2-Research.md §3.2 — §12.4 rule 6 requires every
// entity that can go through an approval chain to carry Rejected explicitly; EmploymentContract's
// ApprovalInstanceId (schema-only until Phase 2) had no matching status value until now.
public enum EmploymentContractStatus { Draft = 1, Active = 2, Expired = 3, Terminated = 4, Rejected = 5 }

// Docs/Modules/10-Module-HR-Payroll.md §2.6 — مشترك بين LeaveRequest/OvertimeRequest (Phase 3) وهيتشارك
// لاحقًا مع EmployeeAdvance/EmployeePenalty/EmployeeBonus (Phase 5)، فمكانه هنا مش
// Domain/Attendance/Enums.cs (Docs/Implementation/Phase-3-Research.md §3.5).
public enum HrRequestStatus { Draft = 1, Pending = 2, Approved = 3, Rejected = 4, Cancelled = 5 }

// Docs/Implementation/Phase-3C-Research.md §3.1 — EmploymentContractLine is a descriptive line on top
// of BasicSalary (housing/transport allowances etc.), not a payroll calculation engine; that's the
// future SalaryComponent (Phase 4), deliberately kept separate.
public enum ContractLineType { Earning = 1, Deduction = 2 }
