using Habbak.ERP.Application.Sales.SalesOrders.Dtos;
using Habbak.ERP.Domain.Sales;

namespace Habbak.ERP.Application.Sales.SalesOrders;

/// <summary>Shared between Create/Update — builds numbered lines from the caller's input and
/// returns the resulting Subtotal, same pattern as PurchaseOrderLineBuilder.</summary>
internal static class SalesOrderLineBuilder
{
    public static (List<SalesOrderLine> Lines, decimal Subtotal) Build(IReadOnlyList<SalesOrderLineInput> inputs)
    {
        var lines = new List<SalesOrderLine>();
        var subtotal = 0m;
        var lineNumber = 1;

        foreach (var input in inputs)
        {
            var lineTotal = input.Quantity * input.UnitPrice;

            lines.Add(new SalesOrderLine
            {
                LineNumber = lineNumber++,
                ItemId = input.ItemId,
                Quantity = input.Quantity,
                UnitPrice = input.UnitPrice,
                LineTotal = lineTotal,
                DeliveredQuantity = 0
            });

            subtotal += lineTotal;
        }

        return (lines, subtotal);
    }
}
