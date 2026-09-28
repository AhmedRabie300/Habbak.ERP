using Habbak.ERP.Application.Sales.SalesReturns.Dtos;
using Habbak.ERP.Domain.Sales;

namespace Habbak.ERP.Application.Sales.SalesReturns;

/// <summary>Shared between Create/Update — builds numbered lines from the caller's input, same
/// pattern as PurchaseOrderLineBuilder.</summary>
internal static class SalesReturnLineBuilder
{
    public static List<SalesReturnLine> Build(IReadOnlyList<SalesReturnLineInput> inputs)
    {
        var lines = new List<SalesReturnLine>();
        var lineNumber = 1;

        foreach (var input in inputs)
        {
            lines.Add(new SalesReturnLine
            {
                LineNumber = lineNumber++,
                ItemId = input.ItemId,
                Quantity = input.Quantity,
                UnitPrice = input.UnitPrice,
                BatchNumber = input.BatchNumber
            });
        }

        return lines;
    }
}
