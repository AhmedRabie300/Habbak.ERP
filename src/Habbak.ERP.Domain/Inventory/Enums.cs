namespace Habbak.ERP.Domain.Inventory;

public enum ItemType
{
    RawMaterial = 1,
    SemiFinished = 2,
    FinishedGood = 3,
    Consumable = 4,
    Service = 5
}

/// <summary>Determines whether a scale integration is required at the POS (00-Project-Overview.md, section 5.1/5.4).</summary>
public enum SaleMethod
{
    ByPiece = 1,
    ByWeight = 2,
    ByVolume = 3
}

public enum CostMethod
{
    WeightedAverage = 1,
    Fifo = 2
}

/// <summary>Richer than the plain IsActive flag ILookupEntity requires — UnderReview items are
/// excluded from new-record pickers exactly like Inactive ones (rule 26), but stay distinguishable
/// from a deliberately deactivated item.</summary>
public enum ItemStatus
{
    Active = 1,
    UnderReview = 2,
    Inactive = 3
}

public enum WarehouseType
{
    Main = 1,
    BranchMaterials = 2,
    Production = 3,
    DamagedReturns = 4,
    FinishedGoods = 5
}

/// <summary>Informational classification of a unit of measure — matches the reference mockup's
/// item card at the user's explicit request. Purely descriptive: the actual conversion factor
/// between two units of measure stays per-item (ItemUnitConversion, rule 32).</summary>
public enum UnitCategory
{
    Weight = 1,
    Volume = 2,
    Count = 3
}

/// <summary>Inbound: Purchase, TransferIn, ProductionReceipt, AdjustmentIn, OpeningBalance.
/// Outbound: POSSale, TransferOut, ProductionIssue, AdjustmentOut, Waste, PurchaseReturn
/// (section 2.2; PurchaseReturn added for 03-Module-Purchasing.md, section 4.7 rule 7).</summary>
public enum TransactionType
{
    Purchase = 1,
    TransferIn = 2,
    ProductionReceipt = 3,
    AdjustmentIn = 4,
    OpeningBalance = 5,
    POSSale = 6,
    TransferOut = 7,
    ProductionIssue = 8,
    AdjustmentOut = 9,
    Waste = 10,
    PurchaseReturn = 11,

    /// <summary>Outbound movement posted by a Sales DeliveryOrder (04-Module-Sales.md, section
    /// 2.4) — distinct from POSSale, which belongs to the separate POS/Shifts module.</summary>
    SalesDelivery = 12,

    /// <summary>Inbound movement posted by a Sales SalesReturn (04-Module-Sales.md, section 2.4,
    /// rule 19) — the mirror image of SalesDelivery, goods physically coming back into stock.</summary>
    SalesReturn = 13,

    /// <summary>Inbound movement posted by a POSReturn (05-Module-POS-Shifts.md, section 2.3, rule
    /// 16) — distinct from SalesReturn, which belongs to the separate Sales module's own return
    /// document against a SalesInvoice, not a POSInvoice.</summary>
    POSReturn = 14,

    /// <summary>Outbound: a spare part used on a maintenance job (08-Module-Maintenance-FixedAssets, rule 16).</summary>
    MaintenanceIssue = 15
}

/// <summary>Section 4.2's state machine — Approved/PartiallyFulfilled are set immediately by
/// ApproveBranchRequestCommand; Fulfilled (reached only once the resulting TransferOrder's goods
/// are actually confirmed received) is not auto-transitioned yet — see BranchRequest's own class
/// note.</summary>
public enum BranchRequestStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    PartiallyFulfilled = 5,
    Fulfilled = 6,
    Cancelled = 7
}

/// <summary>Section 4.4's state machine — Rejected is terminal (rule 20): a rejected recipe is
/// never resubmitted, a fresh Draft version must be created instead via CreateNewRecipeVersionCommand.</summary>
public enum RecipeStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4
}

/// <summary>Section 4.5's state machine.</summary>
public enum ProductionOrderStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum InventoryCountType
{
    Full = 1,
    Partial = 2,
    Surprise = 3,
    Cyclic = 4
}

/// <summary>Section 4.3's state machine — Rejected branches off Settled (an approval chain can
/// reject the settlement instead of letting it Close), same as rule 20's other three entities.</summary>
public enum InventoryCountStatus
{
    Draft = 1,
    InProgress = 2,
    PendingSettlement = 3,
    Settled = 4,
    Closed = 5,
    Rejected = 6,
    Cancelled = 7
}

/// <summary>Rule 10: a per-line decision, independent of every other line in the same count.</summary>
public enum SettlementDecision
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>Section 2.7 — RealTime consumes RecipeLine components directly at the moment of sale;
/// Stocked checks the finished item's own StockBalance only (rule 18).</summary>
public enum ProductionSalesMode
{
    RealTime = 1,
    Stocked = 2
}

/// <summary>Priority order for resolving ProductionSalesModeSetting, most specific to least
/// specific (rule 17): Item &gt; POS &gt; Branch &gt; Company.</summary>
public enum SettingScopeType
{
    Company = 1,
    Branch = 2,
    POS = 3,
    Item = 4
}

public static class TransactionTypeExtensions
{
    private static readonly HashSet<TransactionType> InboundTypes =
    [
        TransactionType.Purchase,
        TransactionType.TransferIn,
        TransactionType.ProductionReceipt,
        TransactionType.AdjustmentIn,
        TransactionType.OpeningBalance,
        TransactionType.SalesReturn,
        TransactionType.POSReturn
    ];

    public static bool IsInbound(this TransactionType type) => InboundTypes.Contains(type);
}
