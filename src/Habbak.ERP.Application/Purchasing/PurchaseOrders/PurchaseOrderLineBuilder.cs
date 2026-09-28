using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Dtos;
using Habbak.ERP.Domain.Purchasing;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders;

/// <summary>Shared between Create/Update — builds numbered lines from the caller's input and
/// returns the resulting Subtotal (Σ per-line TotalPrice, itself Quantity × UnitPrice − line
/// discount) so both commands compute totals identically. Every line's unit is checked (and its
/// base figures computed) here, before the callers touch the stored lines.</summary>
internal static class PurchaseOrderLineBuilder
{
    public static (List<PurchaseOrderLine> Lines, decimal Subtotal) Build(IReadOnlyList<PurchaseOrderLineInput> inputs, ItemUnits units)
    {
        var lines = new List<PurchaseOrderLine>();
        var subtotal = 0m;
        var lineNumber = 1;

        foreach (var input in inputs)
        {
            var totalPrice = (input.Quantity * input.UnitPrice) - (input.DiscountAmount ?? 0);
            var unit = units.Resolve(input.ItemId, input.UnitId, PurchaseUnits.NotAllowed);

            lines.Add(new PurchaseOrderLine
            {
                LineNumber = lineNumber++,
                ItemId = input.ItemId,
                PurchaseRequestLineId = input.PurchaseRequestLineId,
                Quantity = input.Quantity,
                UnitPrice = input.UnitPrice,
                TotalPrice = totalPrice,
                DiscountAmount = input.DiscountAmount,
                UnitId = unit.UnitId,
                UnitFactor = unit.Factor,
                BaseQuantity = ItemUnits.ToBase(input.Quantity, unit.Factor),
                BaseUnitCost = ItemUnits.CostPerBase(input.UnitPrice, unit.Factor),
                ExpectedDeliveryDate = input.ExpectedDeliveryDate,
                Weight = input.Weight
            });

            subtotal += totalPrice;
        }

        return (lines, subtotal);
    }
}
