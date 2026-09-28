using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HrFreeFieldMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "EmployeeIdLinked",
                table: "CustodyRegisters",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EmployeeId",
                table: "CustodyOfficers",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_TechnicianId",
                table: "MaintenanceSchedules",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_TechnicianId",
                table: "MaintenanceRequests",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_CustodyOfficers_EmployeeId",
                table: "CustodyOfficers",
                column: "EmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_CustodyOfficers_Employees_EmployeeId",
                table: "CustodyOfficers",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceRequests_Employees_TechnicianId",
                table: "MaintenanceRequests",
                column: "TechnicianId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceSchedules_Employees_TechnicianId",
                table: "MaintenanceSchedules",
                column: "TechnicianId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustodyOfficers_Employees_EmployeeId",
                table: "CustodyOfficers");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceRequests_Employees_TechnicianId",
                table: "MaintenanceRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceSchedules_Employees_TechnicianId",
                table: "MaintenanceSchedules");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceSchedules_TechnicianId",
                table: "MaintenanceSchedules");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceRequests_TechnicianId",
                table: "MaintenanceRequests");

            migrationBuilder.DropIndex(
                name: "IX_CustodyOfficers_EmployeeId",
                table: "CustodyOfficers");

            migrationBuilder.DropColumn(
                name: "EmployeeIdLinked",
                table: "CustodyRegisters");

            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "CustodyOfficers");
        }
    }
}
