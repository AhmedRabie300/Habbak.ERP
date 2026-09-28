using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// Initial DataGrid column headers / form field labels per screen (00-System-Wide-Corrections-01.md,
/// section 4). Deliberately excludes buttons, hints, validation and Toast text — those stay in
/// react-i18next per the same document, section 4.2.
/// </summary>
public static class FieldLabelSeedData
{
    public static List<FieldLabel> Build()
    {
        var rows = new List<(string ScreenCode, string FieldCode, string Ar, string En)>
        {
            // Journal Entries — list
            ("ACCOUNTING_JOURNAL_ENTRIES", "entryNumber", "رقم القيد", "Entry No."),
            ("ACCOUNTING_JOURNAL_ENTRIES", "date", "التاريخ", "Date"),
            ("ACCOUNTING_JOURNAL_ENTRIES", "description", "البيان", "Description"),
            ("ACCOUNTING_JOURNAL_ENTRIES", "source", "المصدر", "Source"),
            ("ACCOUNTING_JOURNAL_ENTRIES", "debit", "مدين", "Debit"),
            ("ACCOUNTING_JOURNAL_ENTRIES", "credit", "دائن", "Credit"),
            ("ACCOUNTING_JOURNAL_ENTRIES", "status", "الحالة", "Status"),

            // Journal Entry — edit
            ("ACCOUNTING_JOURNAL_ENTRY_EDIT", "entryNumber", "رقم القيد", "Entry No."),
            ("ACCOUNTING_JOURNAL_ENTRY_EDIT", "date", "التاريخ", "Date"),
            ("ACCOUNTING_JOURNAL_ENTRY_EDIT", "description", "البيان", "Description"),
            ("ACCOUNTING_JOURNAL_ENTRY_EDIT", "account", "الحساب", "Account"),
            ("ACCOUNTING_JOURNAL_ENTRY_EDIT", "debit", "مدين", "Debit"),
            ("ACCOUNTING_JOURNAL_ENTRY_EDIT", "credit", "دائن", "Credit"),
            ("ACCOUNTING_JOURNAL_ENTRY_EDIT", "status", "الحالة", "Status"),

            // Receipt / Payment Vouchers — list
            ("ACCOUNTING_RECEIPT_VOUCHERS", "voucherNumber", "رقم السند", "Voucher No."),
            ("ACCOUNTING_RECEIPT_VOUCHERS", "date", "التاريخ", "Date"),
            ("ACCOUNTING_RECEIPT_VOUCHERS", "treasuryAccount", "حساب الخزينة/البنك", "Treasury/Bank Account"),
            ("ACCOUNTING_RECEIPT_VOUCHERS", "counterpartyType", "نوع الطرف المقابل", "Counterparty Type"),
            ("ACCOUNTING_RECEIPT_VOUCHERS", "amount", "المبلغ", "Amount"),
            ("ACCOUNTING_RECEIPT_VOUCHERS", "status", "الحالة", "Status"),
            ("ACCOUNTING_PAYMENT_VOUCHERS", "voucherNumber", "رقم السند", "Voucher No."),
            ("ACCOUNTING_PAYMENT_VOUCHERS", "date", "التاريخ", "Date"),
            ("ACCOUNTING_PAYMENT_VOUCHERS", "treasuryAccount", "حساب الخزينة/البنك", "Treasury/Bank Account"),
            ("ACCOUNTING_PAYMENT_VOUCHERS", "counterpartyType", "نوع الطرف المقابل", "Counterparty Type"),
            ("ACCOUNTING_PAYMENT_VOUCHERS", "amount", "المبلغ", "Amount"),
            ("ACCOUNTING_PAYMENT_VOUCHERS", "status", "الحالة", "Status"),

            // Voucher — edit (shared by receipt/payment)
            ("ACCOUNTING_VOUCHER_EDIT", "voucherNumber", "رقم السند", "Voucher No."),
            ("ACCOUNTING_VOUCHER_EDIT", "date", "التاريخ", "Date"),
            ("ACCOUNTING_VOUCHER_EDIT", "treasuryAccount", "حساب الخزينة/البنك", "Treasury/Bank Account"),
            ("ACCOUNTING_VOUCHER_EDIT", "counterpartyType", "نوع الطرف المقابل", "Counterparty Type"),
            ("ACCOUNTING_VOUCHER_EDIT", "counterpartyId", "رقم الطرف المقابل", "Counterparty Id"),
            ("ACCOUNTING_VOUCHER_EDIT", "directAccount", "حساب مباشر", "Direct Account"),
            ("ACCOUNTING_VOUCHER_EDIT", "amount", "المبلغ", "Amount"),
            ("ACCOUNTING_VOUCHER_EDIT", "status", "الحالة", "Status"),

            // Chart of Accounts
            ("ACCOUNTING_CHART_OF_ACCOUNTS", "code", "الكود", "Code"),
            ("ACCOUNTING_CHART_OF_ACCOUNTS", "nameAr", "الاسم بالعربي", "Arabic Name"),
            ("ACCOUNTING_CHART_OF_ACCOUNTS", "nameEn", "الاسم بالإنجليزي", "English Name"),
            ("ACCOUNTING_CHART_OF_ACCOUNTS", "parent", "الحساب الأب", "Parent Account"),
            ("ACCOUNTING_CHART_OF_ACCOUNTS", "accountType", "نوع الحساب", "Account Type"),
            ("ACCOUNTING_CHART_OF_ACCOUNTS", "nature", "طبيعة الحساب (مدين/دائن)", "Debit/Credit Nature"),
            ("ACCOUNTING_CHART_OF_ACCOUNTS", "isPostable", "يقبل ترحيل مباشر؟", "Accepts direct posting?"),
            ("ACCOUNTING_CHART_OF_ACCOUNTS", "isActive", "نشط؟", "Active?"),
            ("ACCOUNTING_CHART_OF_ACCOUNTS", "currencyCode", "كود العملة", "Currency Code"),
            ("ACCOUNTING_CHART_OF_ACCOUNTS", "shared", "مشترك بين كل الشركات؟", "Shared across all companies?"),

            // Analytical Dimensions
            ("ACCOUNTING_DIMENSIONS", "code", "الكود", "Code"),
            ("ACCOUNTING_DIMENSIONS", "name", "الاسم", "Name"),
            ("ACCOUNTING_DIMENSIONS", "level", "المستوى", "Level"),
            ("ACCOUNTING_DIMENSIONS", "order", "الترتيب", "Order"),
            ("ACCOUNTING_DIMENSIONS", "mandatory", "إلزامي؟", "Mandatory?"),
            ("ACCOUNTING_DIMENSIONS", "dimension", "البُعد", "Dimension"),

            // Accounting Periods
            ("ACCOUNTING_PERIODS", "from", "من", "From"),
            ("ACCOUNTING_PERIODS", "to", "إلى", "To"),
            ("ACCOUNTING_PERIODS", "status", "الحالة", "Status"),
            ("ACCOUNTING_PERIODS", "closedBy", "أُغلقت بمعرفة", "Closed by user"),

            // Custody & Settlement
            ("ACCOUNTING_CUSTODY", "employeeId", "رقم الموظف", "Employee Id"),
            ("ACCOUNTING_CUSTODY", "amount", "المبلغ", "Amount"),
            ("ACCOUNTING_CUSTODY", "date", "التاريخ", "Date"),
            ("ACCOUNTING_CUSTODY", "treasuryAccount", "حساب الخزينة", "Treasury Account"),
            ("ACCOUNTING_CUSTODY", "receivableAccount", "حساب مديونية العهدة", "Custody Receivable Account"),
            ("ACCOUNTING_CUSTODY", "settlementOn", "تسوية بتاريخ", "Settlement on"),
            ("ACCOUNTING_CUSTODY", "difference", "الفرق", "Difference"),
            ("ACCOUNTING_CUSTODY", "status", "الحالة", "Status"),

            // Cash Reconciliation
            ("ACCOUNTING_CASH_RECONCILIATIONS", "date", "التاريخ", "Date"),
            ("ACCOUNTING_CASH_RECONCILIATIONS", "treasuryAccount", "الخزينة/البنك", "Treasury/Bank"),
            ("ACCOUNTING_CASH_RECONCILIATIONS", "expectedBalance", "الرصيد الدفتري", "Book Balance"),
            ("ACCOUNTING_CASH_RECONCILIATIONS", "actualBalance", "الرصيد الفعلي", "Actual Balance"),
            ("ACCOUNTING_CASH_RECONCILIATIONS", "difference", "الفرق", "Difference"),
            ("ACCOUNTING_CASH_RECONCILIATIONS", "reason", "السبب", "Reason"),
            ("ACCOUNTING_CASH_RECONCILIATIONS", "status", "الحالة", "Status"),

            // Bank Reconciliation
            ("ACCOUNTING_BANK_RECONCILIATIONS", "bankAccount", "الحساب البنكي", "Bank Account"),
            ("ACCOUNTING_BANK_RECONCILIATIONS", "transactionType", "نوع الحركة", "Transaction Type"),
            ("ACCOUNTING_BANK_RECONCILIATIONS", "systemReference", "المرجع في النظام", "System Reference"),
            ("ACCOUNTING_BANK_RECONCILIATIONS", "statementReference", "مرجع كشف الحساب", "Statement Reference"),
            ("ACCOUNTING_BANK_RECONCILIATIONS", "value", "القيمة", "Value"),
            ("ACCOUNTING_BANK_RECONCILIATIONS", "adjustmentAmount", "قيمة فرق التسوية", "Adjustment Amount"),
            ("ACCOUNTING_BANK_RECONCILIATIONS", "adjustmentAccount", "حساب التسوية", "Adjustment Account"),
            ("ACCOUNTING_BANK_RECONCILIATIONS", "status", "الحالة", "Status"),

            // Reports
            ("ACCOUNTING_REPORTS", "asOf", "حتى تاريخ", "As of"),
            ("ACCOUNTING_REPORTS", "from", "من", "From"),
            ("ACCOUNTING_REPORTS", "to", "إلى", "To"),
            ("ACCOUNTING_REPORTS", "account", "الحساب", "Account"),
            ("ACCOUNTING_REPORTS", "openingBalance", "الرصيد الافتتاحي", "Opening Balance"),
            ("ACCOUNTING_REPORTS", "debit", "مدين", "Debit"),
            ("ACCOUNTING_REPORTS", "credit", "دائن", "Credit"),
            ("ACCOUNTING_REPORTS", "closingBalance", "الرصيد الختامي", "Closing Balance"),
            ("ACCOUNTING_REPORTS", "runningBalance", "الرصيد المتحرك", "Running Balance"),
            ("ACCOUNTING_REPORTS", "balance", "الرصيد", "Balance"),
            ("ACCOUNTING_REPORTS", "netIncome", "صافي الدخل", "Net Income"),
            ("ACCOUNTING_REPORTS", "totalAssets", "إجمالي الأصول", "Total Assets"),
            ("ACCOUNTING_REPORTS", "totalLiabilities", "إجمالي الخصوم", "Total Liabilities"),
            ("ACCOUNTING_REPORTS", "totalEquity", "إجمالي حقوق الملكية", "Total Equity"),
            ("ACCOUNTING_REPORTS", "totalRevenue", "إجمالي الإيرادات", "Total Revenue"),
            ("ACCOUNTING_REPORTS", "totalExpenses", "إجمالي المصروفات", "Total Expenses"),
            ("ACCOUNTING_REPORTS", "date", "التاريخ", "Date"),
            ("ACCOUNTING_REPORTS", "entryNumber", "رقم القيد", "Entry No."),
            ("ACCOUNTING_REPORTS", "description", "البيان", "Description")
        };

        return rows
            .Select(r => new FieldLabel
            {
                ScreenCode = r.ScreenCode,
                FieldCode = r.FieldCode,
                NameAr = r.Ar,
                NameEn = r.En
            })
            .ToList();
    }
}
