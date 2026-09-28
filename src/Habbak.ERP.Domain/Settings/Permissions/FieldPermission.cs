using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Settings;

/// <summary>
/// Whether a role can see or change one sensitive field <b>on one screen</b>
/// (<see cref="FieldPermissionCatalog"/>). The same field can be open on one screen and hidden on
/// another — a cashier may look a customer's credit limit up on the customers screen but not have it
/// in front of them while invoicing. No row = no restriction on that screen.
/// </summary>
public class FieldPermission : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }
    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;

    /// <summary>MenuItem code of the screen the rule applies on.</summary>
    public string ScreenCode { get; set; } = null!;

    public string EntityType { get; set; } = null!;
    public string FieldName { get; set; } = null!;
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    public bool RequiresAuditLog { get; set; } = true;
}

/// <summary>
/// Screen → entity → the sensitive fields that screen shows. Only screens that exist and actually
/// return these fields are listed: a rule on a screen that never shows the field would be a switch
/// wired to nothing. Every field listed here is also written to the audit log as
/// <see cref="AuditLog.Redacted"/>, whichever screen changed it.
/// </summary>
public static class FieldPermissionCatalog
{
    private static readonly string[] ContactAndCredit = ["CreditLimit", "Phone", "Email"];

    // Docs/Implementation/HR-Core-Plan.md §0.4/§1.1 (Batch B2) — every [PiiField] property on
    // EmployeePersonalData, registered the same batch the entity was added, not deferred. Last4/
    // BankName/lookup FKs are deliberately excluded (not [PiiField] — see the entity's own doc comment).
    private static readonly string[] EmployeePersonalDataPii =
    [
        "NationalIdEncrypted", "NationalIdHash", "BankIbanEncrypted", "BirthDate", "Address",
        "PhoneNumber", "PersonalEmail", "EmergencyContactName", "EmergencyContactPhone"
    ];

    // Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.6 — rule 6 covers "PII أو أجر",
    // not just PII; these are wage/compensation figures, not personal data, so — same as
    // ContactAndCredit above — none of them are [PiiField] (that attribute is reserved for genuinely
    // personal fields; the catalog itself doesn't require it, PiiFieldRegistrationTests.cs only
    // checks the attribute→catalog direction, not the reverse).
    private static readonly string[] LegalTableValues = ["Amount", "EmployeeRate", "EmployerRate", "MinWage", "MaxWage", "PersonalExemption", "Rate", "MaxDaysPerMonth"];

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string[]>> SensitiveFields =
        new Dictionary<string, IReadOnlyDictionary<string, string[]>>
        {
            ["SALES_CUSTOMERS"] = new Dictionary<string, string[]> { ["Customer"] = ContactAndCredit },
            ["SALES_INVOICES"] = new Dictionary<string, string[]>
            {
                ["Customer"] = ContactAndCredit,
                ["SalesInvoice"] = ["DiscountAmount"]
            },
            ["PURCHASING_SUPPLIERS"] = new Dictionary<string, string[]> { ["Supplier"] = ContactAndCredit },
            ["POS_SHIFT_CONSOLE"] = new Dictionary<string, string[]> { ["Shift"] = ["ExpectedClosingCashAmount"] },
            ["POS_SHIFTS"] = new Dictionary<string, string[]> { ["Shift"] = ["ExpectedClosingCashAmount"] },
            ["POS_TABLE_BOARD"] = new Dictionary<string, string[]> { ["POSPayment"] = ["CardTransactionReference"] },
            ["POS_RETURNS"] = new Dictionary<string, string[]> { ["POSPayment"] = ["CardTransactionReference"] },
            ["HR_EMPLOYEES"] = new Dictionary<string, string[]> { ["EmployeePersonalData"] = EmployeePersonalDataPii },

            // Payroll wage fields (rule 6: "أي حقل PII أو أجر").
            ["HR_SALARY_CHANGES"] = new Dictionary<string, string[]> { ["EmployeeSalary"] = ["Amount"] },
            ["PAY_PAYROLL_RUNS"] = new Dictionary<string, string[]>
            {
                ["PayrollLine"] = ["Amount"],
                ["PayrollRun"] = ["TotalGross", "TotalDeductions", "TotalNet", "TotalEmployerCost"]
            },
            ["PAY_PAYSLIPS"] = new Dictionary<string, string[]> { ["Payslip"] = ["Gross", "TotalDeductions", "Net"] },
            ["PAY_LEGAL_TABLES"] = new Dictionary<string, string[]>
            {
                ["MinimumWage"] = LegalTableValues,
                ["SocialInsuranceRate"] = LegalTableValues,
                ["InsurableWageLimit"] = LegalTableValues,
                ["PayrollTaxBracketSet"] = LegalTableValues,
                ["PayrollTaxBracket"] = LegalTableValues,
                ["MartyrsFundRate"] = LegalTableValues,
                ["PenaltyDeductionCap"] = LegalTableValues
            }
        };

    /// <summary>Sensitive on any screen — decides audit-log redaction.</summary>
    public static bool IsSensitive(string entityType, string fieldName) =>
        SensitiveFields.Values.Any(entities => entities.TryGetValue(entityType, out var fields) && fields.Contains(fieldName));

    public static bool IsListed(string screenCode, string entityType, string fieldName) =>
        SensitiveFields.TryGetValue(screenCode, out var entities)
        && entities.TryGetValue(entityType, out var fields)
        && fields.Contains(fieldName);

    /// <summary>The fields of an entity that some screen lists — the names response masking looks for.</summary>
    public static IEnumerable<string> FieldsOf(string entityType) =>
        SensitiveFields.Values.SelectMany(e => e.TryGetValue(entityType, out var f) ? f : []).Distinct();
}
