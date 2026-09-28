using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustodyOfficerAndTransferDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CustodyOfficerId",
                table: "WarehouseDocuments",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustodyOfficers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_CustodyOfficers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseDocuments_CustodyOfficerId",
                table: "WarehouseDocuments",
                column: "CustodyOfficerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyOfficers_CompanyId",
                table: "CustodyOfficers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyOfficers_CompanyId_Code",
                table: "CustodyOfficers",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseDocuments_CustodyOfficers_CustodyOfficerId",
                table: "WarehouseDocuments",
                column: "CustodyOfficerId",
                principalTable: "CustodyOfficers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseDocuments_CustodyOfficers_CustodyOfficerId",
                table: "WarehouseDocuments");

            migrationBuilder.DropTable(
                name: "CustodyOfficers");

            migrationBuilder.DropIndex(
                name: "IX_WarehouseDocuments_CustodyOfficerId",
                table: "WarehouseDocuments");

            migrationBuilder.DropColumn(
                name: "CustodyOfficerId",
                table: "WarehouseDocuments");
        }
    }
}
