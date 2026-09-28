using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsReceiptInvoiceSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "WarehouseId",
                table: "PurchaseInvoices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "PurchaseOrderId",
                table: "GoodsReceipts",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "PurchaseInvoiceId",
                table: "GoodsReceipts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_WarehouseId",
                table: "PurchaseInvoices",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_GoodsReceipts_PurchaseInvoiceId",
                table: "GoodsReceipts",
                column: "PurchaseInvoiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_PurchaseInvoices_PurchaseInvoiceId",
                table: "GoodsReceipts",
                column: "PurchaseInvoiceId",
                principalTable: "PurchaseInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Warehouses_WarehouseId",
                table: "PurchaseInvoices",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_PurchaseInvoices_PurchaseInvoiceId",
                table: "GoodsReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Warehouses_WarehouseId",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_WarehouseId",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_GoodsReceipts_PurchaseInvoiceId",
                table: "GoodsReceipts");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceId",
                table: "GoodsReceipts");

            migrationBuilder.AlterColumn<long>(
                name: "PurchaseOrderId",
                table: "GoodsReceipts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
