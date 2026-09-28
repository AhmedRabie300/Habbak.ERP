using Habbak.ERP.Application.Sales.DeliveryOrders.Dtos;
using Habbak.ERP.Domain.Sales;

namespace Habbak.ERP.Application.Sales.DeliveryOrders;

/// <summary>Shared between Create/Update — builds numbered lines from the caller's input, same
/// pattern as PurchaseOrderLineBuilder (no pricing here — DeliveryOrder only moves quantities).</summary>
internal static class DeliveryOrderLineBuilder
{
    public static List<DeliveryOrderLine> Build(IReadOnlyList<DeliveryOrderLineInput> inputs)
    {
        var lines = new List<DeliveryOrderLine>();
        var lineNumber = 1;

        foreach (var input in inputs)
        {
            lines.Add(new DeliveryOrderLine
            {
                LineNumber = lineNumber++,
                ItemId = input.ItemId,
                Quantity = input.Quantity,
                BatchNumber = input.BatchNumber
            });
        }

        return lines;
    }
}
