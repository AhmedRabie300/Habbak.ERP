using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;

namespace Habbak.ERP.Infrastructure.Services;

/// <summary>
/// Placeholder until the Sales/Purchasing/HR modules exist and own Customer/Supplier/Employee
/// subsidiary-account mapping. Posting a voucher against one of those counterparty types fails
/// clearly instead of silently resolving to a wrong account; CounterpartyType.Other (which
/// carries its own DirectAccountId) is unaffected and posts normally today.
/// </summary>
public class NotImplementedCounterpartyAccountResolver : ICounterpartyAccountResolver
{
    public Task<long> ResolveAccountIdAsync(
        CounterpartyType counterpartyType, long counterpartyId, CancellationToken cancellationToken = default) =>
        throw new BusinessRuleException(
            "ACC-COUNTERPARTY-RESOLUTION-PENDING",
            $"ترحيل السندات على أطراف من نوع {counterpartyType} يحتاج ربط الحساب الفرعي من الموديول المصدر " +
            "(المبيعات/المشتريات/شئون العاملين) — غير متاح بعد. استخدم نوع الطرف \"أخرى\" مع تحديد حساب مباشر.");
}
