using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Amendments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ShiftSchedules_EmployeeId_WorkDate",
                table: "ShiftSchedules");

            migrationBuilder.RenameColumn(
                name: "WorkDate",
                table: "ShiftSchedules",
                newName: "StartDate");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "Holidays",
                newName: "StartDate");

            migrationBuilder.RenameIndex(
                name: "IX_Holidays_BranchId_Date",
                table: "Holidays",
                newName: "IX_Holidays_BranchId_StartDate");

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "ShiftSchedules",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndDate",
                table: "Holidays",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeWeeklyRestDays",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    WeeklyRestDaysMask = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_EmployeeWeeklyRestDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeWeeklyRestDays_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeWeeklyRestDays_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeWeeklyRestDays_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeWeeklyRestDays_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSchedules_EmployeeId_StartDate",
                table: "ShiftSchedules",
                columns: new[] { "EmployeeId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWeeklyRestDays_CompanyId",
                table: "EmployeeWeeklyRestDays",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWeeklyRestDays_EmployeeId_EffectiveFrom",
                table: "EmployeeWeeklyRestDays",
                columns: new[] { "EmployeeId", "EffectiveFrom" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeWeeklyRestDays");

            migrationBuilder.DropIndex(
                name: "IX_ShiftSchedules_EmployeeId_StartDate",
                table: "ShiftSchedules");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "ShiftSchedules");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "Holidays");

            migrationBuilder.RenameColumn(
                name: "StartDate",
                table: "ShiftSchedules",
                newName: "WorkDate");

            migrationBuilder.RenameColumn(
                name: "StartDate",
                table: "Holidays",
                newName: "Date");

            migrationBuilder.RenameIndex(
                name: "IX_Holidays_BranchId_StartDate",
                table: "Holidays",
                newName: "IX_Holidays_BranchId_Date");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftSchedules_EmployeeId_WorkDate",
                table: "ShiftSchedules",
                columns: new[] { "EmployeeId", "WorkDate" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
