using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseInvoiceJournalEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "JournalEntryId",
                table: "PurchaseInvoices",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_JournalEntryId",
                table: "PurchaseInvoices",
                column: "JournalEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_JournalEntries_JournalEntryId",
                table: "PurchaseInvoices",
                column: "JournalEntryId",
                principalTable: "JournalEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_JournalEntries_JournalEntryId",
                table: "PurchaseInvoices");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseInvoices_JournalEntryId",
                table: "PurchaseInvoices");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "PurchaseInvoices");
        }
    }
}
