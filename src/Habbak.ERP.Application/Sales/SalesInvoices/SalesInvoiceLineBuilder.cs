using Habbak.ERP.Application.Sales.SalesInvoices.Dtos;
using Habbak.ERP.Domain.Sales;

namespace Habbak.ERP.Application.Sales.SalesInvoices;

/// <summary>Shared between Create/Update — builds numbered lines from the caller's input and
/// returns the resulting Subtotal, same pattern as PurchaseOrderLineBuilder.</summary>
internal static class SalesInvoiceLineBuilder
{
    public static (List<SalesInvoiceLine> Lines, decimal Subtotal) Build(IReadOnlyList<SalesInvoiceLineInput> inputs)
    {
        var lines = new List<SalesInvoiceLine>();
        var subtotal = 0m;
        var lineNumber = 1;

        foreach (var input in inputs)
        {
            var lineTotal = (input.Quantity * input.UnitPrice) - (input.DiscountAmount ?? 0);

            lines.Add(new SalesInvoiceLine
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
