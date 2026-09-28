using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.Accounting;

/// <summary>
/// The company-level accounts the posting engine falls back to (Docs/Posting-Engine-Implementation-Plan.md,
/// stage 0): where cash sales land, where VAT is owed, where cost of sales is booked, and so on.
///
/// One row per role rather than one column per account, because the engine resolves these by key
/// (`AccountSourceType.FromCompany` + `AccountResolverKey` in 00-Posting-Engine-Architecture.md): a
/// row per role is that lookup directly, adding a role is an enum value rather than a migration,
/// and "which roles has the finance manager still not mapped" is a plain query.
///
/// Mapped by the finance manager, not by development. An unmapped role is not an error here — it
/// only becomes one when a template that needs it tries to post.
/// </summary>
public class CompanyAccountMapping : AuditableEntity, ICompanyScopedEntity
{
    public long? CompanyId { get; set; }

    public CompanyAccountRole Role { get; set; }

    public long AccountId { get; set; }
    public Account? Account { get; set; }
}

/// <summary>
/// Deliberately excludes a company-wide POS cash/card/wallet account: POS takings post to each
/// terminal's own treasury (`POSPaymentMethodConfig.LinkedTreasuryAccountId`) so every branch's cash
/// stays in its own drawer account. The spec's POS template used `Company.CashAccountId` there,
/// which would pool all branches into one.
/// </summary>
public enum CompanyAccountRole
{
    Cash = 1,
    Bank = 2,
    Inventory = 3,
    VatReceivable = 4,
    DefaultReceivable = 5,
    EmployeeReceivable = 6,
    VatPayable = 7,
    DefaultPayable = 8,
    SalesRevenue = 9,
    SalesReturn = 10,
    SalesVariance = 11,
    CostOfGoodsSold = 12,
    BankCommission = 13,
    CashShortage = 14,
    ServiceChargeRevenue = 15,
    TipsPayable = 16,
    PurchasePriceVariance = 17,
    InventoryAdjustment = 18
}

public static class CompanyAccountRoleExtensions
{
    /// <summary>
    /// The account type each role must point at. A journal entry built on a wrongly-mapped role
    /// still balances — revenue booked to an asset account is arithmetically fine and economically
    /// nonsense — so this is the one place that mistake can be caught before it reaches the ledger.
    ///
    /// Sales returns are a contra-revenue account and so sit under Revenue despite their debit nature.
    /// </summary>
    public static AccountType ExpectedAccountType(this CompanyAccountRole role) => role switch
    {
        CompanyAccountRole.Cash => AccountType.Asset,
        CompanyAccountRole.Bank => AccountType.Asset,
        CompanyAccountRole.Inventory => AccountType.Asset,
        CompanyAccountRole.VatReceivable => AccountType.Asset,
        CompanyAccountRole.DefaultReceivable => AccountType.Asset,
        CompanyAccountRole.EmployeeReceivable => AccountType.Asset,
        CompanyAccountRole.VatPayable => AccountType.Liability,
        CompanyAccountRole.DefaultPayable => AccountType.Liability,
        CompanyAccountRole.SalesRevenue => AccountType.Revenue,
        CompanyAccountRole.SalesReturn => AccountType.Revenue,
        CompanyAccountRole.SalesVariance => AccountType.Revenue,
        CompanyAccountRole.CostOfGoodsSold => AccountType.Expense,
        CompanyAccountRole.BankCommission => AccountType.Expense,
        CompanyAccountRole.CashShortage => AccountType.Expense,
        CompanyAccountRole.ServiceChargeRevenue => AccountType.Revenue,
        CompanyAccountRole.TipsPayable => AccountType.Liability,
        CompanyAccountRole.PurchasePriceVariance => AccountType.Expense,
        CompanyAccountRole.InventoryAdjustment => AccountType.Expense,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
