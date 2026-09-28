using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchesAndCostCenterLinking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CostCenterDimensionValues_CostCenterDimensionId_Code",
                table: "CostCenterDimensionValues");

            migrationBuilder.DropIndex(
                name: "IX_AccountDimensionLinks_AccountId_CostCenterDimensionId",
                table: "AccountDimensionLinks");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CostCenterDimensionValues",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkedEntityType",
                table: "CostCenterDimensions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CostCenterDimensionValues_CostCenterDimensionId_Code",
                table: "CostCenterDimensionValues",
                columns: new[] { "CostCenterDimensionId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AccountDimensionLinks_AccountId_CostCenterDimensionId",
                table: "AccountDimensionLinks",
                columns: new[] { "AccountId", "CostCenterDimensionId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_CompanyId",
                table: "Branches",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Branches_CompanyId_Code",
                table: "Branches",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_CostCenterDimensionValues_CostCenterDimensionId_Code",
                table: "CostCenterDimensionValues");

            migrationBuilder.DropIndex(
                name: "IX_AccountDimensionLinks_AccountId_CostCenterDimensionId",
                table: "AccountDimensionLinks");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CostCenterDimensionValues");

            migrationBuilder.DropColumn(
                name: "LinkedEntityType",
                table: "CostCenterDimensions");

            migrationBuilder.CreateIndex(
                name: "IX_CostCenterDimensionValues_CostCenterDimensionId_Code",
                table: "CostCenterDimensionValues",
                columns: new[] { "CostCenterDimensionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountDimensionLinks_AccountId_CostCenterDimensionId",
                table: "AccountDimensionLinks",
                columns: new[] { "AccountId", "CostCenterDimensionId" },
                unique: true);
        }
    }
}
