using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FieldAndButtonPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FieldPermissions_RoleId_EntityType_FieldName",
                table: "FieldPermissions");

            // 1) ScreenCode nullable first, so the existing rows can be given theirs.
            migrationBuilder.AddColumn<string>(
                name: "ScreenCode",
                table: "FieldPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // 2) A field rule used to hold on every screen showing the field: it now becomes one row
            //    per screen that lists it (the catalog as of this migration — a snapshot, not the live
            //    class). The first screen takes the existing row, the others get copies.
            //    A rule for a field no screen lists any more (Employee, FixedAsset — no screens exist)
            //    could never apply: it is kept, soft-deleted, under UNMAPPED.
            const string map = """
                (VALUES
                    ('SALES_CUSTOMERS',   'Customer', 'CreditLimit', 1), ('SALES_INVOICES', 'Customer', 'CreditLimit', 2),
                    ('SALES_CUSTOMERS',   'Customer', 'Phone', 1),       ('SALES_INVOICES', 'Customer', 'Phone', 2),
                    ('SALES_CUSTOMERS',   'Customer', 'Email', 1),       ('SALES_INVOICES', 'Customer', 'Email', 2),
                    ('SALES_INVOICES',    'SalesInvoice', 'DiscountAmount', 1),
                    ('PURCHASING_SUPPLIERS', 'Supplier', 'CreditLimit', 1),
                    ('PURCHASING_SUPPLIERS', 'Supplier', 'Phone', 1),
                    ('PURCHASING_SUPPLIERS', 'Supplier', 'Email', 1),
                    ('POS_SHIFT_CONSOLE', 'Shift', 'ExpectedClosingCashAmount', 1), ('POS_SHIFTS', 'Shift', 'ExpectedClosingCashAmount', 2),
                    ('POS_TABLE_BOARD',   'POSPayment', 'CardTransactionReference', 1), ('POS_RETURNS', 'POSPayment', 'CardTransactionReference', 2)
                ) AS m(ScreenCode, EntityType, FieldName, Rn)
                """;

            migrationBuilder.Sql($"""
                INSERT INTO FieldPermissions
                    (CompanyId, RoleId, ScreenCode, EntityType, FieldName, CanView, CanEdit, RequiresAuditLog,
                     CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, IsDeleted, DeletedAtUtc, DeletedBy)
                SELECT fp.CompanyId, fp.RoleId, m.ScreenCode, fp.EntityType, fp.FieldName, fp.CanView, fp.CanEdit, fp.RequiresAuditLog,
                       fp.CreatedAtUtc, fp.CreatedBy, fp.UpdatedAtUtc, fp.UpdatedBy, fp.IsDeleted, fp.DeletedAtUtc, fp.DeletedBy
                FROM FieldPermissions fp
                JOIN {map} ON m.EntityType = fp.EntityType AND m.FieldName = fp.FieldName
                WHERE fp.ScreenCode IS NULL AND m.Rn > 1;
                """);

            migrationBuilder.Sql($"""
                UPDATE fp SET ScreenCode = m.ScreenCode
                FROM FieldPermissions fp
                JOIN {map} ON m.EntityType = fp.EntityType AND m.FieldName = fp.FieldName
                WHERE fp.ScreenCode IS NULL AND m.Rn = 1;
                """);

            migrationBuilder.Sql("""
                UPDATE FieldPermissions
                SET ScreenCode = 'UNMAPPED',
                    DeletedAtUtc = CASE WHEN IsDeleted = 1 THEN DeletedAtUtc ELSE SYSUTCDATETIME() END,
                    DeletedBy = CASE WHEN IsDeleted = 1 THEN DeletedBy ELSE 0 END,
                    IsDeleted = 1
                WHERE ScreenCode IS NULL;
                """);

            // 3) Every row has its screen now: NOT NULL.
            migrationBuilder.AlterColumn<string>(
                name: "ScreenCode",
                table: "FieldPermissions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ButtonPermissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ScreenCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ButtonCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    RequiresAuditLog = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_ButtonPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ButtonPermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ButtonPermissions_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ButtonPermissions_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ButtonPermissions_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldPermissions_RoleId_ScreenCode_EntityType_FieldName",
                table: "FieldPermissions",
                columns: new[] { "RoleId", "ScreenCode", "EntityType", "FieldName" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ButtonPermissions_CompanyId",
                table: "ButtonPermissions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ButtonPermissions_RoleId_ScreenCode_ButtonCode",
                table: "ButtonPermissions",
                columns: new[] { "RoleId", "ScreenCode", "ButtonCode" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ButtonPermissions");

            migrationBuilder.DropIndex(
                name: "IX_FieldPermissions_RoleId_ScreenCode_EntityType_FieldName",
                table: "FieldPermissions");

            // One row per field again: keep the lowest Id of each live (role, entity, field).
            migrationBuilder.Sql("""
                DELETE fp FROM FieldPermissions fp
                WHERE fp.IsDeleted = 0 AND EXISTS (
                    SELECT 1 FROM FieldPermissions o
                    WHERE o.IsDeleted = 0 AND o.RoleId = fp.RoleId AND o.EntityType = fp.EntityType
                      AND o.FieldName = fp.FieldName AND o.Id < fp.Id);
                """);

            migrationBuilder.DropColumn(
                name: "ScreenCode",
                table: "FieldPermissions");

            migrationBuilder.CreateIndex(
                name: "IX_FieldPermissions_RoleId_EntityType_FieldName",
                table: "FieldPermissions",
                columns: new[] { "RoleId", "EntityType", "FieldName" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }
    }
}
