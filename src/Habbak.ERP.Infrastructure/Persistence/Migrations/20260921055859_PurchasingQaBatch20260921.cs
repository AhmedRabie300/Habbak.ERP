using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PurchasingQaBatch20260921 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PurchaseInvoiceLineId",
                table: "PurchaseReturnLines",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierPaymentAllocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    VoucherId = table.Column<long>(type: "bigint", nullable: false),
                    PurchaseInvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierPaymentAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierPaymentAllocations_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPaymentAllocations_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPaymentAllocations_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPaymentAllocations_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierPaymentAllocations_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnLines_PurchaseInvoiceLineId",
                table: "PurchaseReturnLines",
                column: "PurchaseInvoiceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_RFQId",
                table: "PurchaseOrders",
                column: "RFQId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentAllocations_CompanyId",
                table: "SupplierPaymentAllocations",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentAllocations_PurchaseInvoiceId",
                table: "SupplierPaymentAllocations",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierPaymentAllocations_VoucherId_PurchaseInvoiceId",
                table: "SupplierPaymentAllocations",
                columns: new[] { "VoucherId", "PurchaseInvoiceId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_RequestsForQuotation_RFQId",
                table: "PurchaseOrders",
                column: "RFQId",
                principalTable: "RequestsForQuotation",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturnLines_PurchaseInvoiceLines_PurchaseInvoiceLineId",
                table: "PurchaseReturnLines",
                column: "PurchaseInvoiceLineId",
                principalTable: "PurchaseInvoiceLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Remarks4 item 8, best-effort backfill: an existing return line is linked to its
            // invoice line only when the answer is unambiguous — the return names an invoice, and
            // that invoice has exactly one line for the same item. Anything else (an invoice that
            // billed the same item on two lines, a return with no invoice, a line for an item the
            // invoice never had) stays null, which is what the column allows for. The rule only
            // binds new and edited returns; old ones keep whatever history they have.
            migrationBuilder.Sql(@"
UPDATE rl
SET rl.PurchaseInvoiceLineId = match.InvoiceLineId
FROM PurchaseReturnLines rl
JOIN PurchaseReturns r ON r.Id = rl.PurchaseReturnId
CROSS APPLY (
    SELECT MIN(il.Id) AS InvoiceLineId, COUNT(*) AS LineCount
    FROM PurchaseInvoiceLines il
    WHERE il.PurchaseInvoiceId = r.PurchaseInvoiceId AND il.ItemId = rl.ItemId AND il.IsDeleted = 0
) AS match
WHERE rl.PurchaseInvoiceLineId IS NULL
  AND rl.IsDeleted = 0
  AND r.PurchaseInvoiceId IS NOT NULL
  AND match.LineCount = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_RequestsForQuotation_RFQId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturnLines_PurchaseInvoiceLines_PurchaseInvoiceLineId",
                table: "PurchaseReturnLines");

            migrationBuilder.DropTable(
                name: "SupplierPaymentAllocations");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturnLines_PurchaseInvoiceLineId",
                table: "PurchaseReturnLines");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_RFQId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceLineId",
                table: "PurchaseReturnLines");
        }
    }
}
