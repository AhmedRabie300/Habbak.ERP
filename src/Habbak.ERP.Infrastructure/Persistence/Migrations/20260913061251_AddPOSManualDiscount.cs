using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPOSManualDiscount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ManualDiscountAmount",
                table: "POSInvoices",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ManualDiscountReason",
                table: "Checks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManualDiscountType",
                table: "Checks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ManualDiscountValue",
                table: "Checks",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManualDiscountAmount",
                table: "POSInvoices");

            migrationBuilder.DropColumn(
                name: "ManualDiscountReason",
                table: "Checks");

            migrationBuilder.DropColumn(
                name: "ManualDiscountType",
                table: "Checks");

            migrationBuilder.DropColumn(
                name: "ManualDiscountValue",
                table: "Checks");
        }
    }
}
