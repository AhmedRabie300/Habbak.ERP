using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpandItemFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsSoldByWeight",
                table: "Items",
                newName: "TrackSerial");

            migrationBuilder.AddColumn<bool>(
                name: "AllowSubstitutes",
                table: "Items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "Items",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CostMethod",
                table: "Items",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "DefaultPrice",
                table: "Items",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsManufacturable",
                table: "Items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPurchasable",
                table: "Items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSellable",
                table: "Items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsStocked",
                table: "Items",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "PurchaseUnitOfMeasureId",
                table: "Items",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SaleMethod",
                table: "Items",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "SellUnitOfMeasureId",
                table: "Items",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Items",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Items_Barcode",
                table: "Items",
                column: "Barcode");

            migrationBuilder.CreateIndex(
                name: "IX_Items_PurchaseUnitOfMeasureId",
                table: "Items",
                column: "PurchaseUnitOfMeasureId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_SellUnitOfMeasureId",
                table: "Items",
                column: "SellUnitOfMeasureId");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_UnitsOfMeasure_PurchaseUnitOfMeasureId",
                table: "Items",
                column: "PurchaseUnitOfMeasureId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_UnitsOfMeasure_SellUnitOfMeasureId",
                table: "Items",
                column: "SellUnitOfMeasureId",
                principalTable: "UnitsOfMeasure",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Items_UnitsOfMeasure_PurchaseUnitOfMeasureId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_UnitsOfMeasure_SellUnitOfMeasureId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_Barcode",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_PurchaseUnitOfMeasureId",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Items_SellUnitOfMeasureId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "AllowSubstitutes",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CostMethod",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "DefaultPrice",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsManufacturable",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsPurchasable",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsSellable",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsStocked",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "PurchaseUnitOfMeasureId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SaleMethod",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "SellUnitOfMeasureId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Items");

            migrationBuilder.RenameColumn(
                name: "TrackSerial",
                table: "Items",
                newName: "IsSoldByWeight");
        }
    }
}
