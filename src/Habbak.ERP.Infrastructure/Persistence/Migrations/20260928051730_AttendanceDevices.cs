using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AttendanceDevices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DeviceSecretHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_AttendanceDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceDevices_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDevices_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDevices_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceDeviceLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    AttendanceDeviceId = table.Column<long>(type: "bigint", nullable: false),
                    SyncType = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PunchesReceived = table.Column<int>(type: "int", nullable: false),
                    PunchesProcessed = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AttendanceDeviceLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceDeviceLogs_AttendanceDevices_AttendanceDeviceId",
                        column: x => x.AttendanceDeviceId,
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDeviceLogs_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDeviceLogs_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AttendanceDeviceLogs_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDeviceMappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    AttendanceDeviceId = table.Column<long>(type: "bigint", nullable: false),
                    DeviceUserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("PK_EmployeeDeviceMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDeviceMappings_AttendanceDevices_AttendanceDeviceId",
                        column: x => x.AttendanceDeviceId,
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDeviceMappings_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDeviceMappings_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDeviceMappings_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDeviceMappings_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RawPunches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    AttendanceDeviceId = table.Column<long>(type: "bigint", nullable: false),
                    DeviceUserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PunchTimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawStatus = table.Column<int>(type: "int", nullable: true),
                    RawVerifyType = table.Column<int>(type: "int", nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessingStatus = table.Column<int>(type: "int", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResultTimeEntryId = table.Column<long>(type: "bigint", nullable: true),
                    SkipReason = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_RawPunches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawPunches_AttendanceDevices_AttendanceDeviceId",
                        column: x => x.AttendanceDeviceId,
                        principalTable: "AttendanceDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawPunches_TimeEntries_ResultTimeEntryId",
                        column: x => x.ResultTimeEntryId,
                        principalTable: "TimeEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawPunches_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawPunches_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawPunches_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_DeviceId",
                table: "TimeEntries",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDeviceLogs_AttendanceDeviceId_StartedAtUtc",
                table: "AttendanceDeviceLogs",
                columns: new[] { "AttendanceDeviceId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDeviceLogs_CompanyId",
                table: "AttendanceDeviceLogs",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDevices_CompanyId",
                table: "AttendanceDevices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDevices_CompanyId_Code",
                table: "AttendanceDevices",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceDevices_SerialNumber",
                table: "AttendanceDevices",
                column: "SerialNumber",
                unique: true,
                filter: "[IsDeleted] = 0 AND [SerialNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeviceMappings_AttendanceDeviceId_DeviceUserId",
                table: "EmployeeDeviceMappings",
                columns: new[] { "AttendanceDeviceId", "DeviceUserId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeviceMappings_CompanyId",
                table: "EmployeeDeviceMappings",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeviceMappings_EmployeeId",
                table: "EmployeeDeviceMappings",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_RawPunches_AttendanceDeviceId_DeviceUserId_PunchTimestampUtc",
                table: "RawPunches",
                columns: new[] { "AttendanceDeviceId", "DeviceUserId", "PunchTimestampUtc" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_RawPunches_CompanyId",
                table: "RawPunches",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_RawPunches_ProcessingStatus",
                table: "RawPunches",
                column: "ProcessingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_RawPunches_ResultTimeEntryId",
                table: "RawPunches",
                column: "ResultTimeEntryId");

            migrationBuilder.AddForeignKey(
                name: "FK_TimeEntries_AttendanceDevices_DeviceId",
                table: "TimeEntries",
                column: "DeviceId",
                principalTable: "AttendanceDevices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TimeEntries_AttendanceDevices_DeviceId",
                table: "TimeEntries");

            migrationBuilder.DropTable(
                name: "AttendanceDeviceLogs");

            migrationBuilder.DropTable(
                name: "EmployeeDeviceMappings");

            migrationBuilder.DropTable(
                name: "RawPunches");

            migrationBuilder.DropTable(
                name: "AttendanceDevices");

            migrationBuilder.DropIndex(
                name: "IX_TimeEntries_DeviceId",
                table: "TimeEntries");
        }
    }
}
