using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vouchers_CompanyId_VoucherNumber",
                table: "Vouchers");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Vouchers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_CompanyId_VoucherNumber",
                table: "Vouchers",
                columns: new[] { "CompanyId", "VoucherNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vouchers_CompanyId_VoucherNumber",
                table: "Vouchers");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Vouchers");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_CompanyId_VoucherNumber",
                table: "Vouchers",
                columns: new[] { "CompanyId", "VoucherNumber" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");
        }
    }
}
