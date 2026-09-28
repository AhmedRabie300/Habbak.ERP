using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Posting.Resolvers;

/// <summary>
/// Works out an account from the document when the template cannot name one up front — the
/// supplier's own payable account, for instance. The key is what a template line stores.
///
/// Adding a resolver means adding a class; they are picked up by assembly scanning (spec 4.4).
/// There is deliberately no way to add one from the settings screen.
/// </summary>
public interface IPostingAccountResolver
{
    string Key { get; }

    /// <summary>The document field it reads; a screen that does not supply it cannot use this resolver.</summary>
    string RequiredField { get; }

    Task<long?> ResolveAsync(PostingContext context, CancellationToken cancellationToken);
}

/// <summary>Works out a cost-center value for a template line, e.g. the one mirroring the branch.</summary>
public interface IPostingCostCenterResolver
{
    string Key { get; }

    /// <summary>The document field it reads; a screen that does not supply it cannot use this resolver.</summary>
    string RequiredField { get; }

    /// <summary>
    /// The kind of dimension this resolver can answer for, when it only makes sense for one. Checked
    /// when a template is saved, so pointing a branch resolver at a non-branch dimension is refused
    /// then rather than failing on the first real posting.
    /// </summary>
    CostCenterLinkedEntityType? RequiresLinkedEntityType { get; }

    Task<long?> ResolveAsync(PostingContext context, long costCenterDimensionId, CancellationToken cancellationToken);
}

public interface IPostingResolverRegistry
{
    IPostingAccountResolver? FindAccountResolver(string key);
    IPostingCostCenterResolver? FindCostCenterResolver(string key);
    IReadOnlyCollection<string> AccountResolverKeys { get; }
    IReadOnlyCollection<string> CostCenterResolverKeys { get; }
}

internal sealed class PostingResolverRegistry : IPostingResolverRegistry
{
    private readonly Dictionary<string, IPostingAccountResolver> _accounts;
    private readonly Dictionary<string, IPostingCostCenterResolver> _costCenters;

    public PostingResolverRegistry(
        IEnumerable<IPostingAccountResolver> accountResolvers,
        IEnumerable<IPostingCostCenterResolver> costCenterResolvers)
    {
        // ToDictionary throws on a duplicate key: two resolvers answering to one key is a coding
        // mistake that must fail the first request, not resolve to whichever registered last.
        _accounts = accountResolvers.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);
        _costCenters = costCenterResolvers.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IPostingAccountResolver? FindAccountResolver(string key) => _accounts.GetValueOrDefault(key);
    public IPostingCostCenterResolver? FindCostCenterResolver(string key) => _costCenters.GetValueOrDefault(key);
    public IReadOnlyCollection<string> AccountResolverKeys => _accounts.Keys;
    public IReadOnlyCollection<string> CostCenterResolverKeys => _costCenters.Keys;
}

/// <summary>
/// The supplier's own payable account, falling back to the company default. That fallback is exactly
/// what Supplier's own doc comment anticipated "a future company-settings screen" would add.
/// </summary>
internal sealed class SupplierPayableAccountResolver(IApplicationDbContext db) : IPostingAccountResolver
{
    public string Key => "Supplier.PayableAccountId";

    public string RequiredField => "SupplierId";

    public async Task<long?> ResolveAsync(PostingContext context, CancellationToken cancellationToken)
    {
        var supplierId = context.GetLong("SupplierId");
        if (supplierId is null)
        {
            return null;
        }

        var own = await db.Suppliers
            .Where(s => s.Id == supplierId)
            .Select(s => s.PayableAccountId)
            .FirstOrDefaultAsync(cancellationToken);

        return own ?? await CompanyAccountLookup.FindAsync(db, CompanyAccountRole.DefaultPayable, cancellationToken);
    }
}

/// <summary>The customer's own receivable account, falling back to the company default.</summary>
internal sealed class CustomerReceivableAccountResolver(IApplicationDbContext db) : IPostingAccountResolver
{
    public string Key => "Customer.ReceivableAccountId";

    public string RequiredField => "CustomerId";

    public async Task<long?> ResolveAsync(PostingContext context, CancellationToken cancellationToken)
    {
        var customerId = context.GetLong("CustomerId");
        if (customerId is null)
        {
            return null;
        }

        var own = await db.Customers
            .Where(c => c.Id == customerId)
            .Select(c => c.ReceivableAccountId)
            .FirstOrDefaultAsync(cancellationToken);

        return own ?? await CompanyAccountLookup.FindAsync(db, CompanyAccountRole.DefaultReceivable, cancellationToken);
    }
}

/// <summary>
/// The cost-center value standing for the document's branch. Branch-linked dimensions keep one value
/// per branch in sync automatically, keyed by the branch code (BranchDimensionSync), so nobody has to
/// maintain this mapping by hand.
/// </summary>
internal sealed class BranchCostCenterResolver(IApplicationDbContext db) : IPostingCostCenterResolver
{
    public string Key => "Branch.ToCostCenterValue";

    public string RequiredField => "BranchId";

    public CostCenterLinkedEntityType? RequiresLinkedEntityType => CostCenterLinkedEntityType.Branch;

    public async Task<long?> ResolveAsync(PostingContext context, long costCenterDimensionId, CancellationToken cancellationToken)
    {
        var branchId = context.GetLong("BranchId");
        if (branchId is null)
        {
            return null;
        }

        var branchCode = await db.Branches
            .Where(b => b.Id == branchId)
            .Select(b => b.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (branchCode is null)
        {
            return null;
        }

        return await db.CostCenterDimensionValues
            .Where(v => v.CostCenterDimensionId == costCenterDimensionId && v.Code == branchCode && v.IsActive)
            .Select(v => (long?)v.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

/// <summary>
/// The treasury a terminal's cash drawer posts to — where refunds come out of and shift differences
/// are settled against.
///
/// Payment methods carry no "is cash" flag; the POS already treats a payment with an amount tendered
/// as cash (CloseShiftCommand's expected-cash rule). So the drawer's treasury is the one linked to the
/// method this terminal last took tendered cash with. A terminal that has never taken cash falls
/// back to the company's Cash role, so the entry still says where it went instead of failing.
/// </summary>
internal sealed class TerminalCashTreasuryResolver(IApplicationDbContext db) : IPostingAccountResolver
{
    public string Key => "POSTerminal.CashTreasuryAccount";

    public string RequiredField => "POSTerminalId";

    public async Task<long?> ResolveAsync(PostingContext context, CancellationToken cancellationToken)
    {
        var terminalId = context.GetLong("POSTerminalId");
        if (terminalId is null)
        {
            return null;
        }

        var cashMethodId = await db.POSPayments
            .Where(p => p.POSInvoice!.POSTerminalId == terminalId && p.AmountTendered != null)
            .OrderByDescending(p => p.Id)
            .Select(p => (long?)p.PaymentMethodId)
            .FirstOrDefaultAsync(cancellationToken);

        if (cashMethodId is not null)
        {
            var treasury = await db.POSPaymentMethodConfigs
                .Where(c => c.POSTerminalId == terminalId && c.PaymentMethodId == cashMethodId)
                .Select(c => (long?)c.LinkedTreasuryAccountId)
                .FirstOrDefaultAsync(cancellationToken);
            if (treasury is not null)
            {
                return treasury;
            }
        }

        return await CompanyAccountLookup.FindAsync(db, CompanyAccountRole.Cash, cancellationToken);
    }
}

internal static class CompanyAccountLookup
{
    public static Task<long?> FindAsync(IApplicationDbContext db, CompanyAccountRole role, CancellationToken cancellationToken) =>
        db.CompanyAccountMappings
            .Where(m => m.Role == role)
            .Select(m => (long?)m.AccountId)
            .FirstOrDefaultAsync(cancellationToken);
}
