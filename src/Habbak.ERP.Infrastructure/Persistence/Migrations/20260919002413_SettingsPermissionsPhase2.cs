using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SettingsPermissionsPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAtUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            // The security screens' menu items. Only for a database whose menu is already seeded — a
            // fresh one gets them from MenuItemSeedData, and inserting here would make the seeder
            // think the menu exists and skip the rest of it.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM MenuItems) AND NOT EXISTS (SELECT 1 FROM MenuItems WHERE Code = N'SETTINGS_USERS')
BEGIN
    DECLARE @now datetime2 = SYSUTCDATETIME();
    DECLARE @settings bigint = (SELECT Id FROM MenuItems WHERE Code = N'SETTINGS');
    INSERT INTO MenuItems (Code, NameAr, NameEn, ParentId, DisplayOrder, RouteKey, IconKey, IsActive, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, IsDeleted)
    VALUES
        (N'SETTINGS_USERS',          N'المستخدمون',          N'Users',               @settings, 4, N'/settings/users',          NULL, 1, @now, 0, @now, 0, 0),
        (N'SETTINGS_ROLES',          N'الأدوار والصلاحيات',  N'Roles & Permissions', @settings, 5, N'/settings/roles',          NULL, 1, @now, 0, @now, 0, 0),
        (N'SETTINGS_SESSIONS',       N'الجلسات النشطة',      N'Active Sessions',     @settings, 6, N'/settings/sessions',       NULL, 1, @now, 0, @now, 0, 0),
        (N'SETTINGS_LOGIN_ATTEMPTS', N'محاولات الدخول',      N'Login Attempts',      @settings, 7, N'/settings/login-attempts', NULL, 1, @now, 0, @now, 0, 0),
        (N'SETTINGS_AUDIT_LOG',      N'سجل المراجعة',        N'Audit Log',           @settings, 8, N'/settings/audit-log',      NULL, 1, @now, 0, @now, 0, 0),
        (N'SETTINGS_SECURITY',       N'إعدادات الأمان',      N'Security Settings',   @settings, 9, N'/settings/security',       NULL, 1, @now, 0, @now, 0, 0);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM MenuItems WHERE Code IN (N'SETTINGS_USERS', N'SETTINGS_ROLES', N'SETTINGS_SESSIONS', N'SETTINGS_LOGIN_ATTEMPTS', N'SETTINGS_AUDIT_LOG', N'SETTINGS_SECURITY');");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAtUtc",
                table: "Users");
        }
    }
}
