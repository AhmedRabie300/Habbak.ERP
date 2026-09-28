using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPostingEngineDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "JournalEntryId",
                table: "WasteRecords",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VarianceJournalEntryId",
                table: "Shifts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "JournalEntryId",
                table: "SalesReturns",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "JournalEntryId",
                table: "SalesInvoices",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "JournalEntryId",
                table: "PurchaseReturns",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "JournalEntryId",
                table: "InventoryCounts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "JournalEntryId",
                table: "DeliveryOrders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PostingMode",
                table: "BranchPOSSettings",
                type: "int",
                nullable: false,
                defaultValue: 2); // PerShift — 0 is not a valid mode

            migrationBuilder.AddColumn<decimal>(
                name: "ShiftVarianceEmployeeLiabilityThreshold",
                table: "BranchPOSSettings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 10m); // spec rule 20 default

            migrationBuilder.CreateTable(
                name: "PostingFailures",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    ScreenCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceModule = table.Column<int>(type: "int", nullable: false),
                    SourceDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByJournalEntryId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_PostingFailures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostingFailures_JournalEntries_ResolvedByJournalEntryId",
                        column: x => x.ResolvedByJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WasteRecords_JournalEntryId",
                table: "WasteRecords",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseDocuments_JournalEntryId",
                table: "WarehouseDocuments",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_VarianceJournalEntryId",
                table: "Shifts",
                column: "VarianceJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_JournalEntryId",
                table: "SalesReturns",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesInvoices_JournalEntryId",
                table: "SalesInvoices",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_JournalEntryId",
                table: "PurchaseReturns",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_POSReturns_JournalEntryId",
                table: "POSReturns",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_POSInvoices_JournalEntryId",
                table: "POSInvoices",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCounts_JournalEntryId",
                table: "InventoryCounts",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DrawerExpenses_JournalEntryId",
                table: "DrawerExpenses",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryOrders_JournalEntryId",
                table: "DeliveryOrders",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PostingFailures_CompanyId",
                table: "PostingFailures",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PostingFailures_CompanyId_IsResolved_OccurredAtUtc",
                table: "PostingFailures",
                columns: new[] { "CompanyId", "IsResolved", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PostingFailures_CompanyId_ScreenCode_SourceDocumentId",
                table: "PostingFailures",
                columns: new[] { "CompanyId", "ScreenCode", "SourceDocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_PostingFailures_ResolvedByJournalEntryId",
                table: "PostingFailures",
                column: "ResolvedByJournalEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryOrders_JournalEntries_JournalEntryId",
                table: "DeliveryOrders",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DrawerExpenses_JournalEntries_JournalEntryId",
                table: "DrawerExpenses",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCounts_JournalEntries_JournalEntryId",
                table: "InventoryCounts",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_POSInvoices_JournalEntries_JournalEntryId",
                table: "POSInvoices",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_POSReturns_JournalEntries_JournalEntryId",
                table: "POSReturns",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_JournalEntries_JournalEntryId",
                table: "PurchaseReturns",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_JournalEntries_JournalEntryId",
                table: "SalesInvoices",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturns_JournalEntries_JournalEntryId",
                table: "SalesReturns",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_JournalEntries_VarianceJournalEntryId",
                table: "Shifts",
                column: "VarianceJournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseDocuments_JournalEntries_JournalEntryId",
                table: "WarehouseDocuments",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_WasteRecords_JournalEntries_JournalEntryId",
                table: "WasteRecords",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryOrders_JournalEntries_JournalEntryId",
                table: "DeliveryOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_DrawerExpenses_JournalEntries_JournalEntryId",
                table: "DrawerExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCounts_JournalEntries_JournalEntryId",
                table: "InventoryCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_POSInvoices_JournalEntries_JournalEntryId",
                table: "POSInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_POSReturns_JournalEntries_JournalEntryId",
                table: "POSReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_JournalEntries_JournalEntryId",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_JournalEntries_JournalEntryId",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturns_JournalEntries_JournalEntryId",
                table: "SalesReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_JournalEntries_VarianceJournalEntryId",
                table: "Shifts");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseDocuments_JournalEntries_JournalEntryId",
                table: "WarehouseDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_WasteRecords_JournalEntries_JournalEntryId",
                table: "WasteRecords");

            migrationBuilder.DropTable(
                name: "PostingFailures");

            migrationBuilder.DropIndex(
                name: "IX_WasteRecords_JournalEntryId",
                table: "WasteRecords");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseDocuments_JournalEntryId",
                table: "WarehouseDocuments");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_VarianceJournalEntryId",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_SalesReturns_JournalEntryId",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_SalesInvoices_JournalEntryId",
                table: "SalesInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_JournalEntryId",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_POSReturns_JournalEntryId",
                table: "POSReturns");

            migrationBuilder.DropIndex(
                name: "IX_POSInvoices_JournalEntryId",
                table: "POSInvoices");

            migrationBuilder.DropIndex(
                name: "IX_InventoryCounts_JournalEntryId",
                table: "InventoryCounts");

            migrationBuilder.DropIndex(
                name: "IX_DrawerExpenses_JournalEntryId",
                table: "DrawerExpenses");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryOrders_JournalEntryId",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "WasteRecords");

            migrationBuilder.DropColumn(
                name: "VarianceJournalEntryId",
                table: "Shifts");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "SalesInvoices");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "InventoryCounts");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "DeliveryOrders");

            migrationBuilder.DropColumn(
                name: "PostingMode",
                table: "BranchPOSSettings");

            migrationBuilder.DropColumn(
                name: "ShiftVarianceEmployeeLiabilityThreshold",
                table: "BranchPOSSettings");
        }
    }
}
