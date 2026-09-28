using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitCostCenterDimensionNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing data in "Name" was always Arabic text — rename it into NameAr (not NameEn)
            // so it doesn't need backfilling, then backfill NameEn from the same value as a
            // reasonable fallback (there is no real English translation on file for this data).
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "CostCenterDimensionValues",
                newName: "NameAr");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "CostCenterDimensions",
                newName: "NameAr");

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "CostCenterDimensionValues",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "CostCenterDimensions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.Sql("UPDATE CostCenterDimensionValues SET NameEn = NameAr WHERE NameEn IS NULL;");
            migrationBuilder.Sql("UPDATE CostCenterDimensions SET NameEn = NameAr WHERE NameEn IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "NameEn",
                table: "CostCenterDimensionValues",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NameEn",
                table: "CostCenterDimensions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "CostCenterDimensionValues");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "CostCenterDimensions");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "CostCenterDimensionValues",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "NameAr",
                table: "CostCenterDimensions",
                newName: "Name");
        }
    }
}
