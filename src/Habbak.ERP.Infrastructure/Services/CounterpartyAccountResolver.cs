using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Infrastructure.Services;

/// <summary>
/// Resolves CounterpartyType.Supplier via Supplier.PayableAccountId (03-Module-Purchasing.md,
/// section 4.1), CounterpartyType.Customer via Customer.ReceivableAccountId (04-Module-Sales.md,
/// section 2.1), and CounterpartyType.Employee via the company-wide CompanyAccountMapping role
/// EmployeeReceivable (HR-MASTER-PLAN.md §Phase 1.3) — unlike Customer/Supplier, an employee has
/// no subsidiary account of its own; every employee shares one company-level control account, and
/// per-employee detail comes from the transaction lines themselves (EmployeeAdvance/PayrollLine),
/// not a separate GL account.
/// </summary>
public class CounterpartyAccountResolver(IApplicationDbContext db) : ICounterpartyAccountResolver
{
    public async Task<long> ResolveAccountIdAsync(
        CounterpartyType counterpartyType, long counterpartyId, CancellationToken cancellationToken = default)
    {
        if (counterpartyType == CounterpartyType.Supplier)
        {
            var payableAccountId = await db.Suppliers
                .Where(s => s.Id == counterpartyId)
                .Select(s => s.PayableAccountId)
                .FirstOrDefaultAsync(cancellationToken);

            return payableAccountId
                ?? throw new BusinessRuleException(
                    "ACC-SUPPLIER-NO-PAYABLE-ACCOUNT",
                    "لا يمكن ترحيل السند — هذا المورد ليس له حساب دائنية محدد (PayableAccountId) في بيانات المورد.");
        }

        if (counterpartyType == CounterpartyType.Customer)
        {
            var receivableAccountId = await db.Customers
                .Where(c => c.Id == counterpartyId)
                .Select(c => c.ReceivableAccountId)
                .FirstOrDefaultAsync(cancellationToken);

            return receivableAccountId
                ?? throw new BusinessRuleException(
                    "ACC-CUSTOMER-NO-RECEIVABLE-ACCOUNT",
                    "لا يمكن ترحيل السند — هذا العميل ليس له حساب مدينية محدد (ReceivableAccountId) في بياناته.");
        }

        if (counterpartyType == CounterpartyType.Employee)
        {
            var employeeReceivableAccountId = await db.CompanyAccountMappings
                .Where(m => m.Role == CompanyAccountRole.EmployeeReceivable)
                .Select(m => (long?)m.AccountId)
                .FirstOrDefaultAsync(cancellationToken);

            return employeeReceivableAccountId
                ?? throw new BusinessRuleException(
                    "ACC-EMPLOYEE-ACCOUNT-NOT-MAPPED",
                    "لا يمكن ترحيل السند — حساب ذمم الموظفين (EmployeeReceivable) لم يُربط بعد من شاشة حسابات الترحيل الافتراضية.");
        }

        throw new BusinessRuleException(
            "ACC-COUNTERPARTY-RESOLUTION-PENDING",
            $"ترحيل السندات على أطراف من نوع {counterpartyType} يحتاج ربط الحساب الفرعي من الموديول المصدر " +
            "(شئون العاملين) — غير متاح بعد. استخدم نوع الطرف \"أخرى\" مع تحديد حساب مباشر.");
    }
}
