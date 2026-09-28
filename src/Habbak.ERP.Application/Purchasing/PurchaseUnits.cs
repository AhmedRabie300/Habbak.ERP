namespace Habbak.ERP.Application.Purchasing;

/// <summary>
/// The unit on purchasing lines (Fixes-Batch-2026-09-19): the item's base unit or one of its
/// ItemUnitConversion units, checked with Inventory.Common.ItemUnits. Each line keeps its quantity
/// and price in that unit plus UnitFactor (snapshot), BaseQuantity and BaseUnitCost; stock moves by
/// the base figures. Before this, the unit was stored but never converted — receiving "2 bags" added
/// 2 base units to stock.
/// </summary>
public static class PurchaseUnits
{
    public const string NotAllowed = "PUR-UNIT-NOT-ALLOWED";
}
