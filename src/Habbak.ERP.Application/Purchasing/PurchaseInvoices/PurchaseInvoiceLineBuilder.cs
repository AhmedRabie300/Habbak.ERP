using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Dtos;
using Habbak.ERP.Domain.Purchasing;

namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices;

/// <summary>Shared between Create/Update — builds numbered lines from the caller's input, computes
/// each line's TotalPrice (Quantity × UnitPrice − line discount) and the resulting Subtotal, then
/// spreads the invoice's AdditionalCosts across the lines per AdditionalCostAllocationMethod
/// (03-Module-Purchasing.md, section 4.5 "توزيع التكاليف الإضافية"):
/// ByValue/ByQuantity/ByWeight distribute proportionally to each line's TotalPrice/Quantity/Weight;
/// Manual uses the caller-supplied AllocationPercentage per line. The last line absorbs any rounding
/// remainder so the lines' AllocatedAdditionalCost always sums to exactly AdditionalCosts.</summary>
internal static class PurchaseInvoiceLineBuilder
{
    public static (List<PurchaseInvoiceLine> Lines, decimal Subtotal) Build(
        IReadOnlyList<PurchaseInvoiceLineInput> inputs,
        decimal additionalCosts,
        CostAllocationMethod? allocationMethod,
        ItemUnits units)
    {
        var lines = new List<PurchaseInvoiceLine>();
        var subtotal = 0m;
        var lineNumber = 1;

        foreach (var input in inputs)
        {
            var totalPrice = (input.Quantity * input.UnitPrice) - (input.DiscountAmount ?? 0);
            var unit = units.Resolve(input.ItemId, input.UnitId, PurchaseUnits.NotAllowed);

            lines.Add(new PurchaseInvoiceLine
            {
                LineNumber = lineNumber++,
                ItemId = input.ItemId,
                PurchaseOrderLineId = input.PurchaseOrderLineId,
                Quantity = input.Quantity,
                ReceivedQuantity = input.ReceivedQuantity,
                UnitPrice = input.UnitPrice,
                TotalPrice = totalPrice,
                DiscountAmount = input.DiscountAmount,
                UnitId = unit.UnitId,
                UnitFactor = unit.Factor,
                BaseQuantity = ItemUnits.ToBase(input.Quantity, unit.Factor),
                BaseUnitCost = ItemUnits.CostPerBase(input.UnitPrice, unit.Factor),
                AllocationPercentage = input.AllocationPercentage,
                Weight = input.Weight
            });

            subtotal += totalPrice;
        }

        AllocateAdditionalCosts(lines, additionalCosts, allocationMethod);

        return (lines, subtotal);
    }

    private static void AllocateAdditionalCosts(List<PurchaseInvoiceLine> lines, decimal additionalCosts, CostAllocationMethod? method)
    {
        if (lines.Count == 0 || additionalCosts == 0 || method is null)
        {
            return;
        }

        var bases = method switch
        {
            CostAllocationMethod.ByValue => lines.Select(l => l.TotalPrice).ToList(),
            CostAllocationMethod.ByQuantity => lines.Select(l => l.BaseQuantity).ToList(),
            CostAllocationMethod.ByWeight => lines.Select(l => l.Weight ?? 0).ToList(),
            CostAllocationMethod.Manual => lines.Select(l => l.AllocationPercentage ?? 0).ToList(),
            _ => lines.Select(l => l.TotalPrice).ToList()
        };

        var totalBase = bases.Sum();
        if (totalBase == 0)
        {
            return;
        }

        var allocated = 0m;
        for (var i = 0; i < lines.Count; i++)
        {
            var share = method == CostAllocationMethod.Manual
                ? additionalCosts * (bases[i] / 100m)
                : Math.Round(additionalCosts * bases[i] / totalBase, 4);

            if (i == lines.Count - 1)
            {
                share = additionalCosts - allocated;
            }

            lines[i].AllocatedAdditionalCost = share;
            allocated += share;
        }
    }
}
