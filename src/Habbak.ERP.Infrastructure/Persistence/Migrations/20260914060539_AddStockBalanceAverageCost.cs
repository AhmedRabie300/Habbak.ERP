using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockBalanceAverageCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AverageCost",
                table: "StockBalances",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastCostUpdateAtUtc",
                table: "StockBalances",
                type: "datetime2",
                nullable: true);

            // Backfill from the movement history rather than leaving every existing balance at
            // zero. Without this, rule 41 would reject the first return or count surplus on any
            // stock that predates this migration, and every outbound movement would record no
            // cost at all - the exact problem this column exists to end.
            //
            // Weighted average over the inbound transactions that actually carried a cost, which
            // is the same figure the live formula would have produced had it always been running.
            migrationBuilder.Sql("""
                UPDATE b
                SET b.AverageCost = h.WeightedAverageCost,
                    b.LastCostUpdateAtUtc = SYSUTCDATETIME()
                FROM StockBalances b
                INNER JOIN (
                    SELECT ItemId,
                           WarehouseId,
                           SUM(Quantity * UnitCost) / NULLIF(SUM(Quantity), 0) AS WeightedAverageCost
                    FROM StockTransactions
                    WHERE UnitCost > 0
                      AND IsDeleted = 0
                      AND TransactionType IN (1, 2, 3, 4, 5, 13, 14)
                    GROUP BY ItemId, WarehouseId
                ) h ON h.ItemId = b.ItemId AND h.WarehouseId = b.WarehouseId
                WHERE h.WeightedAverageCost IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageCost",
                table: "StockBalances");

            migrationBuilder.DropColumn(
                name: "LastCostUpdateAtUtc",
                table: "StockBalances");
        }
    }
}
