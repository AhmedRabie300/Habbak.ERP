using Habbak.ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Reports.Queries;

/// <summary>
/// The Account entity has no "is treasury account" flag (01-Module-Accounting.md, section 2.1),
/// so "treasury accounts" are derived empirically: every account ever referenced as a
/// treasury/bank account by a Voucher, Cash Reconciliation, Bank Reconciliation, or Treasury
/// Transfer. Shared by every report that needs "all treasury/bank accounts" (Treasury Position,
/// Treasury &amp; Bank Statement) rather than re-deriving this union in each handler.
/// </summary>
internal static class TreasuryAccountsLookup
{
    public static async Task<IReadOnlyList<long>> GetTreasuryAccountIdsAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var voucherAccountIds = db.Vouchers.Select(v => v.TreasuryAccountId);
        var cashReconAccountIds = db.CashReconciliations.Select(c => c.TreasuryAccountId);
        var bankReconAccountIds = db.BankReconciliationRuns.Select(r => r.BankAccountId);
        var transferFromIds = db.TreasuryTransfers.Select(t => t.FromTreasuryAccountId);
        var transferToIds = db.TreasuryTransfers.Select(t => t.ToTreasuryAccountId);

        return await voucherAccountIds
            .Union(cashReconAccountIds)
            .Union(bankReconAccountIds)
            .Union(transferFromIds)
            .Union(transferToIds)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
