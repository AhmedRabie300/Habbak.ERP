using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchaseOrderRequestLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OrderedQuantity",
                table: "PurchaseRequestLines",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "PurchaseRequestLineId",
                table: "PurchaseOrderLines",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowManualOrderLines",
                table: "PurchaseCycleSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PurchaseRequestLineId",
                table: "PurchaseOrderLines",
                column: "PurchaseRequestLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLines_PurchaseRequestLines_PurchaseRequestLineId",
                table: "PurchaseOrderLines",
                column: "PurchaseRequestLineId",
                principalTable: "PurchaseRequestLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Remarks7, best-effort backfill — the same rule the invoice-order linking used: an
            // existing order line is tied to a request line only when the answer is unambiguous, i.e.
            // the order names a request and that request has exactly one line for the same item.
            // Anything else stays null; a null line simply orders nothing against the request.
            migrationBuilder.Sql(@"
UPDATE ol
SET ol.PurchaseRequestLineId = match.RequestLineId
FROM PurchaseOrderLines ol
JOIN PurchaseOrders o ON o.Id = ol.PurchaseOrderId
CROSS APPLY (
    SELECT MIN(rl.Id) AS RequestLineId, COUNT(*) AS LineCount
    FROM PurchaseRequestLines rl
    WHERE rl.PurchaseRequestId = o.PurchaseRequestId AND rl.ItemId = ol.ItemId AND rl.IsDeleted = 0
) AS match
WHERE ol.PurchaseRequestLineId IS NULL
  AND ol.IsDeleted = 0
  AND o.PurchaseRequestId IS NOT NULL
  AND match.LineCount = 1;");

            // What those linked lines already ordered, in the request line's own unit (base quantity ÷
            // its factor). Cancelled/Rejected orders order nothing. Without this, a request that was
            // already (fully or partly) converted would look untouched and could be over-ordered.
            migrationBuilder.Sql(@"
UPDATE rl
SET rl.OrderedQuantity = ordered.Quantity
FROM PurchaseRequestLines rl
JOIN (
    SELECT ol.PurchaseRequestLineId AS RequestLineId,
           SUM(CASE WHEN rl2.UnitFactor = 0 THEN ol.BaseQuantity ELSE ol.BaseQuantity / rl2.UnitFactor END) AS Quantity
    FROM PurchaseOrderLines ol
    JOIN PurchaseOrders o ON o.Id = ol.PurchaseOrderId
    JOIN PurchaseRequestLines rl2 ON rl2.Id = ol.PurchaseRequestLineId
    WHERE ol.PurchaseRequestLineId IS NOT NULL AND ol.IsDeleted = 0 AND o.IsDeleted = 0
      AND o.Status NOT IN (8, 9)   -- Cancelled, Rejected: they order nothing
    GROUP BY ol.PurchaseRequestLineId
) AS ordered ON ordered.RequestLineId = rl.Id;");

            // And the requests' own status follows from those totals: fully converted = Converted (4),
            // partly converted = PartiallyConverted (8). Requests nobody converted, and the ones whose
            // status is a decision (rejected, cancelled, archived), are left exactly as they are. This
            // also fixes CreatePurchaseOrderCommand's pre-existing bug where the first order from a
            // request unconditionally marked it Converted regardless of how much it actually covered.
            migrationBuilder.Sql(@"
UPDATE r
SET r.Status = CASE WHEN totals.UncoveredLines = 0 THEN 4 ELSE 8 END
FROM PurchaseRequests r
JOIN (
    SELECT rl.PurchaseRequestId,
           SUM(CASE WHEN rl.OrderedQuantity > 0 THEN 1 ELSE 0 END) AS CoveredLines,
           SUM(CASE WHEN rl.OrderedQuantity >= rl.Quantity THEN 0 ELSE 1 END) AS UncoveredLines
    FROM PurchaseRequestLines rl
    WHERE rl.IsDeleted = 0
    GROUP BY rl.PurchaseRequestId
) AS totals ON totals.PurchaseRequestId = r.Id
WHERE totals.CoveredLines > 0
  AND r.IsDeleted = 0
  AND r.Status NOT IN (5, 6, 7);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_PurchaseRequestLines_PurchaseRequestLineId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderLines_PurchaseRequestLineId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "OrderedQuantity",
                table: "PurchaseRequestLines");

            migrationBuilder.DropColumn(
                name: "PurchaseRequestLineId",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "AllowManualOrderLines",
                table: "PurchaseCycleSettings");
        }
    }
}
