using Habbak.ERP.Domain.Accounting;

namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>
/// Resolves the ledger Account for a Voucher's Customer/Supplier/Employee counterparty
/// (01-Module-Accounting.md, Voucher.CounterpartyId — "مرجع للطرف صاحب الحساب الفرعي المعروف").
/// That subsidiary-account mapping is owned by the Sales/Purchasing/HR modules, none of which
/// exist in this solution yet — this interface is the extension seam for when they do.
/// CounterpartyType.Other never calls this: it resolves directly via Voucher.DirectAccountId.
/// </summary>
public interface ICounterpartyAccountResolver
{
    Task<long> ResolveAccountIdAsync(
        CounterpartyType counterpartyType, long counterpartyId, CancellationToken cancellationToken = default);
}
