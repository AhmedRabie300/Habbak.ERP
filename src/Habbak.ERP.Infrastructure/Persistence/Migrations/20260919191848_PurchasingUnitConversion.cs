using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchasingUnitConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every existing purchasing line was entered in its item's base unit (the unit was stored
            // but never converted), so: factor 1, base quantity = quantity, base cost = the line's price.
            // Nullable first, filled, then NOT NULL.
            var tables = new (string Table, string? CostColumn)[]
            {
                ("PurchaseRequestLines", null),
                ("PurchaseOrderLines", "UnitPrice"),
                ("GoodsReceiptLines", "UnitCost"),
                ("PurchaseInvoiceLines", "UnitPrice"),
                ("PurchaseReturnLines", "UnitCost")
            };

            foreach (var (table, costColumn) in tables)
            {
                migrationBuilder.AddColumn<decimal>(name: "UnitFactor", table: table, type: "decimal(18,6)", precision: 18, scale: 6, nullable: true);
                migrationBuilder.AddColumn<decimal>(name: "BaseQuantity", table: table, type: "decimal(18,4)", precision: 18, scale: 4, nullable: true);
                if (costColumn is not null)
                {
                    migrationBuilder.AddColumn<decimal>(name: "BaseUnitCost", table: table, type: "decimal(18,6)", precision: 18, scale: 6, nullable: true);
                }

                migrationBuilder.Sql(costColumn is null
                    ? $"UPDATE {table} SET UnitFactor = 1, BaseQuantity = Quantity;"
                    : $"UPDATE {table} SET UnitFactor = 1, BaseQuantity = Quantity, BaseUnitCost = {costColumn};");

                migrationBuilder.AlterColumn<decimal>(name: "UnitFactor", table: table, type: "decimal(18,6)", precision: 18, scale: 6, nullable: false,
                    oldClrType: typeof(decimal), oldType: "decimal(18,6)", oldPrecision: 18, oldScale: 6, oldNullable: true);
                migrationBuilder.AlterColumn<decimal>(name: "BaseQuantity", table: table, type: "decimal(18,4)", precision: 18, scale: 4, nullable: false,
                    oldClrType: typeof(decimal), oldType: "decimal(18,4)", oldPrecision: 18, oldScale: 4, oldNullable: true);
                if (costColumn is not null)
                {
                    migrationBuilder.AlterColumn<decimal>(name: "BaseUnitCost", table: table, type: "decimal(18,6)", precision: 18, scale: 6, nullable: false,
                        oldClrType: typeof(decimal), oldType: "decimal(18,6)", oldPrecision: 18, oldScale: 6, oldNullable: true);
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseQuantity",
                table: "PurchaseReturnLines");

            migrationBuilder.DropColumn(
                name: "BaseUnitCost",
                table: "PurchaseReturnLines");

            migrationBuilder.DropColumn(
                name: "UnitFactor",
                table: "PurchaseReturnLines");

            migrationBuilder.DropColumn(
                name: "BaseQuantity",
                table: "PurchaseRequestLines");

            migrationBuilder.DropColumn(
                name: "UnitFactor",
                table: "PurchaseRequestLines");

            migrationBuilder.DropColumn(
                name: "BaseQuantity",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "BaseUnitCost",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "UnitFactor",
                table: "PurchaseOrderLines");

            migrationBuilder.DropColumn(
                name: "BaseQuantity",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "BaseUnitCost",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "UnitFactor",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropColumn(
                name: "BaseQuantity",
                table: "GoodsReceiptLines");

            migrationBuilder.DropColumn(
                name: "BaseUnitCost",
                table: "GoodsReceiptLines");

            migrationBuilder.DropColumn(
                name: "UnitFactor",
                table: "GoodsReceiptLines");
        }
    }
}
