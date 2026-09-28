using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPostingTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PostingTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    ScreenCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TemplateSelectorField = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TemplateSelectorValue = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    PreviousVersionId = table.Column<long>(type: "bigint", nullable: true),
                    IsCurrentVersion = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsSystemTemplate = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_PostingTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntryTemplateSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: false),
                    PostingTemplateId = table.Column<long>(type: "bigint", nullable: false),
                    TemplateVersionNumber = table.Column<int>(type: "int", nullable: false),
                    TemplateSnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_JournalEntryTemplateSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntryTemplateSnapshots_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JournalEntryTemplateSnapshots_PostingTemplates_PostingTemplateId",
                        column: x => x.PostingTemplateId,
                        principalTable: "PostingTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PostingTemplateLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostingTemplateId = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    AccountSourceType = table.Column<int>(type: "int", nullable: false),
                    FixedAccountId = table.Column<long>(type: "bigint", nullable: true),
                    AccountFieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccountResolverKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AmountFormulaType = table.Column<int>(type: "int", nullable: false),
                    AmountFieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConditionType = table.Column<int>(type: "int", nullable: false),
                    ConditionFieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConditionFieldValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LineDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_PostingTemplateLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostingTemplateLines_Accounts_FixedAccountId",
                        column: x => x.FixedAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PostingTemplateLines_PostingTemplates_PostingTemplateId",
                        column: x => x.PostingTemplateId,
                        principalTable: "PostingTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PostingTemplateLineCostCenters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostingTemplateLineId = table.Column<long>(type: "bigint", nullable: false),
                    CostCenterDimensionId = table.Column<long>(type: "bigint", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    FixedValueId = table.Column<long>(type: "bigint", nullable: true),
                    ValueFieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ValueResolverKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_PostingTemplateLineCostCenters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostingTemplateLineCostCenters_CostCenterDimensions_CostCenterDimensionId",
                        column: x => x.CostCenterDimensionId,
                        principalTable: "CostCenterDimensions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PostingTemplateLineCostCenters_PostingTemplateLines_PostingTemplateLineId",
                        column: x => x.PostingTemplateLineId,
                        principalTable: "PostingTemplateLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryTemplateSnapshots_IdempotencyKey",
                table: "JournalEntryTemplateSnapshots",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryTemplateSnapshots_JournalEntryId",
                table: "JournalEntryTemplateSnapshots",
                column: "JournalEntryId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryTemplateSnapshots_PostingTemplateId",
                table: "JournalEntryTemplateSnapshots",
                column: "PostingTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PostingTemplateLineCostCenters_CostCenterDimensionId",
                table: "PostingTemplateLineCostCenters",
                column: "CostCenterDimensionId");

            migrationBuilder.CreateIndex(
                name: "IX_PostingTemplateLineCostCenters_PostingTemplateLineId",
                table: "PostingTemplateLineCostCenters",
                column: "PostingTemplateLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PostingTemplateLines_FixedAccountId",
                table: "PostingTemplateLines",
                column: "FixedAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PostingTemplateLines_PostingTemplateId_LineNumber",
                table: "PostingTemplateLines",
                columns: new[] { "PostingTemplateId", "LineNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PostingTemplates_CompanyId",
                table: "PostingTemplates",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_PostingTemplates_CompanyId_ScreenCode_TemplateSelectorField_TemplateSelectorValue",
                table: "PostingTemplates",
                columns: new[] { "CompanyId", "ScreenCode", "TemplateSelectorField", "TemplateSelectorValue" },
                unique: true,
                filter: "[IsCurrentVersion] = 1 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntryTemplateSnapshots");

            migrationBuilder.DropTable(
                name: "PostingTemplateLineCostCenters");

            migrationBuilder.DropTable(
                name: "PostingTemplateLines");

            migrationBuilder.DropTable(
                name: "PostingTemplates");
        }
    }
}
