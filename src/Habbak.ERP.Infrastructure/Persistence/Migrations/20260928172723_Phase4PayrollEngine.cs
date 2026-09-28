using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4PayrollEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExceedsLimit",
                table: "OvertimeRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "HrOverrideApprovedByUserId",
                table: "OvertimeRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OvertimeLimitRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    MaxMinutesPerDay = table.Column<int>(type: "int", nullable: true),
                    MaxMinutesPerMonth = table.Column<int>(type: "int", nullable: true),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_OvertimeLimitRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OvertimeLimitRules_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OvertimeLimitRules_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OvertimeLimitRules_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeLimitRules_CompanyId",
                table: "OvertimeLimitRules",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_OvertimeLimitRules_CompanyId_EffectiveFrom",
                table: "OvertimeLimitRules",
                columns: new[] { "CompanyId", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OvertimeLimitRules");

            migrationBuilder.DropColumn(
                name: "ExceedsLimit",
                table: "OvertimeRequests");

            migrationBuilder.DropColumn(
                name: "HrOverrideApprovedByUserId",
                table: "OvertimeRequests");
        }
    }
}
