using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Repairs the stock whose cost the old transfer behaviour destroyed.
    ///
    /// Before AverageCost existed, TransferOut/TransferIn recorded UnitCost = 0, so any balance
    /// that only ever arrived by transfer has no priced movement in its history and the previous
    /// migration's backfill could not value it. Those balances are real goods that were paid for —
    /// their cost just never travelled with them.
    ///
    /// The goods came from somewhere, so this values them at the same item's quantity-weighted
    /// cost across the warehouses that *do* have one. It is an estimate, and only ever applied to
    /// balances that would otherwise stay at zero forever; the next real receipt into that
    /// warehouse supersedes it through the normal weighted-average formula.
    /// </summary>
    public partial class RepairTransferOrphanedStockCosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE b
                SET b.AverageCost = src.FallbackCost,
                    b.LastCostUpdateAtUtc = SYSUTCDATETIME()
                FROM StockBalances b
                INNER JOIN (
                    SELECT ItemId,
                           SUM(QuantityOnHand * AverageCost) / NULLIF(SUM(QuantityOnHand), 0) AS FallbackCost
                    FROM StockBalances
                    WHERE AverageCost > 0 AND QuantityOnHand > 0 AND IsDeleted = 0
                    GROUP BY ItemId
                ) src ON src.ItemId = b.ItemId
                WHERE b.AverageCost = 0
                  AND b.QuantityOnHand > 0
                  AND b.IsDeleted = 0
                  AND src.FallbackCost IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way data repair: the zeros it replaced carried no information to restore.
        }
    }
}
