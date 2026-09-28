using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryUnitsTaxCodeDefaultCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitFactor",
                table: "WarehouseDocumentLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<long>(
                name: "UnitId",
                table: "WarehouseDocumentLines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitFactor",
                table: "RecipeLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<long>(
                name: "UnitId",
                table: "RecipeLines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "TaxCode",
                table: "Items",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitFactor",
                table: "InventoryCountLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<long>(
                name: "UnitId",
                table: "InventoryCountLines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Currencies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitFactor",
                table: "BranchRequestLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<long>(
                name: "UnitId",
                table: "BranchRequestLines",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // Every existing line was entered in its item's base unit: that unit, factor 1 — so what the
            // lines mean does not change. Done before the foreign keys go on.
            foreach (var (table, itemColumn) in new[]
            {
                ("WarehouseDocumentLines", "ItemId"), ("BranchRequestLines", "ItemId"),
                ("RecipeLines", "ComponentItemId"), ("InventoryCountLines", "ItemId")
            })
            {
                migrationBuilder.Sql($"""
                    UPDATE l SET UnitId = i.BaseUnitOfMeasureId, UnitFactor = 1
                    FROM {table} l JOIN Items i ON i.Id = l.{itemColumn}
                    WHERE l.UnitId = 0;
                    """);
            }

            // Warehouse documents belong to the branch of the warehouse they work on (Remarks3,
            // items 17/20): the screens never sent a branch, so every document was company-level
            // and visible to every branch. A company-level (main) warehouse keeps the document as it was.
            migrationBuilder.Sql("""
                UPDATE d SET BranchId = w.BranchId
                FROM WarehouseDocuments d
                JOIN Warehouses w ON w.Id = CASE
                    WHEN d.DocumentType IN (1, 4, 7, 8) THEN d.DestinationWarehouseId
                    WHEN d.DocumentType IN (2, 3, 6) THEN d.SourceWarehouseId
                    ELSE COALESCE(d.SourceWarehouseId, d.DestinationWarehouseId) END
                WHERE d.BranchId IS NULL AND w.BranchId IS NOT NULL;
                """);

            // One default currency: the one most companies keep their books in, else EGP, else the first.
            migrationBuilder.Sql("""
                UPDATE Currencies SET IsDefault = 1
                WHERE Id = (
                    SELECT TOP 1 c.Id FROM Currencies c
                    WHERE c.IsDeleted = 0 AND c.IsActive = 1
                    ORDER BY (SELECT COUNT(*) FROM Companies co WHERE co.BaseCurrencyId = c.Id AND co.IsDeleted = 0) DESC,
                             CASE WHEN c.Code = 'EGP' THEN 0 ELSE 1 END, c.Id);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseDocumentLines_UnitId",
                table: "WarehouseDocumentLines",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLines_UnitId",
                table: "RecipeLines",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCountLines_UnitId",
                table: "InventoryCountLines",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_IsDefault",
                table: "Currencies",
                column: "IsDefault",
                unique: true,
                filter: "[IsDefault] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequestLines_UnitId",
                table: "BranchRequestLines",
                column: "UnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_BranchRequestLines_UnitsOfMeasure_UnitId",
                table: "BranchRequestLines",
                column: "UnitId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCountLines_UnitsOfMeasure_UnitId",
                table: "InventoryCountLines",
                column: "UnitId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeLines_UnitsOfMeasure_UnitId",
                table: "RecipeLines",
                column: "UnitId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseDocumentLines_UnitsOfMeasure_UnitId",
                table: "WarehouseDocumentLines",
                column: "UnitId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BranchRequestLines_UnitsOfMeasure_UnitId",
                table: "BranchRequestLines");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCountLines_UnitsOfMeasure_UnitId",
                table: "InventoryCountLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeLines_UnitsOfMeasure_UnitId",
                table: "RecipeLines");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseDocumentLines_UnitsOfMeasure_UnitId",
                table: "WarehouseDocumentLines");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseDocumentLines_UnitId",
                table: "WarehouseDocumentLines");

            migrationBuilder.DropIndex(
                name: "IX_RecipeLines_UnitId",
                table: "RecipeLines");

            migrationBuilder.DropIndex(
                name: "IX_InventoryCountLines_UnitId",
                table: "InventoryCountLines");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_IsDefault",
                table: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_BranchRequestLines_UnitId",
                table: "BranchRequestLines");

            migrationBuilder.DropColumn(
                name: "UnitFactor",
                table: "WarehouseDocumentLines");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "WarehouseDocumentLines");

            migrationBuilder.DropColumn(
                name: "UnitFactor",
                table: "RecipeLines");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "RecipeLines");

            migrationBuilder.DropColumn(
                name: "TaxCode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "UnitFactor",
                table: "InventoryCountLines");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "InventoryCountLines");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "UnitFactor",
                table: "BranchRequestLines");

            migrationBuilder.DropColumn(
                name: "UnitId",
                table: "BranchRequestLines");
        }
    }
}
