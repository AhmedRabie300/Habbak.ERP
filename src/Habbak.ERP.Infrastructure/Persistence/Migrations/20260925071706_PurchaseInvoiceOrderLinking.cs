using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseInvoiceOrderLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InvoicedQuantity",
                table: "PurchaseOrderLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "PurchaseOrderLineId",
                table: "PurchaseInvoiceLines",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowManualInvoiceLines",
                table: "PurchaseCycleSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceLines_PurchaseOrderLineId",
                table: "PurchaseInvoiceLines",
                column: "PurchaseOrderLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceLines_PurchaseOrderLines_PurchaseOrderLineId",
                table: "PurchaseInvoiceLines",
                column: "PurchaseOrderLineId",
                principalTable: "PurchaseOrderLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Remarks6, best-effort backfill — the same rule the purchase-return linking used: an
            // existing invoice line is tied to an order line only when the answer is unambiguous, i.e.
            // the invoice names an order and that order has exactly one line for the same item.
            // Anything else stays null; a null line simply bills nothing against the order.
            migrationBuilder.Sql(@"
UPDATE il
SET il.PurchaseOrderLineId = match.OrderLineId
FROM PurchaseInvoiceLines il
JOIN PurchaseInvoices i ON i.Id = il.PurchaseInvoiceId
CROSS APPLY (
    SELECT MIN(ol.Id) AS OrderLineId, COUNT(*) AS LineCount
    FROM PurchaseOrderLines ol
    WHERE ol.PurchaseOrderId = i.PurchaseOrderId AND ol.ItemId = il.ItemId AND ol.IsDeleted = 0
) AS match
WHERE il.PurchaseOrderLineId IS NULL
  AND il.IsDeleted = 0
  AND i.PurchaseOrderId IS NOT NULL
  AND match.LineCount = 1;");

            // What those linked lines already billed, in the order line's own unit (base quantity ÷ its
            // factor). Cancelled invoices bill nothing. Without this, an order that was already invoiced
            // would look untouched and could be billed twice.
            migrationBuilder.Sql(@"
UPDATE ol
SET ol.InvoicedQuantity = billed.Quantity
FROM PurchaseOrderLines ol
JOIN (
    SELECT il.PurchaseOrderLineId AS OrderLineId,
           SUM(CASE WHEN ol2.UnitFactor = 0 THEN il.BaseQuantity ELSE il.BaseQuantity / ol2.UnitFactor END) AS Quantity
    FROM PurchaseInvoiceLines il
    JOIN PurchaseInvoices i ON i.Id = il.PurchaseInvoiceId
    JOIN PurchaseOrderLines ol2 ON ol2.Id = il.PurchaseOrderLineId
    WHERE il.PurchaseOrderLineId IS NOT NULL AND il.IsDeleted = 0 AND i.IsDeleted = 0
      AND i.Status NOT IN (8, 9)   -- Cancelled, Rejected: they bill nothing
    GROUP BY il.PurchaseOrderLineId
) AS billed ON billed.OrderLineId = ol.Id;");

            // And the orders' own status follows from those totals: fully billed = Invoiced (6),
            // partly billed = PartiallyInvoiced (11). Orders nobody billed, and the ones whose status
            // is a decision (cancelled, rejected, closed, archived), are left exactly as they are.
            migrationBuilder.Sql(@"
UPDATE o
SET o.Status = CASE WHEN totals.UnbilledLines = 0 THEN 6 ELSE 11 END
FROM PurchaseOrders o
JOIN (
    SELECT ol.PurchaseOrderId,
           SUM(CASE WHEN ol.InvoicedQuantity > 0 THEN 1 ELSE 0 END) AS BilledLines,
           SUM(CASE WHEN ol.InvoicedQuantity >= ol.Quantity THEN 0 ELSE 1 END) AS UnbilledLines
    FROM PurchaseOrderLines ol
    WHERE ol.IsDeleted = 0
    GROUP BY ol.PurchaseOrderId
) AS totals ON totals.PurchaseOrderId = o.Id
WHERE totals.BilledLines > 0
  AND o.IsDeleted = 0
  AND o.Status NOT IN (7, 8, 9, 10);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoiceLines_PurchaseOrderLines_PurchaseOrderLineId",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoiceLines_PurchaseOrderLineId",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "InvoicedQuantity",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderLineId",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "AllowManualInvoiceLines",
                table: "PurchaseCycleSettings");
        }
    }
}
