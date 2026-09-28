using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4PayrollRunNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RunNumber",
                table: "PayrollRuns",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollRuns_CompanyId_RunNumber",
                table: "PayrollRuns",
                columns: new[] { "CompanyId", "RunNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollRuns_CompanyId_RunNumber",
                table: "PayrollRuns");

            migrationBuilder.DropColumn(
                name: "RunNumber",
                table: "PayrollRuns");
        }
    }
}
