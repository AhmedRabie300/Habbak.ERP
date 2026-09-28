using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PostingTemplateTriggersAndFormulas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostingTemplates_CompanyId_ScreenCode_TemplateSelectorField_TemplateSelectorValue",
                table: "PostingTemplates");

            migrationBuilder.RenameColumn(
                name: "TemplateSelectorValue",
                table: "PostingTemplates",
                newName: "TriggerFieldValue");

            migrationBuilder.RenameColumn(
                name: "TemplateSelectorField",
                table: "PostingTemplates",
                newName: "TriggerFieldName");

            migrationBuilder.AddColumn<int>(
                name: "ExecutionOrder",
                table: "PostingTemplates",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                table: "PostingTemplates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "TriggerType",
                table: "PostingTemplates",
                type: "int",
                nullable: false,
                defaultValue: 1); // Always — what every template did before triggers existed

            migrationBuilder.AddColumn<string>(
                name: "AmountFieldNames",
                table: "PostingTemplateLines",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountMultiplier",
                table: "PostingTemplateLines",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPercentage",
                table: "PostingTemplateLines",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextKey",
                table: "PostingTemplateLineCostCenters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedEntityField",
                table: "PostingTemplateLineCostCenters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedEntityType",
                table: "PostingTemplateLineCostCenters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PostingGroupId",
                table: "JournalEntryTemplateSnapshots",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RequestIdempotencyKey",
                table: "JournalEntryTemplateSnapshots",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PostingGroupId",
                table: "JournalEntries",
                type: "uniqueidentifier",
                nullable: true);

            // A template written for one selector value becomes a FieldCondition trigger on that value.
            migrationBuilder.Sql("UPDATE [PostingTemplates] SET [TriggerType] = 3 WHERE [TriggerFieldName] IS NOT NULL;");

            // Every version chain gets one family id, shared down PreviousVersionId, before the index
            // that makes "one current version per family" unique is built.
            migrationBuilder.Sql(@"
UPDATE [PostingTemplates] SET [FamilyId] = NEWID() WHERE [PreviousVersionId] IS NULL;
WITH [chain] AS (
    SELECT [Id], [FamilyId] FROM [PostingTemplates] WHERE [PreviousVersionId] IS NULL
    UNION ALL
    SELECT t.[Id], c.[FamilyId] FROM [PostingTemplates] t JOIN [chain] c ON t.[PreviousVersionId] = c.[Id]
)
UPDATE t SET t.[FamilyId] = c.[FamilyId] FROM [PostingTemplates] t JOIN [chain] c ON t.[Id] = c.[Id];");

            // Entries posted before groups existed were one per document: each is its own group, and
            // the request key is the key it was posted with.
            migrationBuilder.Sql(@"
UPDATE [JournalEntryTemplateSnapshots] SET [RequestIdempotencyKey] = [IdempotencyKey], [PostingGroupId] = NEWID();
UPDATE je SET je.[PostingGroupId] = s.[PostingGroupId]
FROM [JournalEntries] je JOIN [JournalEntryTemplateSnapshots] s ON s.[JournalEntryId] = je.[Id];");

            migrationBuilder.CreateIndex(
                name: "IX_PostingTemplates_CompanyId_FamilyId",
                table: "PostingTemplates",
                columns: new[] { "CompanyId", "FamilyId" },
                unique: true,
                filter: "[IsCurrentVersion] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PostingTemplates_CompanyId_ScreenCode",
                table: "PostingTemplates",
                columns: new[] { "CompanyId", "ScreenCode" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryTemplateSnapshots_PostingGroupId",
                table: "JournalEntryTemplateSnapshots",
                column: "PostingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntryTemplateSnapshots_RequestIdempotencyKey",
                table: "JournalEntryTemplateSnapshots",
                column: "RequestIdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntries_PostingGroupId",
                table: "JournalEntries",
                column: "PostingGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PostingTemplates_CompanyId_FamilyId",
                table: "PostingTemplates");

            migrationBuilder.DropIndex(
                name: "IX_PostingTemplates_CompanyId_ScreenCode",
                table: "PostingTemplates");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntryTemplateSnapshots_PostingGroupId",
                table: "JournalEntryTemplateSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntryTemplateSnapshots_RequestIdempotencyKey",
                table: "JournalEntryTemplateSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_JournalEntries_PostingGroupId",
                table: "JournalEntries");

            migrationBuilder.DropColumn(
                name: "ExecutionOrder",
                table: "PostingTemplates");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "PostingTemplates");

            migrationBuilder.DropColumn(
                name: "TriggerType",
                table: "PostingTemplates");

            migrationBuilder.DropColumn(
                name: "AmountFieldNames",
                table: "PostingTemplateLines");

            migrationBuilder.DropColumn(
                name: "AmountMultiplier",
                table: "PostingTemplateLines");

            migrationBuilder.DropColumn(
                name: "AmountPercentage",
                table: "PostingTemplateLines");

            migrationBuilder.DropColumn(
                name: "ContextKey",
                table: "PostingTemplateLineCostCenters");

            migrationBuilder.DropColumn(
                name: "RelatedEntityField",
                table: "PostingTemplateLineCostCenters");

            migrationBuilder.DropColumn(
                name: "RelatedEntityType",
                table: "PostingTemplateLineCostCenters");

            migrationBuilder.DropColumn(
                name: "PostingGroupId",
                table: "JournalEntryTemplateSnapshots");

            migrationBuilder.DropColumn(
                name: "RequestIdempotencyKey",
                table: "JournalEntryTemplateSnapshots");

            migrationBuilder.DropColumn(
                name: "PostingGroupId",
                table: "JournalEntries");

            migrationBuilder.RenameColumn(
                name: "TriggerFieldValue",
                table: "PostingTemplates",
                newName: "TemplateSelectorValue");

            migrationBuilder.RenameColumn(
                name: "TriggerFieldName",
                table: "PostingTemplates",
                newName: "TemplateSelectorField");

            migrationBuilder.CreateIndex(
                name: "IX_PostingTemplates_CompanyId_ScreenCode_TemplateSelectorField_TemplateSelectorValue",
                table: "PostingTemplates",
                columns: new[] { "CompanyId", "ScreenCode", "TemplateSelectorField", "TemplateSelectorValue" },
                unique: true,
                filter: "[IsCurrentVersion] = 1 AND [IsDeleted] = 0");
        }
    }
}
