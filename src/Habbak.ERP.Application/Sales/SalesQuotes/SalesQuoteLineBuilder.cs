using Habbak.ERP.Application.Sales.SalesQuotes.Dtos;
using Habbak.ERP.Domain.Sales;

namespace Habbak.ERP.Application.Sales.SalesQuotes;

/// <summary>Shared between Create/Update — builds numbered lines from the caller's input and
/// returns the resulting Subtotal, same pattern as PurchaseOrderLineBuilder.</summary>
internal static class SalesQuoteLineBuilder
{
    public static (List<SalesQuoteLine> Lines, decimal Subtotal) Build(IReadOnlyList<SalesQuoteLineInput> inputs)
    {
        var lines = new List<SalesQuoteLine>();
        var subtotal = 0m;
        var lineNumber = 1;

        foreach (var input in inputs)
        {
            var lineTotal = (input.Quantity * input.UnitPrice) - (input.DiscountAmount ?? 0);

            lines.Add(new SalesQuoteLine
            {
                LineNumber = lineNumber++,
                ItemId = input.ItemId,
                Quantity = input.Quantity,
                UnitPrice = input.UnitPrice,
                DiscountAmount = input.DiscountAmount,
                LineTotal = lineTotal
            });

            subtotal += lineTotal;
        }

        return (lines, subtotal);
    }
}
