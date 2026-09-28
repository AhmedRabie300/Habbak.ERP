using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SettingsPermissionsPhase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogsArchive",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: true),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdditionalData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogsArchive", x => x.Id);
                });

            // Before any key exists: the system user (Id 0 — what every audit column holds for work
            // done by seeding or before sign-in), then a safety net for any stored user id with no
            // Users row. The databases this was written against had none (only 0 and 1 anywhere); a
            // stray id — a number typed into the old dev login — is attributed to the system rather
            // than failing the migration.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = 0)
BEGIN
    SET IDENTITY_INSERT Users ON;
    INSERT INTO Users (Id, Username, Email, PasswordHash, PasswordSalt, FullName, PreferredLanguage, PhoneNumber, EmployeeId, Status,
        FailedLoginAttempts, LockedUntilUtc, LastLoginAtUtc, MustChangePassword, TwoFactorEnabled, PasswordChangedAtUtc,
        CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, IsDeleted)
    VALUES (0, N'system', N'system@habbak.local', N'!', N'', N'النظام', 1, NULL, NULL, 3,
        0, NULL, NULL, 0, 0, NULL, SYSUTCDATETIME(), 0, SYSUTCDATETIME(), 0, 0);
    SET IDENTITY_INSERT Users OFF;
END");

            migrationBuilder.Sql(@"
DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql + N'UPDATE [' + t.name + N'] SET [' + c.name + N'] = 0 WHERE [' + c.name + N'] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Users u WHERE u.Id = [' + t.name + N'].[' + c.name + N']);' + CHAR(10)
FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
WHERE (c.name IN (N'CreatedBy', N'UpdatedBy', N'DeletedBy') AND t.name NOT IN (N'AuditLogs', N'AuditLogsArchive', N'__EFMigrationsHistory'))
   OR (t.name = N'AccountingPeriods' AND c.name = N'ClosedByUserId')
   OR (t.name = N'BankReconciliationRuns' AND c.name = N'ImportedByUserId')
   OR (t.name IN (N'CashReconciliations', N'DrawerMovements') AND c.name = N'ApprovedByUserId')
   OR (t.name = N'JournalEntries' AND c.name = N'PostedBy')
   OR (t.name IN (N'BranchRequests', N'PurchaseRequests') AND c.name = N'RequestedByUserId')
   OR (t.name = N'ProductionOrders' AND c.name = N'ExecutedByUserId')
   OR (t.name = N'CheckLineVoids' AND c.name = N'VoidedByUserId')
   OR (t.name = N'Shifts' AND c.name IN (N'CashierUserId', N'ClosedByUserId'))
   OR (t.name IN (N'ShiftAssignments', N'PostingFailures') AND c.name = N'UserId')
   OR (t.name = N'UserRoles' AND c.name = N'AssignedByUserId')
   OR (t.name = N'UserScopes' AND c.name = N'GrantedByUserId');
EXEC sp_executesql @sql;");

            migrationBuilder.CreateIndex(
                name: "IX_UserScopes_GrantedByUserId",
                table: "UserScopes",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_AssignedByUserId",
                table: "UserRoles",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_CashierUserId",
                table: "Shifts",
                column: "CashierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_ClosedByUserId",
                table: "Shifts",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_UserId",
                table: "ShiftAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_RequestedByUserId",
                table: "PurchaseRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_ExecutedByUserId",
                table: "ProductionOrders",
                column: "ExecutedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostingFailures_UserId",
                table: "PostingFailures",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_PostedBy",
                table: "JournalEntries",
                column: "PostedBy");

            migrationBuilder.CreateIndex(
                name: "IX_DrawerMovements_ApprovedByUserId",
                table: "DrawerMovements",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckLineVoids_VoidedByUserId",
                table: "CheckLineVoids",
                column: "VoidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashReconciliations_ApprovedByUserId",
                table: "CashReconciliations",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BranchRequests_RequestedByUserId",
                table: "BranchRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BankReconciliationRuns_ImportedByUserId",
                table: "BankReconciliationRuns",
                column: "ImportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriods_ClosedByUserId",
                table: "AccountingPeriods",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogsArchive_CompanyId_OccurredAtUtc",
                table: "AuditLogsArchive",
                columns: new[] { "CompanyId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogsArchive_EntityType_EntityId",
                table: "AuditLogsArchive",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AccountDimensionLinks_Users_CreatedBy",
                table: "AccountDimensionLinks",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountDimensionLinks_Users_DeletedBy",
                table: "AccountDimensionLinks",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountDimensionLinks_Users_UpdatedBy",
                table: "AccountDimensionLinks",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingPeriods_Users_ClosedByUserId",
                table: "AccountingPeriods",
                column: "ClosedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingPeriods_Users_CreatedBy",
                table: "AccountingPeriods",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingPeriods_Users_DeletedBy",
                table: "AccountingPeriods",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingPeriods_Users_UpdatedBy",
                table: "AccountingPeriods",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountOpeningBalanceBatches_Users_CreatedBy",
                table: "AccountOpeningBalanceBatches",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountOpeningBalanceBatches_Users_DeletedBy",
                table: "AccountOpeningBalanceBatches",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountOpeningBalanceBatches_Users_UpdatedBy",
                table: "AccountOpeningBalanceBatches",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountOpeningBalanceLines_Users_CreatedBy",
                table: "AccountOpeningBalanceLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountOpeningBalanceLines_Users_DeletedBy",
                table: "AccountOpeningBalanceLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AccountOpeningBalanceLines_Users_UpdatedBy",
                table: "AccountOpeningBalanceLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Users_CreatedBy",
                table: "Accounts",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Users_DeletedBy",
                table: "Accounts",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Accounts_Users_UpdatedBy",
                table: "Accounts",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_Users_CreatedBy",
                table: "Attachments",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_Users_DeletedBy",
                table: "Attachments",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_Users_UpdatedBy",
                table: "Attachments",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankReconciliationLines_Users_CreatedBy",
                table: "BankReconciliationLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankReconciliationLines_Users_DeletedBy",
                table: "BankReconciliationLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankReconciliationLines_Users_UpdatedBy",
                table: "BankReconciliationLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankReconciliationRuns_Users_CreatedBy",
                table: "BankReconciliationRuns",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankReconciliationRuns_Users_DeletedBy",
                table: "BankReconciliationRuns",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankReconciliationRuns_Users_ImportedByUserId",
                table: "BankReconciliationRuns",
                column: "ImportedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankReconciliationRuns_Users_UpdatedBy",
                table: "BankReconciliationRuns",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BlendTypes_Users_CreatedBy",
                table: "BlendTypes",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BlendTypes_Users_DeletedBy",
                table: "BlendTypes",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BlendTypes_Users_UpdatedBy",
                table: "BlendTypes",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Users_CreatedBy",
                table: "Branches",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Users_DeletedBy",
                table: "Branches",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Branches_Users_UpdatedBy",
                table: "Branches",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchItemLimits_Users_CreatedBy",
                table: "BranchItemLimits",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchItemLimits_Users_DeletedBy",
                table: "BranchItemLimits",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchItemLimits_Users_UpdatedBy",
                table: "BranchItemLimits",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchPOSSettings_Users_CreatedBy",
                table: "BranchPOSSettings",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchPOSSettings_Users_DeletedBy",
                table: "BranchPOSSettings",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchPOSSettings_Users_UpdatedBy",
                table: "BranchPOSSettings",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchRequestLines_Users_CreatedBy",
                table: "BranchRequestLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchRequestLines_Users_DeletedBy",
                table: "BranchRequestLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchRequestLines_Users_UpdatedBy",
                table: "BranchRequestLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchRequests_Users_CreatedBy",
                table: "BranchRequests",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchRequests_Users_DeletedBy",
                table: "BranchRequests",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchRequests_Users_RequestedByUserId",
                table: "BranchRequests",
                column: "RequestedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BranchRequests_Users_UpdatedBy",
                table: "BranchRequests",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashReconciliationDenominations_Users_CreatedBy",
                table: "CashReconciliationDenominations",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashReconciliationDenominations_Users_DeletedBy",
                table: "CashReconciliationDenominations",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashReconciliationDenominations_Users_UpdatedBy",
                table: "CashReconciliationDenominations",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashReconciliations_Users_ApprovedByUserId",
                table: "CashReconciliations",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashReconciliations_Users_CreatedBy",
                table: "CashReconciliations",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashReconciliations_Users_DeletedBy",
                table: "CashReconciliations",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashReconciliations_Users_UpdatedBy",
                table: "CashReconciliations",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CheckLines_Users_CreatedBy",
                table: "CheckLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CheckLines_Users_DeletedBy",
                table: "CheckLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CheckLines_Users_UpdatedBy",
                table: "CheckLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CheckLineVoids_Users_CreatedBy",
                table: "CheckLineVoids",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CheckLineVoids_Users_DeletedBy",
                table: "CheckLineVoids",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CheckLineVoids_Users_UpdatedBy",
                table: "CheckLineVoids",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CheckLineVoids_Users_VoidedByUserId",
                table: "CheckLineVoids",
                column: "VoidedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Checks_Users_CreatedBy",
                table: "Checks",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Checks_Users_DeletedBy",
                table: "Checks",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Checks_Users_UpdatedBy",
                table: "Checks",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CodingRules_Users_CreatedBy",
                table: "CodingRules",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CodingRules_Users_DeletedBy",
                table: "CodingRules",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CodingRules_Users_UpdatedBy",
                table: "CodingRules",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Users_CreatedBy",
                table: "Companies",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Users_DeletedBy",
                table: "Companies",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Companies_Users_UpdatedBy",
                table: "Companies",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyAccountMappings_Users_CreatedBy",
                table: "CompanyAccountMappings",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyAccountMappings_Users_DeletedBy",
                table: "CompanyAccountMappings",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CompanyAccountMappings_Users_UpdatedBy",
                table: "CompanyAccountMappings",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractItems_Users_CreatedBy",
                table: "ContractItems",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractItems_Users_DeletedBy",
                table: "ContractItems",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ContractItems_Users_UpdatedBy",
                table: "ContractItems",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CostCenterDimensions_Users_CreatedBy",
                table: "CostCenterDimensions",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CostCenterDimensions_Users_DeletedBy",
                table: "CostCenterDimensions",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CostCenterDimensions_Users_UpdatedBy",
                table: "CostCenterDimensions",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CostCenterDimensionValues_Users_CreatedBy",
                table: "CostCenterDimensionValues",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CostCenterDimensionValues_Users_DeletedBy",
                table: "CostCenterDimensionValues",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CostCenterDimensionValues_Users_UpdatedBy",
                table: "CostCenterDimensionValues",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Currencies_Users_CreatedBy",
                table: "Currencies",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Currencies_Users_DeletedBy",
                table: "Currencies",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Currencies_Users_UpdatedBy",
                table: "Currencies",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustodyOfficers_Users_CreatedBy",
                table: "CustodyOfficers",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustodyOfficers_Users_DeletedBy",
                table: "CustodyOfficers",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustodyOfficers_Users_UpdatedBy",
                table: "CustodyOfficers",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustodyRegisters_Users_CreatedBy",
                table: "CustodyRegisters",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustodyRegisters_Users_DeletedBy",
                table: "CustodyRegisters",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustodyRegisters_Users_UpdatedBy",
                table: "CustodyRegisters",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustodySettlementLines_Users_CreatedBy",
                table: "CustodySettlementLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustodySettlementLines_Users_DeletedBy",
                table: "CustodySettlementLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustodySettlementLines_Users_UpdatedBy",
                table: "CustodySettlementLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustodySettlements_Users_CreatedBy",
                table: "CustodySettlements",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustodySettlements_Users_DeletedBy",
                table: "CustodySettlements",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustodySettlements_Users_UpdatedBy",
                table: "CustodySettlements",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Users_CreatedBy",
                table: "Customers",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Users_DeletedBy",
                table: "Customers",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Users_UpdatedBy",
                table: "Customers",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryOrderLines_Users_CreatedBy",
                table: "DeliveryOrderLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryOrderLines_Users_DeletedBy",
                table: "DeliveryOrderLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryOrderLines_Users_UpdatedBy",
                table: "DeliveryOrderLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryOrders_Users_CreatedBy",
                table: "DeliveryOrders",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryOrders_Users_DeletedBy",
                table: "DeliveryOrders",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryOrders_Users_UpdatedBy",
                table: "DeliveryOrders",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryPlatformOrders_Users_CreatedBy",
                table: "DeliveryPlatformOrders",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryPlatformOrders_Users_DeletedBy",
                table: "DeliveryPlatformOrders",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryPlatformOrders_Users_UpdatedBy",
                table: "DeliveryPlatformOrders",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Discounts_Users_CreatedBy",
                table: "Discounts",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Discounts_Users_DeletedBy",
                table: "Discounts",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Discounts_Users_UpdatedBy",
                table: "Discounts",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DrawerExpenses_Users_CreatedBy",
                table: "DrawerExpenses",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DrawerExpenses_Users_DeletedBy",
                table: "DrawerExpenses",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DrawerExpenses_Users_UpdatedBy",
                table: "DrawerExpenses",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DrawerMovements_Users_ApprovedByUserId",
                table: "DrawerMovements",
                column: "ApprovedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DrawerMovements_Users_CreatedBy",
                table: "DrawerMovements",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DrawerMovements_Users_DeletedBy",
                table: "DrawerMovements",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DrawerMovements_Users_UpdatedBy",
                table: "DrawerMovements",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldLabels_Users_CreatedBy",
                table: "FieldLabels",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldLabels_Users_DeletedBy",
                table: "FieldLabels",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldLabels_Users_UpdatedBy",
                table: "FieldLabels",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldPermissions_Users_CreatedBy",
                table: "FieldPermissions",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldPermissions_Users_DeletedBy",
                table: "FieldPermissions",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FieldPermissions_Users_UpdatedBy",
                table: "FieldPermissions",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptLines_Users_CreatedBy",
                table: "GoodsReceiptLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptLines_Users_DeletedBy",
                table: "GoodsReceiptLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceiptLines_Users_UpdatedBy",
                table: "GoodsReceiptLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_Users_CreatedBy",
                table: "GoodsReceipts",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_Users_DeletedBy",
                table: "GoodsReceipts",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GoodsReceipts_Users_UpdatedBy",
                table: "GoodsReceipts",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCountLines_Users_CreatedBy",
                table: "InventoryCountLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCountLines_Users_DeletedBy",
                table: "InventoryCountLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCountLines_Users_UpdatedBy",
                table: "InventoryCountLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCounts_Users_CreatedBy",
                table: "InventoryCounts",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCounts_Users_DeletedBy",
                table: "InventoryCounts",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryCounts_Users_UpdatedBy",
                table: "InventoryCounts",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventorySettingsRows_Users_CreatedBy",
                table: "InventorySettingsRows",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventorySettingsRows_Users_DeletedBy",
                table: "InventorySettingsRows",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventorySettingsRows_Users_UpdatedBy",
                table: "InventorySettingsRows",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemGroups_Users_CreatedBy",
                table: "ItemGroups",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemGroups_Users_DeletedBy",
                table: "ItemGroups",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemGroups_Users_UpdatedBy",
                table: "ItemGroups",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Users_CreatedBy",
                table: "Items",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Users_DeletedBy",
                table: "Items",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Users_UpdatedBy",
                table: "Items",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemUnitConversions_Users_CreatedBy",
                table: "ItemUnitConversions",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemUnitConversions_Users_DeletedBy",
                table: "ItemUnitConversions",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemUnitConversions_Users_UpdatedBy",
                table: "ItemUnitConversions",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemWarehouseSettings_Users_CreatedBy",
                table: "ItemWarehouseSettings",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemWarehouseSettings_Users_DeletedBy",
                table: "ItemWarehouseSettings",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ItemWarehouseSettings_Users_UpdatedBy",
                table: "ItemWarehouseSettings",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_Users_CreatedBy",
                table: "JournalEntries",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_Users_DeletedBy",
                table: "JournalEntries",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_Users_PostedBy",
                table: "JournalEntries",
                column: "PostedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntries_Users_UpdatedBy",
                table: "JournalEntries",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntryLineDimensionValues_Users_CreatedBy",
                table: "JournalEntryLineDimensionValues",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntryLineDimensionValues_Users_DeletedBy",
                table: "JournalEntryLineDimensionValues",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntryLineDimensionValues_Users_UpdatedBy",
                table: "JournalEntryLineDimensionValues",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntryLines_Users_CreatedBy",
                table: "JournalEntryLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntryLines_Users_DeletedBy",
                table: "JournalEntryLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntryLines_Users_UpdatedBy",
                table: "JournalEntryLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntryTemplateSnapshots_Users_CreatedBy",
                table: "JournalEntryTemplateSnapshots",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntryTemplateSnapshots_Users_DeletedBy",
                table: "JournalEntryTemplateSnapshots",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JournalEntryTemplateSnapshots_Users_UpdatedBy",
                table: "JournalEntryTemplateSnapshots",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoginAttempts_Users_CreatedBy",
                table: "LoginAttempts",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoginAttempts_Users_DeletedBy",
                table: "LoginAttempts",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoginAttempts_Users_UpdatedBy",
                table: "LoginAttempts",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyProgramSettings_Users_CreatedBy",
                table: "LoyaltyProgramSettings",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyProgramSettings_Users_DeletedBy",
                table: "LoyaltyProgramSettings",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyProgramSettings_Users_UpdatedBy",
                table: "LoyaltyProgramSettings",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyTiers_Users_CreatedBy",
                table: "LoyaltyTiers",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyTiers_Users_DeletedBy",
                table: "LoyaltyTiers",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyTiers_Users_UpdatedBy",
                table: "LoyaltyTiers",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyTransactions_Users_CreatedBy",
                table: "LoyaltyTransactions",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyTransactions_Users_DeletedBy",
                table: "LoyaltyTransactions",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LoyaltyTransactions_Users_UpdatedBy",
                table: "LoyaltyTransactions",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Users_CreatedBy",
                table: "MenuItems",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Users_DeletedBy",
                table: "MenuItems",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_Users_UpdatedBy",
                table: "MenuItems",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentMethods_Users_CreatedBy",
                table: "PaymentMethods",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentMethods_Users_DeletedBy",
                table: "PaymentMethods",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentMethods_Users_UpdatedBy",
                table: "PaymentMethods",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PeriodCloseChecklistItems_Users_CreatedBy",
                table: "PeriodCloseChecklistItems",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PeriodCloseChecklistItems_Users_DeletedBy",
                table: "PeriodCloseChecklistItems",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PeriodCloseChecklistItems_Users_UpdatedBy",
                table: "PeriodCloseChecklistItems",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSCategories_Users_CreatedBy",
                table: "POSCategories",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSCategories_Users_DeletedBy",
                table: "POSCategories",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSCategories_Users_UpdatedBy",
                table: "POSCategories",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSInvoiceLines_Users_CreatedBy",
                table: "POSInvoiceLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSInvoiceLines_Users_DeletedBy",
                table: "POSInvoiceLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSInvoiceLines_Users_UpdatedBy",
                table: "POSInvoiceLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSInvoices_Users_CreatedBy",
                table: "POSInvoices",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSInvoices_Users_DeletedBy",
                table: "POSInvoices",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSInvoices_Users_UpdatedBy",
                table: "POSInvoices",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSPaymentMethodConfigs_Users_CreatedBy",
                table: "POSPaymentMethodConfigs",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSPaymentMethodConfigs_Users_DeletedBy",
                table: "POSPaymentMethodConfigs",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSPaymentMethodConfigs_Users_UpdatedBy",
                table: "POSPaymentMethodConfigs",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSPayments_Users_CreatedBy",
                table: "POSPayments",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSPayments_Users_DeletedBy",
                table: "POSPayments",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSPayments_Users_UpdatedBy",
                table: "POSPayments",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSReturnLines_Users_CreatedBy",
                table: "POSReturnLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSReturnLines_Users_DeletedBy",
                table: "POSReturnLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSReturnLines_Users_UpdatedBy",
                table: "POSReturnLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSReturns_Users_CreatedBy",
                table: "POSReturns",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSReturns_Users_DeletedBy",
                table: "POSReturns",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSReturns_Users_UpdatedBy",
                table: "POSReturns",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSTerminals_Users_CreatedBy",
                table: "POSTerminals",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSTerminals_Users_DeletedBy",
                table: "POSTerminals",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_POSTerminals_Users_UpdatedBy",
                table: "POSTerminals",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingFailures_Users_CreatedBy",
                table: "PostingFailures",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingFailures_Users_DeletedBy",
                table: "PostingFailures",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingFailures_Users_UpdatedBy",
                table: "PostingFailures",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingFailures_Users_UserId",
                table: "PostingFailures",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingTemplateLineCostCenters_Users_CreatedBy",
                table: "PostingTemplateLineCostCenters",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingTemplateLineCostCenters_Users_DeletedBy",
                table: "PostingTemplateLineCostCenters",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingTemplateLineCostCenters_Users_UpdatedBy",
                table: "PostingTemplateLineCostCenters",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingTemplateLines_Users_CreatedBy",
                table: "PostingTemplateLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingTemplateLines_Users_DeletedBy",
                table: "PostingTemplateLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingTemplateLines_Users_UpdatedBy",
                table: "PostingTemplateLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingTemplates_Users_CreatedBy",
                table: "PostingTemplates",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingTemplates_Users_DeletedBy",
                table: "PostingTemplates",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostingTemplates_Users_UpdatedBy",
                table: "PostingTemplates",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceListBranches_Users_CreatedBy",
                table: "PriceListBranches",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceListBranches_Users_DeletedBy",
                table: "PriceListBranches",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceListBranches_Users_UpdatedBy",
                table: "PriceListBranches",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceListLines_Users_CreatedBy",
                table: "PriceListLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceListLines_Users_DeletedBy",
                table: "PriceListLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceListLines_Users_UpdatedBy",
                table: "PriceListLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceLists_Users_CreatedBy",
                table: "PriceLists",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceLists_Users_DeletedBy",
                table: "PriceLists",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PriceLists_Users_UpdatedBy",
                table: "PriceLists",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessedIdempotencyKeys_Users_CreatedBy",
                table: "ProcessedIdempotencyKeys",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessedIdempotencyKeys_Users_DeletedBy",
                table: "ProcessedIdempotencyKeys",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessedIdempotencyKeys_Users_UpdatedBy",
                table: "ProcessedIdempotencyKeys",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_Users_CreatedBy",
                table: "ProductionOrders",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_Users_DeletedBy",
                table: "ProductionOrders",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_Users_ExecutedByUserId",
                table: "ProductionOrders",
                column: "ExecutedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_Users_UpdatedBy",
                table: "ProductionOrders",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionSalesModeSettings_Users_CreatedBy",
                table: "ProductionSalesModeSettings",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionSalesModeSettings_Users_DeletedBy",
                table: "ProductionSalesModeSettings",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionSalesModeSettings_Users_UpdatedBy",
                table: "ProductionSalesModeSettings",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseCycleSettings_Users_CreatedBy",
                table: "PurchaseCycleSettings",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseCycleSettings_Users_DeletedBy",
                table: "PurchaseCycleSettings",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseCycleSettings_Users_UpdatedBy",
                table: "PurchaseCycleSettings",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseExpenses_Users_CreatedBy",
                table: "PurchaseExpenses",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseExpenses_Users_DeletedBy",
                table: "PurchaseExpenses",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseExpenses_Users_UpdatedBy",
                table: "PurchaseExpenses",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceLines_Users_CreatedBy",
                table: "PurchaseInvoiceLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceLines_Users_DeletedBy",
                table: "PurchaseInvoiceLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoiceLines_Users_UpdatedBy",
                table: "PurchaseInvoiceLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Users_CreatedBy",
                table: "PurchaseInvoices",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Users_DeletedBy",
                table: "PurchaseInvoices",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseInvoices_Users_UpdatedBy",
                table: "PurchaseInvoices",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLines_Users_CreatedBy",
                table: "PurchaseOrderLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLines_Users_DeletedBy",
                table: "PurchaseOrderLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderLines_Users_UpdatedBy",
                table: "PurchaseOrderLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Users_CreatedBy",
                table: "PurchaseOrders",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Users_DeletedBy",
                table: "PurchaseOrders",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Users_UpdatedBy",
                table: "PurchaseOrders",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequestLines_Users_CreatedBy",
                table: "PurchaseRequestLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequestLines_Users_DeletedBy",
                table: "PurchaseRequestLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequestLines_Users_UpdatedBy",
                table: "PurchaseRequestLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequests_Users_CreatedBy",
                table: "PurchaseRequests",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequests_Users_DeletedBy",
                table: "PurchaseRequests",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequests_Users_RequestedByUserId",
                table: "PurchaseRequests",
                column: "RequestedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequests_Users_UpdatedBy",
                table: "PurchaseRequests",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturnLines_Users_CreatedBy",
                table: "PurchaseReturnLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturnLines_Users_DeletedBy",
                table: "PurchaseReturnLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturnLines_Users_UpdatedBy",
                table: "PurchaseReturnLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Users_CreatedBy",
                table: "PurchaseReturns",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Users_DeletedBy",
                table: "PurchaseReturns",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturns_Users_UpdatedBy",
                table: "PurchaseReturns",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QRTicketLines_Users_CreatedBy",
                table: "QRTicketLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QRTicketLines_Users_DeletedBy",
                table: "QRTicketLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QRTicketLines_Users_UpdatedBy",
                table: "QRTicketLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QRTickets_Users_CreatedBy",
                table: "QRTickets",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QRTickets_Users_DeletedBy",
                table: "QRTickets",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QRTickets_Users_UpdatedBy",
                table: "QRTickets",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeLines_Users_CreatedBy",
                table: "RecipeLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeLines_Users_DeletedBy",
                table: "RecipeLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeLines_Users_UpdatedBy",
                table: "RecipeLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Users_CreatedBy",
                table: "Recipes",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Users_DeletedBy",
                table: "Recipes",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Recipes_Users_UpdatedBy",
                table: "Recipes",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Users_CreatedBy",
                table: "RefreshTokens",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Users_DeletedBy",
                table: "RefreshTokens",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RefreshTokens_Users_UpdatedBy",
                table: "RefreshTokens",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestsForQuotation_Users_CreatedBy",
                table: "RequestsForQuotation",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestsForQuotation_Users_DeletedBy",
                table: "RequestsForQuotation",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RequestsForQuotation_Users_UpdatedBy",
                table: "RequestsForQuotation",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RFQLines_Users_CreatedBy",
                table: "RFQLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RFQLines_Users_DeletedBy",
                table: "RFQLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RFQLines_Users_UpdatedBy",
                table: "RFQLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RFQSupplierQuotes_Users_CreatedBy",
                table: "RFQSupplierQuotes",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RFQSupplierQuotes_Users_DeletedBy",
                table: "RFQSupplierQuotes",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RFQSupplierQuotes_Users_UpdatedBy",
                table: "RFQSupplierQuotes",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RFQSuppliers_Users_CreatedBy",
                table: "RFQSuppliers",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RFQSuppliers_Users_DeletedBy",
                table: "RFQSuppliers",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RFQSuppliers_Users_UpdatedBy",
                table: "RFQSuppliers",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Users_CreatedBy",
                table: "Roles",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Users_DeletedBy",
                table: "Roles",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Roles_Users_UpdatedBy",
                table: "Roles",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesCycleSettings_Users_CreatedBy",
                table: "SalesCycleSettings",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesCycleSettings_Users_DeletedBy",
                table: "SalesCycleSettings",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesCycleSettings_Users_UpdatedBy",
                table: "SalesCycleSettings",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoiceLines_Users_CreatedBy",
                table: "SalesInvoiceLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoiceLines_Users_DeletedBy",
                table: "SalesInvoiceLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoiceLines_Users_UpdatedBy",
                table: "SalesInvoiceLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Users_CreatedBy",
                table: "SalesInvoices",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Users_DeletedBy",
                table: "SalesInvoices",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesInvoices_Users_UpdatedBy",
                table: "SalesInvoices",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrderLines_Users_CreatedBy",
                table: "SalesOrderLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrderLines_Users_DeletedBy",
                table: "SalesOrderLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrderLines_Users_UpdatedBy",
                table: "SalesOrderLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Users_CreatedBy",
                table: "SalesOrders",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Users_DeletedBy",
                table: "SalesOrders",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_Users_UpdatedBy",
                table: "SalesOrders",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesQuoteLines_Users_CreatedBy",
                table: "SalesQuoteLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesQuoteLines_Users_DeletedBy",
                table: "SalesQuoteLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesQuoteLines_Users_UpdatedBy",
                table: "SalesQuoteLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesQuotes_Users_CreatedBy",
                table: "SalesQuotes",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesQuotes_Users_DeletedBy",
                table: "SalesQuotes",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesQuotes_Users_UpdatedBy",
                table: "SalesQuotes",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturnLines_Users_CreatedBy",
                table: "SalesReturnLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturnLines_Users_DeletedBy",
                table: "SalesReturnLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturnLines_Users_UpdatedBy",
                table: "SalesReturnLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturns_Users_CreatedBy",
                table: "SalesReturns",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturns_Users_DeletedBy",
                table: "SalesReturns",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturns_Users_UpdatedBy",
                table: "SalesReturns",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScreenPermissions_Users_CreatedBy",
                table: "ScreenPermissions",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScreenPermissions_Users_DeletedBy",
                table: "ScreenPermissions",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ScreenPermissions_Users_UpdatedBy",
                table: "ScreenPermissions",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Users_CreatedBy",
                table: "ShiftAssignments",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Users_DeletedBy",
                table: "ShiftAssignments",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Users_UpdatedBy",
                table: "ShiftAssignments",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftAssignments_Users_UserId",
                table: "ShiftAssignments",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftDenominationCounts_Users_CreatedBy",
                table: "ShiftDenominationCounts",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftDenominationCounts_Users_DeletedBy",
                table: "ShiftDenominationCounts",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShiftDenominationCounts_Users_UpdatedBy",
                table: "ShiftDenominationCounts",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Users_CashierUserId",
                table: "Shifts",
                column: "CashierUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Users_ClosedByUserId",
                table: "Shifts",
                column: "ClosedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Users_CreatedBy",
                table: "Shifts",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Users_DeletedBy",
                table: "Shifts",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Shifts_Users_UpdatedBy",
                table: "Shifts",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShortagePolicies_Users_CreatedBy",
                table: "ShortagePolicies",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShortagePolicies_Users_DeletedBy",
                table: "ShortagePolicies",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ShortagePolicies_Users_UpdatedBy",
                table: "ShortagePolicies",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockBalances_Users_CreatedBy",
                table: "StockBalances",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockBalances_Users_DeletedBy",
                table: "StockBalances",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockBalances_Users_UpdatedBy",
                table: "StockBalances",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_Users_CreatedBy",
                table: "StockTransactions",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_Users_DeletedBy",
                table: "StockTransactions",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_Users_UpdatedBy",
                table: "StockTransactions",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierContracts_Users_CreatedBy",
                table: "SupplierContracts",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierContracts_Users_DeletedBy",
                table: "SupplierContracts",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierContracts_Users_UpdatedBy",
                table: "SupplierContracts",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierEvaluations_Users_CreatedBy",
                table: "SupplierEvaluations",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierEvaluations_Users_DeletedBy",
                table: "SupplierEvaluations",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierEvaluations_Users_UpdatedBy",
                table: "SupplierEvaluations",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPriceHistories_Users_CreatedBy",
                table: "SupplierPriceHistories",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPriceHistories_Users_DeletedBy",
                table: "SupplierPriceHistories",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierPriceHistories_Users_UpdatedBy",
                table: "SupplierPriceHistories",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Users_CreatedBy",
                table: "Suppliers",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Users_DeletedBy",
                table: "Suppliers",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Users_UpdatedBy",
                table: "Suppliers",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemSettings_Users_CreatedBy",
                table: "SystemSettings",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemSettings_Users_DeletedBy",
                table: "SystemSettings",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemSettings_Users_UpdatedBy",
                table: "SystemSettings",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_Users_CreatedBy",
                table: "Tables",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_Users_DeletedBy",
                table: "Tables",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_Users_UpdatedBy",
                table: "Tables",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TreasuryTransfers_Users_CreatedBy",
                table: "TreasuryTransfers",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TreasuryTransfers_Users_DeletedBy",
                table: "TreasuryTransfers",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TreasuryTransfers_Users_UpdatedBy",
                table: "TreasuryTransfers",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitsOfMeasure_Users_CreatedBy",
                table: "UnitsOfMeasure",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitsOfMeasure_Users_DeletedBy",
                table: "UnitsOfMeasure",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UnitsOfMeasure_Users_UpdatedBy",
                table: "UnitsOfMeasure",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Users_AssignedByUserId",
                table: "UserRoles",
                column: "AssignedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Users_CreatedBy",
                table: "UserRoles",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Users_DeletedBy",
                table: "UserRoles",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Users_UpdatedBy",
                table: "UserRoles",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_CreatedBy",
                table: "Users",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_DeletedBy",
                table: "Users",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_UpdatedBy",
                table: "Users",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserScopes_Users_CreatedBy",
                table: "UserScopes",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserScopes_Users_DeletedBy",
                table: "UserScopes",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserScopes_Users_GrantedByUserId",
                table: "UserScopes",
                column: "GrantedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserScopes_Users_UpdatedBy",
                table: "UserScopes",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Users_CreatedBy",
                table: "Vouchers",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Users_DeletedBy",
                table: "Vouchers",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vouchers_Users_UpdatedBy",
                table: "Vouchers",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseDocumentLines_Users_CreatedBy",
                table: "WarehouseDocumentLines",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseDocumentLines_Users_DeletedBy",
                table: "WarehouseDocumentLines",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseDocumentLines_Users_UpdatedBy",
                table: "WarehouseDocumentLines",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseDocuments_Users_CreatedBy",
                table: "WarehouseDocuments",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseDocuments_Users_DeletedBy",
                table: "WarehouseDocuments",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WarehouseDocuments_Users_UpdatedBy",
                table: "WarehouseDocuments",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Users_CreatedBy",
                table: "Warehouses",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Users_DeletedBy",
                table: "Warehouses",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Warehouses_Users_UpdatedBy",
                table: "Warehouses",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WasteRecords_Users_CreatedBy",
                table: "WasteRecords",
                column: "CreatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WasteRecords_Users_DeletedBy",
                table: "WasteRecords",
                column: "DeletedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WasteRecords_Users_UpdatedBy",
                table: "WasteRecords",
                column: "UpdatedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccountDimensionLinks_Users_CreatedBy",
                table: "AccountDimensionLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountDimensionLinks_Users_DeletedBy",
                table: "AccountDimensionLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountDimensionLinks_Users_UpdatedBy",
                table: "AccountDimensionLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountingPeriods_Users_ClosedByUserId",
                table: "AccountingPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountingPeriods_Users_CreatedBy",
                table: "AccountingPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountingPeriods_Users_DeletedBy",
                table: "AccountingPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountingPeriods_Users_UpdatedBy",
                table: "AccountingPeriods");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountOpeningBalanceBatches_Users_CreatedBy",
                table: "AccountOpeningBalanceBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountOpeningBalanceBatches_Users_DeletedBy",
                table: "AccountOpeningBalanceBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountOpeningBalanceBatches_Users_UpdatedBy",
                table: "AccountOpeningBalanceBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountOpeningBalanceLines_Users_CreatedBy",
                table: "AccountOpeningBalanceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountOpeningBalanceLines_Users_DeletedBy",
                table: "AccountOpeningBalanceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_AccountOpeningBalanceLines_Users_UpdatedBy",
                table: "AccountOpeningBalanceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Users_CreatedBy",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Users_DeletedBy",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Accounts_Users_UpdatedBy",
                table: "Accounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_Users_CreatedBy",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_Users_DeletedBy",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_Users_UpdatedBy",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_BankReconciliationLines_Users_CreatedBy",
                table: "BankReconciliationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BankReconciliationLines_Users_DeletedBy",
                table: "BankReconciliationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BankReconciliationLines_Users_UpdatedBy",
                table: "BankReconciliationLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BankReconciliationRuns_Users_CreatedBy",
                table: "BankReconciliationRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_BankReconciliationRuns_Users_DeletedBy",
                table: "BankReconciliationRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_BankReconciliationRuns_Users_ImportedByUserId",
                table: "BankReconciliationRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_BankReconciliationRuns_Users_UpdatedBy",
                table: "BankReconciliationRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_BlendTypes_Users_CreatedBy",
                table: "BlendTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_BlendTypes_Users_DeletedBy",
                table: "BlendTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_BlendTypes_Users_UpdatedBy",
                table: "BlendTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Users_CreatedBy",
                table: "Branches");

            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Users_DeletedBy",
                table: "Branches");

            migrationBuilder.DropForeignKey(
                name: "FK_Branches_Users_UpdatedBy",
                table: "Branches");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchItemLimits_Users_CreatedBy",
                table: "BranchItemLimits");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchItemLimits_Users_DeletedBy",
                table: "BranchItemLimits");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchItemLimits_Users_UpdatedBy",
                table: "BranchItemLimits");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchPOSSettings_Users_CreatedBy",
                table: "BranchPOSSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchPOSSettings_Users_DeletedBy",
                table: "BranchPOSSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchPOSSettings_Users_UpdatedBy",
                table: "BranchPOSSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchRequestLines_Users_CreatedBy",
                table: "BranchRequestLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchRequestLines_Users_DeletedBy",
                table: "BranchRequestLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchRequestLines_Users_UpdatedBy",
                table: "BranchRequestLines");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchRequests_Users_CreatedBy",
                table: "BranchRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchRequests_Users_DeletedBy",
                table: "BranchRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchRequests_Users_RequestedByUserId",
                table: "BranchRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_BranchRequests_Users_UpdatedBy",
                table: "BranchRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_CashReconciliationDenominations_Users_CreatedBy",
                table: "CashReconciliationDenominations");

            migrationBuilder.DropForeignKey(
                name: "FK_CashReconciliationDenominations_Users_DeletedBy",
                table: "CashReconciliationDenominations");

            migrationBuilder.DropForeignKey(
                name: "FK_CashReconciliationDenominations_Users_UpdatedBy",
                table: "CashReconciliationDenominations");

            migrationBuilder.DropForeignKey(
                name: "FK_CashReconciliations_Users_ApprovedByUserId",
                table: "CashReconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_CashReconciliations_Users_CreatedBy",
                table: "CashReconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_CashReconciliations_Users_DeletedBy",
                table: "CashReconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_CashReconciliations_Users_UpdatedBy",
                table: "CashReconciliations");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckLines_Users_CreatedBy",
                table: "CheckLines");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckLines_Users_DeletedBy",
                table: "CheckLines");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckLines_Users_UpdatedBy",
                table: "CheckLines");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckLineVoids_Users_CreatedBy",
                table: "CheckLineVoids");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckLineVoids_Users_DeletedBy",
                table: "CheckLineVoids");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckLineVoids_Users_UpdatedBy",
                table: "CheckLineVoids");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckLineVoids_Users_VoidedByUserId",
                table: "CheckLineVoids");

            migrationBuilder.DropForeignKey(
                name: "FK_Checks_Users_CreatedBy",
                table: "Checks");

            migrationBuilder.DropForeignKey(
                name: "FK_Checks_Users_DeletedBy",
                table: "Checks");

            migrationBuilder.DropForeignKey(
                name: "FK_Checks_Users_UpdatedBy",
                table: "Checks");

            migrationBuilder.DropForeignKey(
                name: "FK_CodingRules_Users_CreatedBy",
                table: "CodingRules");

            migrationBuilder.DropForeignKey(
                name: "FK_CodingRules_Users_DeletedBy",
                table: "CodingRules");

            migrationBuilder.DropForeignKey(
                name: "FK_CodingRules_Users_UpdatedBy",
                table: "CodingRules");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Users_CreatedBy",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Users_DeletedBy",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_Companies_Users_UpdatedBy",
                table: "Companies");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyAccountMappings_Users_CreatedBy",
                table: "CompanyAccountMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyAccountMappings_Users_DeletedBy",
                table: "CompanyAccountMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_CompanyAccountMappings_Users_UpdatedBy",
                table: "CompanyAccountMappings");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractItems_Users_CreatedBy",
                table: "ContractItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractItems_Users_DeletedBy",
                table: "ContractItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ContractItems_Users_UpdatedBy",
                table: "ContractItems");

            migrationBuilder.DropForeignKey(
                name: "FK_CostCenterDimensions_Users_CreatedBy",
                table: "CostCenterDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_CostCenterDimensions_Users_DeletedBy",
                table: "CostCenterDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_CostCenterDimensions_Users_UpdatedBy",
                table: "CostCenterDimensions");

            migrationBuilder.DropForeignKey(
                name: "FK_CostCenterDimensionValues_Users_CreatedBy",
                table: "CostCenterDimensionValues");

            migrationBuilder.DropForeignKey(
                name: "FK_CostCenterDimensionValues_Users_DeletedBy",
                table: "CostCenterDimensionValues");

            migrationBuilder.DropForeignKey(
                name: "FK_CostCenterDimensionValues_Users_UpdatedBy",
                table: "CostCenterDimensionValues");

            migrationBuilder.DropForeignKey(
                name: "FK_Currencies_Users_CreatedBy",
                table: "Currencies");

            migrationBuilder.DropForeignKey(
                name: "FK_Currencies_Users_DeletedBy",
                table: "Currencies");

            migrationBuilder.DropForeignKey(
                name: "FK_Currencies_Users_UpdatedBy",
                table: "Currencies");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodyOfficers_Users_CreatedBy",
                table: "CustodyOfficers");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodyOfficers_Users_DeletedBy",
                table: "CustodyOfficers");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodyOfficers_Users_UpdatedBy",
                table: "CustodyOfficers");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodyRegisters_Users_CreatedBy",
                table: "CustodyRegisters");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodyRegisters_Users_DeletedBy",
                table: "CustodyRegisters");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodyRegisters_Users_UpdatedBy",
                table: "CustodyRegisters");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodySettlementLines_Users_CreatedBy",
                table: "CustodySettlementLines");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodySettlementLines_Users_DeletedBy",
                table: "CustodySettlementLines");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodySettlementLines_Users_UpdatedBy",
                table: "CustodySettlementLines");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodySettlements_Users_CreatedBy",
                table: "CustodySettlements");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodySettlements_Users_DeletedBy",
                table: "CustodySettlements");

            migrationBuilder.DropForeignKey(
                name: "FK_CustodySettlements_Users_UpdatedBy",
                table: "CustodySettlements");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Users_CreatedBy",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Users_DeletedBy",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Users_UpdatedBy",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryOrderLines_Users_CreatedBy",
                table: "DeliveryOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryOrderLines_Users_DeletedBy",
                table: "DeliveryOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryOrderLines_Users_UpdatedBy",
                table: "DeliveryOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryOrders_Users_CreatedBy",
                table: "DeliveryOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryOrders_Users_DeletedBy",
                table: "DeliveryOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryOrders_Users_UpdatedBy",
                table: "DeliveryOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryPlatformOrders_Users_CreatedBy",
                table: "DeliveryPlatformOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryPlatformOrders_Users_DeletedBy",
                table: "DeliveryPlatformOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryPlatformOrders_Users_UpdatedBy",
                table: "DeliveryPlatformOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_Discounts_Users_CreatedBy",
                table: "Discounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Discounts_Users_DeletedBy",
                table: "Discounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Discounts_Users_UpdatedBy",
                table: "Discounts");

            migrationBuilder.DropForeignKey(
                name: "FK_DrawerExpenses_Users_CreatedBy",
                table: "DrawerExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_DrawerExpenses_Users_DeletedBy",
                table: "DrawerExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_DrawerExpenses_Users_UpdatedBy",
                table: "DrawerExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_DrawerMovements_Users_ApprovedByUserId",
                table: "DrawerMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_DrawerMovements_Users_CreatedBy",
                table: "DrawerMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_DrawerMovements_Users_DeletedBy",
                table: "DrawerMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_DrawerMovements_Users_UpdatedBy",
                table: "DrawerMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldLabels_Users_CreatedBy",
                table: "FieldLabels");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldLabels_Users_DeletedBy",
                table: "FieldLabels");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldLabels_Users_UpdatedBy",
                table: "FieldLabels");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldPermissions_Users_CreatedBy",
                table: "FieldPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldPermissions_Users_DeletedBy",
                table: "FieldPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_FieldPermissions_Users_UpdatedBy",
                table: "FieldPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptLines_Users_CreatedBy",
                table: "GoodsReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptLines_Users_DeletedBy",
                table: "GoodsReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceiptLines_Users_UpdatedBy",
                table: "GoodsReceiptLines");

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_Users_CreatedBy",
                table: "GoodsReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_Users_DeletedBy",
                table: "GoodsReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_GoodsReceipts_Users_UpdatedBy",
                table: "GoodsReceipts");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCountLines_Users_CreatedBy",
                table: "InventoryCountLines");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCountLines_Users_DeletedBy",
                table: "InventoryCountLines");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCountLines_Users_UpdatedBy",
                table: "InventoryCountLines");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCounts_Users_CreatedBy",
                table: "InventoryCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCounts_Users_DeletedBy",
                table: "InventoryCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryCounts_Users_UpdatedBy",
                table: "InventoryCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_InventorySettingsRows_Users_CreatedBy",
                table: "InventorySettingsRows");

            migrationBuilder.DropForeignKey(
                name: "FK_InventorySettingsRows_Users_DeletedBy",
                table: "InventorySettingsRows");

            migrationBuilder.DropForeignKey(
                name: "FK_InventorySettingsRows_Users_UpdatedBy",
                table: "InventorySettingsRows");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemGroups_Users_CreatedBy",
                table: "ItemGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemGroups_Users_DeletedBy",
                table: "ItemGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemGroups_Users_UpdatedBy",
                table: "ItemGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Users_CreatedBy",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Users_DeletedBy",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Users_UpdatedBy",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemUnitConversions_Users_CreatedBy",
                table: "ItemUnitConversions");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemUnitConversions_Users_DeletedBy",
                table: "ItemUnitConversions");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemUnitConversions_Users_UpdatedBy",
                table: "ItemUnitConversions");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemWarehouseSettings_Users_CreatedBy",
                table: "ItemWarehouseSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemWarehouseSettings_Users_DeletedBy",
                table: "ItemWarehouseSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_ItemWarehouseSettings_Users_UpdatedBy",
                table: "ItemWarehouseSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_Users_CreatedBy",
                table: "JournalEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_Users_DeletedBy",
                table: "JournalEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_Users_PostedBy",
                table: "JournalEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntries_Users_UpdatedBy",
                table: "JournalEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntryLineDimensionValues_Users_CreatedBy",
                table: "JournalEntryLineDimensionValues");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntryLineDimensionValues_Users_DeletedBy",
                table: "JournalEntryLineDimensionValues");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntryLineDimensionValues_Users_UpdatedBy",
                table: "JournalEntryLineDimensionValues");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntryLines_Users_CreatedBy",
                table: "JournalEntryLines");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntryLines_Users_DeletedBy",
                table: "JournalEntryLines");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntryLines_Users_UpdatedBy",
                table: "JournalEntryLines");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntryTemplateSnapshots_Users_CreatedBy",
                table: "JournalEntryTemplateSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntryTemplateSnapshots_Users_DeletedBy",
                table: "JournalEntryTemplateSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_JournalEntryTemplateSnapshots_Users_UpdatedBy",
                table: "JournalEntryTemplateSnapshots");

            migrationBuilder.DropForeignKey(
                name: "FK_LoginAttempts_Users_CreatedBy",
                table: "LoginAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_LoginAttempts_Users_DeletedBy",
                table: "LoginAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_LoginAttempts_Users_UpdatedBy",
                table: "LoginAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyProgramSettings_Users_CreatedBy",
                table: "LoyaltyProgramSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyProgramSettings_Users_DeletedBy",
                table: "LoyaltyProgramSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyProgramSettings_Users_UpdatedBy",
                table: "LoyaltyProgramSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyTiers_Users_CreatedBy",
                table: "LoyaltyTiers");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyTiers_Users_DeletedBy",
                table: "LoyaltyTiers");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyTiers_Users_UpdatedBy",
                table: "LoyaltyTiers");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyTransactions_Users_CreatedBy",
                table: "LoyaltyTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyTransactions_Users_DeletedBy",
                table: "LoyaltyTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_LoyaltyTransactions_Users_UpdatedBy",
                table: "LoyaltyTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Users_CreatedBy",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Users_DeletedBy",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_Users_UpdatedBy",
                table: "MenuItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentMethods_Users_CreatedBy",
                table: "PaymentMethods");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentMethods_Users_DeletedBy",
                table: "PaymentMethods");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentMethods_Users_UpdatedBy",
                table: "PaymentMethods");

            migrationBuilder.DropForeignKey(
                name: "FK_PeriodCloseChecklistItems_Users_CreatedBy",
                table: "PeriodCloseChecklistItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PeriodCloseChecklistItems_Users_DeletedBy",
                table: "PeriodCloseChecklistItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PeriodCloseChecklistItems_Users_UpdatedBy",
                table: "PeriodCloseChecklistItems");

            migrationBuilder.DropForeignKey(
                name: "FK_POSCategories_Users_CreatedBy",
                table: "POSCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_POSCategories_Users_DeletedBy",
                table: "POSCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_POSCategories_Users_UpdatedBy",
                table: "POSCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_POSInvoiceLines_Users_CreatedBy",
                table: "POSInvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_POSInvoiceLines_Users_DeletedBy",
                table: "POSInvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_POSInvoiceLines_Users_UpdatedBy",
                table: "POSInvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_POSInvoices_Users_CreatedBy",
                table: "POSInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_POSInvoices_Users_DeletedBy",
                table: "POSInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_POSInvoices_Users_UpdatedBy",
                table: "POSInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_POSPaymentMethodConfigs_Users_CreatedBy",
                table: "POSPaymentMethodConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_POSPaymentMethodConfigs_Users_DeletedBy",
                table: "POSPaymentMethodConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_POSPaymentMethodConfigs_Users_UpdatedBy",
                table: "POSPaymentMethodConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_POSPayments_Users_CreatedBy",
                table: "POSPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_POSPayments_Users_DeletedBy",
                table: "POSPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_POSPayments_Users_UpdatedBy",
                table: "POSPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_POSReturnLines_Users_CreatedBy",
                table: "POSReturnLines");

            migrationBuilder.DropForeignKey(
                name: "FK_POSReturnLines_Users_DeletedBy",
                table: "POSReturnLines");

            migrationBuilder.DropForeignKey(
                name: "FK_POSReturnLines_Users_UpdatedBy",
                table: "POSReturnLines");

            migrationBuilder.DropForeignKey(
                name: "FK_POSReturns_Users_CreatedBy",
                table: "POSReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_POSReturns_Users_DeletedBy",
                table: "POSReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_POSReturns_Users_UpdatedBy",
                table: "POSReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_POSTerminals_Users_CreatedBy",
                table: "POSTerminals");

            migrationBuilder.DropForeignKey(
                name: "FK_POSTerminals_Users_DeletedBy",
                table: "POSTerminals");

            migrationBuilder.DropForeignKey(
                name: "FK_POSTerminals_Users_UpdatedBy",
                table: "POSTerminals");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingFailures_Users_CreatedBy",
                table: "PostingFailures");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingFailures_Users_DeletedBy",
                table: "PostingFailures");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingFailures_Users_UpdatedBy",
                table: "PostingFailures");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingFailures_Users_UserId",
                table: "PostingFailures");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingTemplateLineCostCenters_Users_CreatedBy",
                table: "PostingTemplateLineCostCenters");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingTemplateLineCostCenters_Users_DeletedBy",
                table: "PostingTemplateLineCostCenters");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingTemplateLineCostCenters_Users_UpdatedBy",
                table: "PostingTemplateLineCostCenters");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingTemplateLines_Users_CreatedBy",
                table: "PostingTemplateLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingTemplateLines_Users_DeletedBy",
                table: "PostingTemplateLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingTemplateLines_Users_UpdatedBy",
                table: "PostingTemplateLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingTemplates_Users_CreatedBy",
                table: "PostingTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingTemplates_Users_DeletedBy",
                table: "PostingTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_PostingTemplates_Users_UpdatedBy",
                table: "PostingTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceListBranches_Users_CreatedBy",
                table: "PriceListBranches");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceListBranches_Users_DeletedBy",
                table: "PriceListBranches");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceListBranches_Users_UpdatedBy",
                table: "PriceListBranches");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceListLines_Users_CreatedBy",
                table: "PriceListLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceListLines_Users_DeletedBy",
                table: "PriceListLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceListLines_Users_UpdatedBy",
                table: "PriceListLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceLists_Users_CreatedBy",
                table: "PriceLists");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceLists_Users_DeletedBy",
                table: "PriceLists");

            migrationBuilder.DropForeignKey(
                name: "FK_PriceLists_Users_UpdatedBy",
                table: "PriceLists");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessedIdempotencyKeys_Users_CreatedBy",
                table: "ProcessedIdempotencyKeys");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessedIdempotencyKeys_Users_DeletedBy",
                table: "ProcessedIdempotencyKeys");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessedIdempotencyKeys_Users_UpdatedBy",
                table: "ProcessedIdempotencyKeys");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_Users_CreatedBy",
                table: "ProductionOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_Users_DeletedBy",
                table: "ProductionOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_Users_ExecutedByUserId",
                table: "ProductionOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_Users_UpdatedBy",
                table: "ProductionOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionSalesModeSettings_Users_CreatedBy",
                table: "ProductionSalesModeSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionSalesModeSettings_Users_DeletedBy",
                table: "ProductionSalesModeSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionSalesModeSettings_Users_UpdatedBy",
                table: "ProductionSalesModeSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseCycleSettings_Users_CreatedBy",
                table: "PurchaseCycleSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseCycleSettings_Users_DeletedBy",
                table: "PurchaseCycleSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseCycleSettings_Users_UpdatedBy",
                table: "PurchaseCycleSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseExpenses_Users_CreatedBy",
                table: "PurchaseExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseExpenses_Users_DeletedBy",
                table: "PurchaseExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseExpenses_Users_UpdatedBy",
                table: "PurchaseExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoiceLines_Users_CreatedBy",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoiceLines_Users_DeletedBy",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoiceLines_Users_UpdatedBy",
                table: "PurchaseInvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Users_CreatedBy",
                table: "PurchaseInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Users_DeletedBy",
                table: "PurchaseInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseInvoices_Users_UpdatedBy",
                table: "PurchaseInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_Users_CreatedBy",
                table: "PurchaseOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_Users_DeletedBy",
                table: "PurchaseOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderLines_Users_UpdatedBy",
                table: "PurchaseOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Users_CreatedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Users_DeletedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Users_UpdatedBy",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequestLines_Users_CreatedBy",
                table: "PurchaseRequestLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequestLines_Users_DeletedBy",
                table: "PurchaseRequestLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequestLines_Users_UpdatedBy",
                table: "PurchaseRequestLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequests_Users_CreatedBy",
                table: "PurchaseRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequests_Users_DeletedBy",
                table: "PurchaseRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequests_Users_RequestedByUserId",
                table: "PurchaseRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequests_Users_UpdatedBy",
                table: "PurchaseRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturnLines_Users_CreatedBy",
                table: "PurchaseReturnLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturnLines_Users_DeletedBy",
                table: "PurchaseReturnLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturnLines_Users_UpdatedBy",
                table: "PurchaseReturnLines");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Users_CreatedBy",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Users_DeletedBy",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturns_Users_UpdatedBy",
                table: "PurchaseReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_QRTicketLines_Users_CreatedBy",
                table: "QRTicketLines");

            migrationBuilder.DropForeignKey(
                name: "FK_QRTicketLines_Users_DeletedBy",
                table: "QRTicketLines");

            migrationBuilder.DropForeignKey(
                name: "FK_QRTicketLines_Users_UpdatedBy",
                table: "QRTicketLines");

            migrationBuilder.DropForeignKey(
                name: "FK_QRTickets_Users_CreatedBy",
                table: "QRTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_QRTickets_Users_DeletedBy",
                table: "QRTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_QRTickets_Users_UpdatedBy",
                table: "QRTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeLines_Users_CreatedBy",
                table: "RecipeLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeLines_Users_DeletedBy",
                table: "RecipeLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RecipeLines_Users_UpdatedBy",
                table: "RecipeLines");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Users_CreatedBy",
                table: "Recipes");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Users_DeletedBy",
                table: "Recipes");

            migrationBuilder.DropForeignKey(
                name: "FK_Recipes_Users_UpdatedBy",
                table: "Recipes");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Users_CreatedBy",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Users_DeletedBy",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_RefreshTokens_Users_UpdatedBy",
                table: "RefreshTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestsForQuotation_Users_CreatedBy",
                table: "RequestsForQuotation");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestsForQuotation_Users_DeletedBy",
                table: "RequestsForQuotation");

            migrationBuilder.DropForeignKey(
                name: "FK_RequestsForQuotation_Users_UpdatedBy",
                table: "RequestsForQuotation");

            migrationBuilder.DropForeignKey(
                name: "FK_RFQLines_Users_CreatedBy",
                table: "RFQLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RFQLines_Users_DeletedBy",
                table: "RFQLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RFQLines_Users_UpdatedBy",
                table: "RFQLines");

            migrationBuilder.DropForeignKey(
                name: "FK_RFQSupplierQuotes_Users_CreatedBy",
                table: "RFQSupplierQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_RFQSupplierQuotes_Users_DeletedBy",
                table: "RFQSupplierQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_RFQSupplierQuotes_Users_UpdatedBy",
                table: "RFQSupplierQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_RFQSuppliers_Users_CreatedBy",
                table: "RFQSuppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_RFQSuppliers_Users_DeletedBy",
                table: "RFQSuppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_RFQSuppliers_Users_UpdatedBy",
                table: "RFQSuppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Users_CreatedBy",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Users_DeletedBy",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_Roles_Users_UpdatedBy",
                table: "Roles");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesCycleSettings_Users_CreatedBy",
                table: "SalesCycleSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesCycleSettings_Users_DeletedBy",
                table: "SalesCycleSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesCycleSettings_Users_UpdatedBy",
                table: "SalesCycleSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoiceLines_Users_CreatedBy",
                table: "SalesInvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoiceLines_Users_DeletedBy",
                table: "SalesInvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoiceLines_Users_UpdatedBy",
                table: "SalesInvoiceLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Users_CreatedBy",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Users_DeletedBy",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesInvoices_Users_UpdatedBy",
                table: "SalesInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrderLines_Users_CreatedBy",
                table: "SalesOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrderLines_Users_DeletedBy",
                table: "SalesOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrderLines_Users_UpdatedBy",
                table: "SalesOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Users_CreatedBy",
                table: "SalesOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Users_DeletedBy",
                table: "SalesOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_Users_UpdatedBy",
                table: "SalesOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesQuoteLines_Users_CreatedBy",
                table: "SalesQuoteLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesQuoteLines_Users_DeletedBy",
                table: "SalesQuoteLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesQuoteLines_Users_UpdatedBy",
                table: "SalesQuoteLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesQuotes_Users_CreatedBy",
                table: "SalesQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesQuotes_Users_DeletedBy",
                table: "SalesQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesQuotes_Users_UpdatedBy",
                table: "SalesQuotes");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturnLines_Users_CreatedBy",
                table: "SalesReturnLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturnLines_Users_DeletedBy",
                table: "SalesReturnLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturnLines_Users_UpdatedBy",
                table: "SalesReturnLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturns_Users_CreatedBy",
                table: "SalesReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturns_Users_DeletedBy",
                table: "SalesReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturns_Users_UpdatedBy",
                table: "SalesReturns");

            migrationBuilder.DropForeignKey(
                name: "FK_ScreenPermissions_Users_CreatedBy",
                table: "ScreenPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_ScreenPermissions_Users_DeletedBy",
                table: "ScreenPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_ScreenPermissions_Users_UpdatedBy",
                table: "ScreenPermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Users_CreatedBy",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Users_DeletedBy",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Users_UpdatedBy",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftAssignments_Users_UserId",
                table: "ShiftAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftDenominationCounts_Users_CreatedBy",
                table: "ShiftDenominationCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftDenominationCounts_Users_DeletedBy",
                table: "ShiftDenominationCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_ShiftDenominationCounts_Users_UpdatedBy",
                table: "ShiftDenominationCounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Users_CashierUserId",
                table: "Shifts");

            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Users_ClosedByUserId",
                table: "Shifts");

            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Users_CreatedBy",
                table: "Shifts");

            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Users_DeletedBy",
                table: "Shifts");

            migrationBuilder.DropForeignKey(
                name: "FK_Shifts_Users_UpdatedBy",
                table: "Shifts");

            migrationBuilder.DropForeignKey(
                name: "FK_ShortagePolicies_Users_CreatedBy",
                table: "ShortagePolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_ShortagePolicies_Users_DeletedBy",
                table: "ShortagePolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_ShortagePolicies_Users_UpdatedBy",
                table: "ShortagePolicies");

            migrationBuilder.DropForeignKey(
                name: "FK_StockBalances_Users_CreatedBy",
                table: "StockBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_StockBalances_Users_DeletedBy",
                table: "StockBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_StockBalances_Users_UpdatedBy",
                table: "StockBalances");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_Users_CreatedBy",
                table: "StockTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_Users_DeletedBy",
                table: "StockTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_Users_UpdatedBy",
                table: "StockTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierContracts_Users_CreatedBy",
                table: "SupplierContracts");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierContracts_Users_DeletedBy",
                table: "SupplierContracts");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierContracts_Users_UpdatedBy",
                table: "SupplierContracts");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierEvaluations_Users_CreatedBy",
                table: "SupplierEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierEvaluations_Users_DeletedBy",
                table: "SupplierEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierEvaluations_Users_UpdatedBy",
                table: "SupplierEvaluations");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPriceHistories_Users_CreatedBy",
                table: "SupplierPriceHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPriceHistories_Users_DeletedBy",
                table: "SupplierPriceHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierPriceHistories_Users_UpdatedBy",
                table: "SupplierPriceHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Users_CreatedBy",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Users_DeletedBy",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Users_UpdatedBy",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemSettings_Users_CreatedBy",
                table: "SystemSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemSettings_Users_DeletedBy",
                table: "SystemSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemSettings_Users_UpdatedBy",
                table: "SystemSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Tables_Users_CreatedBy",
                table: "Tables");

            migrationBuilder.DropForeignKey(
                name: "FK_Tables_Users_DeletedBy",
                table: "Tables");

            migrationBuilder.DropForeignKey(
                name: "FK_Tables_Users_UpdatedBy",
                table: "Tables");

            migrationBuilder.DropForeignKey(
                name: "FK_TreasuryTransfers_Users_CreatedBy",
                table: "TreasuryTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_TreasuryTransfers_Users_DeletedBy",
                table: "TreasuryTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_TreasuryTransfers_Users_UpdatedBy",
                table: "TreasuryTransfers");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitsOfMeasure_Users_CreatedBy",
                table: "UnitsOfMeasure");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitsOfMeasure_Users_DeletedBy",
                table: "UnitsOfMeasure");

            migrationBuilder.DropForeignKey(
                name: "FK_UnitsOfMeasure_Users_UpdatedBy",
                table: "UnitsOfMeasure");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Users_AssignedByUserId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Users_CreatedBy",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Users_DeletedBy",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Users_UpdatedBy",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_CreatedBy",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_DeletedBy",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_UpdatedBy",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_UserScopes_Users_CreatedBy",
                table: "UserScopes");

            migrationBuilder.DropForeignKey(
                name: "FK_UserScopes_Users_DeletedBy",
                table: "UserScopes");

            migrationBuilder.DropForeignKey(
                name: "FK_UserScopes_Users_GrantedByUserId",
                table: "UserScopes");

            migrationBuilder.DropForeignKey(
                name: "FK_UserScopes_Users_UpdatedBy",
                table: "UserScopes");

            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Users_CreatedBy",
                table: "Vouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Users_DeletedBy",
                table: "Vouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_Vouchers_Users_UpdatedBy",
                table: "Vouchers");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseDocumentLines_Users_CreatedBy",
                table: "WarehouseDocumentLines");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseDocumentLines_Users_DeletedBy",
                table: "WarehouseDocumentLines");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseDocumentLines_Users_UpdatedBy",
                table: "WarehouseDocumentLines");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseDocuments_Users_CreatedBy",
                table: "WarehouseDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseDocuments_Users_DeletedBy",
                table: "WarehouseDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_WarehouseDocuments_Users_UpdatedBy",
                table: "WarehouseDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Users_CreatedBy",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Users_DeletedBy",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_Warehouses_Users_UpdatedBy",
                table: "Warehouses");

            migrationBuilder.DropForeignKey(
                name: "FK_WasteRecords_Users_CreatedBy",
                table: "WasteRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_WasteRecords_Users_DeletedBy",
                table: "WasteRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_WasteRecords_Users_UpdatedBy",
                table: "WasteRecords");

            migrationBuilder.DropTable(
                name: "AuditLogsArchive");

            migrationBuilder.DropIndex(
                name: "IX_UserScopes_GrantedByUserId",
                table: "UserScopes");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_AssignedByUserId",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_CashierUserId",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Shifts_ClosedByUserId",
                table: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_ShiftAssignments_UserId",
                table: "ShiftAssignments");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequests_RequestedByUserId",
                table: "PurchaseRequests");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_ExecutedByUserId",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_PostingFailures_UserId",
                table: "PostingFailures");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_PostedBy",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_DrawerMovements_ApprovedByUserId",
                table: "DrawerMovements");

            migrationBuilder.DropIndex(
                name: "IX_CheckLineVoids_VoidedByUserId",
                table: "CheckLineVoids");

            migrationBuilder.DropIndex(
                name: "IX_CashReconciliations_ApprovedByUserId",
                table: "CashReconciliations");

            migrationBuilder.DropIndex(
                name: "IX_BranchRequests_RequestedByUserId",
                table: "BranchRequests");

            migrationBuilder.DropIndex(
                name: "IX_BankReconciliationRuns_ImportedByUserId",
                table: "BankReconciliationRuns");

            migrationBuilder.DropIndex(
                name: "IX_AccountingPeriods_ClosedByUserId",
                table: "AccountingPeriods");
        }
    }
}
