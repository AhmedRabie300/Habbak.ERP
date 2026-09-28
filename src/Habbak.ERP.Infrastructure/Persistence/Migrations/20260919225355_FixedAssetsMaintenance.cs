using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixedAssetsMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetPhysicalCounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    CountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CountDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AssetPhysicalCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetPhysicalCounts_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetPhysicalCounts_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetPhysicalCounts_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetPhysicalCounts_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    AutoDepreciationEnabled = table.Column<bool>(type: "bit", nullable: false),
                    DepreciationRunDay = table.Column<int>(type: "int", nullable: false),
                    RequireApprovalForDisposal = table.Column<bool>(type: "bit", nullable: false),
                    RequireApprovalForTransfer = table.Column<bool>(type: "bit", nullable: false),
                    MaintenanceApprovalThreshold = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    PhysicalCountFrequency = table.Column<int>(type: "int", nullable: true),
                    DefaultFirstMonthProrated = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AssetSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetSettings_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetSettings_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetSettings_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepreciationRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    RunNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RunDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalDepreciation = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AssetCount = table.Column<int>(type: "int", nullable: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PostedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReversalJournalEntryId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_DepreciationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepreciationRuns_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepreciationRuns_JournalEntries_ReversalJournalEntryId",
                        column: x => x.ReversalJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepreciationRuns_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepreciationRuns_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepreciationRuns_Users_PostedByUserId",
                        column: x => x.PostedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepreciationRuns_Users_ReversedByUserId",
                        column: x => x.ReversedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepreciationRuns_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssetCategories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DepreciationMethod = table.Column<int>(type: "int", nullable: false),
                    DefaultDepreciationRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    DefaultUsefulLifeYears = table.Column<int>(type: "int", nullable: true),
                    DefaultSalvagePercentage = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    AssetAccountId = table.Column<long>(type: "bigint", nullable: false),
                    AccumulatedDepreciationAccountId = table.Column<long>(type: "bigint", nullable: false),
                    DepreciationExpenseAccountId = table.Column<long>(type: "bigint", nullable: false),
                    DisposalGainAccountId = table.Column<long>(type: "bigint", nullable: true),
                    DisposalLossAccountId = table.Column<long>(type: "bigint", nullable: true),
                    MaintenanceExpenseAccountId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_FixedAssetCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_AccumulatedDepreciationAccountId",
                        column: x => x.AccumulatedDepreciationAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_AssetAccountId",
                        column: x => x.AssetAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_DepreciationExpenseAccountId",
                        column: x => x.DepreciationExpenseAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_DisposalGainAccountId",
                        column: x => x.DisposalGainAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_DisposalLossAccountId",
                        column: x => x.DisposalLossAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Accounts_MaintenanceExpenseAccountId",
                        column: x => x.MaintenanceExpenseAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssetCategories_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceCategories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MaintenanceType = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_MaintenanceCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceCategories_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceCategories_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceCategories_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixedAssets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    AssetNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AcquisitionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AcquisitionCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    BaseCurrencyAmount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SupplierId = table.Column<long>(type: "bigint", nullable: true),
                    PurchaseInvoiceId = table.Column<long>(type: "bigint", nullable: true),
                    FundingAccountId = table.Column<long>(type: "bigint", nullable: true),
                    AcquisitionJournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    UsefulLifeYears = table.Column<int>(type: "int", nullable: true),
                    SalvageValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DepreciationMethod = table.Column<int>(type: "int", nullable: false),
                    DepreciationRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    DepreciationStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FirstMonthProrated = table.Column<bool>(type: "bit", nullable: false),
                    AccumulatedDepreciation = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DisposalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DisposalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisposalProceeds = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    DisposalJournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    CustodyOfficerId = table.Column<long>(type: "bigint", nullable: true),
                    CustodyOfficerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CostCenterValueId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_FixedAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixedAssets_Accounts_FundingAccountId",
                        column: x => x.FundingAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_CostCenterDimensionValues_CostCenterValueId",
                        column: x => x.CostCenterValueId,
                        principalTable: "CostCenterDimensionValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_CustodyOfficers_CustodyOfficerId",
                        column: x => x.CustodyOfficerId,
                        principalTable: "CustodyOfficers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_FixedAssetCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "FixedAssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_JournalEntries_AcquisitionJournalEntryId",
                        column: x => x.AcquisitionJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_JournalEntries_DisposalJournalEntryId",
                        column: x => x.DisposalJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FixedAssets_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetDisposals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    DisposalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    DisposalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DisposalType = table.Column<int>(type: "int", nullable: false),
                    Proceeds = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ProceedsAccountId = table.Column<long>(type: "bigint", nullable: true),
                    BuyerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CostAtDisposal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AccumulatedAtDisposal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BookValueAtDisposal = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    GainOrLoss = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AssetDisposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetDisposals_Accounts_ProceedsAccountId",
                        column: x => x.ProceedsAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDisposals_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDisposals_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDisposals_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDisposals_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetDisposals_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetPhysicalCountLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssetPhysicalCountId = table.Column<long>(type: "bigint", nullable: false),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    ExpectedLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActualLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsFound = table.Column<bool>(type: "bit", nullable: true),
                    Condition = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AssetPhysicalCountLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetPhysicalCountLines_AssetPhysicalCounts_AssetPhysicalCountId",
                        column: x => x.AssetPhysicalCountId,
                        principalTable: "AssetPhysicalCounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetPhysicalCountLines_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetPhysicalCountLines_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetPhysicalCountLines_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetPhysicalCountLines_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetTransfers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    TransferNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    FromBranchId = table.Column<long>(type: "bigint", nullable: true),
                    ToBranchId = table.Column<long>(type: "bigint", nullable: false),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CustodyOfficerId = table.Column<long>(type: "bigint", nullable: false),
                    CustodyOfficerNameSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AssetTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_Branches_FromBranchId",
                        column: x => x.FromBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_Branches_ToBranchId",
                        column: x => x.ToBranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_CustodyOfficers_CustodyOfficerId",
                        column: x => x.CustodyOfficerId,
                        principalTable: "CustodyOfficers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetTransfers_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DepreciationSchedules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    PeriodNumber = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    AccumulatedAfter = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BookValueAfter = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DepreciationRunId = table.Column<long>(type: "bigint", nullable: true),
                    PostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_DepreciationSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepreciationSchedules_DepreciationRuns_DepreciationRunId",
                        column: x => x.DepreciationRunId,
                        principalTable: "DepreciationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepreciationSchedules_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DepreciationSchedules_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepreciationSchedules_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepreciationSchedules_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DepreciationSchedules_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceIssues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    BranchId = table.Column<long>(type: "bigint", nullable: true),
                    IssueNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: true),
                    DeviceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_MaintenanceIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceIssues_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceIssues_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceIssues_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceIssues_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceIssues_Users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceIssues_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceSchedules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    MaintenanceCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    LastExecutedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TechnicianId = table.Column<long>(type: "bigint", nullable: true),
                    TechnicianName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_MaintenanceSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_MaintenanceCategories_MaintenanceCategoryId",
                        column: x => x.MaintenanceCategoryId,
                        principalTable: "MaintenanceCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<long>(type: "bigint", nullable: true),
                    RequestNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IssueId = table.Column<long>(type: "bigint", nullable: true),
                    FixedAssetId = table.Column<long>(type: "bigint", nullable: false),
                    MaintenanceCategoryId = table.Column<long>(type: "bigint", nullable: false),
                    MaintenanceScheduleId = table.Column<long>(type: "bigint", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ScheduledDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TechnicianId = table.Column<long>(type: "bigint", nullable: true),
                    TechnicianName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupplierId = table.Column<long>(type: "bigint", nullable: true),
                    ExternalCreditAccountId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    LaborCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    SparePartsTotalCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ActualCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    JournalEntryId = table.Column<long>(type: "bigint", nullable: true),
                    SparePartsJournalEntryId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_MaintenanceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_Accounts_ExternalCreditAccountId",
                        column: x => x.ExternalCreditAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_FixedAssets_FixedAssetId",
                        column: x => x.FixedAssetId,
                        principalTable: "FixedAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_JournalEntries_JournalEntryId",
                        column: x => x.JournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_JournalEntries_SparePartsJournalEntryId",
                        column: x => x.SparePartsJournalEntryId,
                        principalTable: "JournalEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_MaintenanceCategories_MaintenanceCategoryId",
                        column: x => x.MaintenanceCategoryId,
                        principalTable: "MaintenanceCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_MaintenanceIssues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "MaintenanceIssues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_MaintenanceSchedules_MaintenanceScheduleId",
                        column: x => x.MaintenanceScheduleId,
                        principalTable: "MaintenanceSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceRequests_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceSpareParts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaintenanceRequestId = table.Column<long>(type: "bigint", nullable: false),
                    ItemId = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: true),
                    StockTransactionId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_MaintenanceSpareParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSpareParts_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceSpareParts_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceSpareParts_StockTransactions_StockTransactionId",
                        column: x => x.StockTransactionId,
                        principalTable: "StockTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceSpareParts_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceSpareParts_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceSpareParts_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceSpareParts_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_CompanyId",
                table: "AssetDisposals",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_CompanyId_DisposalNumber",
                table: "AssetDisposals",
                columns: new[] { "CompanyId", "DisposalNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_FixedAssetId",
                table: "AssetDisposals",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_JournalEntryId",
                table: "AssetDisposals",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDisposals_ProceedsAccountId",
                table: "AssetDisposals",
                column: "ProceedsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetPhysicalCountLines_AssetPhysicalCountId_FixedAssetId",
                table: "AssetPhysicalCountLines",
                columns: new[] { "AssetPhysicalCountId", "FixedAssetId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AssetPhysicalCountLines_FixedAssetId",
                table: "AssetPhysicalCountLines",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetPhysicalCounts_BranchId",
                table: "AssetPhysicalCounts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetPhysicalCounts_CompanyId",
                table: "AssetPhysicalCounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetPhysicalCounts_CompanyId_CountNumber",
                table: "AssetPhysicalCounts",
                columns: new[] { "CompanyId", "CountNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AssetSettings_CompanyId",
                table: "AssetSettings",
                column: "CompanyId",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_CompanyId",
                table: "AssetTransfers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_CompanyId_TransferNumber",
                table: "AssetTransfers",
                columns: new[] { "CompanyId", "TransferNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_CustodyOfficerId",
                table: "AssetTransfers",
                column: "CustodyOfficerId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_FixedAssetId",
                table: "AssetTransfers",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_FromBranchId",
                table: "AssetTransfers",
                column: "FromBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransfers_ToBranchId",
                table: "AssetTransfers",
                column: "ToBranchId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRuns_CompanyId",
                table: "DepreciationRuns",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRuns_CompanyId_RunNumber",
                table: "DepreciationRuns",
                columns: new[] { "CompanyId", "RunNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRuns_IdempotencyKey",
                table: "DepreciationRuns",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IsDeleted] = 0 AND [Status] <> 3");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRuns_JournalEntryId",
                table: "DepreciationRuns",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRuns_PostedByUserId",
                table: "DepreciationRuns",
                column: "PostedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRuns_ReversalJournalEntryId",
                table: "DepreciationRuns",
                column: "ReversalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationRuns_ReversedByUserId",
                table: "DepreciationRuns",
                column: "ReversedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationSchedules_DepreciationRunId",
                table: "DepreciationSchedules",
                column: "DepreciationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationSchedules_FixedAssetId_PeriodNumber",
                table: "DepreciationSchedules",
                columns: new[] { "FixedAssetId", "PeriodNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationSchedules_JournalEntryId",
                table: "DepreciationSchedules",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DepreciationSchedules_Status_PeriodEnd",
                table: "DepreciationSchedules",
                columns: new[] { "Status", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_AccumulatedDepreciationAccountId",
                table: "FixedAssetCategories",
                column: "AccumulatedDepreciationAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_AssetAccountId",
                table: "FixedAssetCategories",
                column: "AssetAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_CompanyId",
                table: "FixedAssetCategories",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_CompanyId_Code",
                table: "FixedAssetCategories",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_DepreciationExpenseAccountId",
                table: "FixedAssetCategories",
                column: "DepreciationExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_DisposalGainAccountId",
                table: "FixedAssetCategories",
                column: "DisposalGainAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_DisposalLossAccountId",
                table: "FixedAssetCategories",
                column: "DisposalLossAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssetCategories_MaintenanceExpenseAccountId",
                table: "FixedAssetCategories",
                column: "MaintenanceExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_AcquisitionJournalEntryId",
                table: "FixedAssets",
                column: "AcquisitionJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_BranchId",
                table: "FixedAssets",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CategoryId",
                table: "FixedAssets",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CompanyId",
                table: "FixedAssets",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CompanyId_AssetNumber",
                table: "FixedAssets",
                columns: new[] { "CompanyId", "AssetNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CompanyId_Status",
                table: "FixedAssets",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CostCenterValueId",
                table: "FixedAssets",
                column: "CostCenterValueId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CustodyOfficerId",
                table: "FixedAssets",
                column: "CustodyOfficerId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_DisposalJournalEntryId",
                table: "FixedAssets",
                column: "DisposalJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_FundingAccountId",
                table: "FixedAssets",
                column: "FundingAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_PurchaseInvoiceId",
                table: "FixedAssets",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_SupplierId",
                table: "FixedAssets",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCategories_CompanyId",
                table: "MaintenanceCategories",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCategories_CompanyId_Code",
                table: "MaintenanceCategories",
                columns: new[] { "CompanyId", "Code" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIssues_BranchId",
                table: "MaintenanceIssues",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIssues_CompanyId",
                table: "MaintenanceIssues",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIssues_CompanyId_IssueNumber",
                table: "MaintenanceIssues",
                columns: new[] { "CompanyId", "IssueNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIssues_CompanyId_Status",
                table: "MaintenanceIssues",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIssues_FixedAssetId",
                table: "MaintenanceIssues",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceIssues_ReportedByUserId",
                table: "MaintenanceIssues",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_CompanyId",
                table: "MaintenanceRequests",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_CompanyId_RequestNumber",
                table: "MaintenanceRequests",
                columns: new[] { "CompanyId", "RequestNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_ExternalCreditAccountId",
                table: "MaintenanceRequests",
                column: "ExternalCreditAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_FixedAssetId",
                table: "MaintenanceRequests",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_IdempotencyKey",
                table: "MaintenanceRequests",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_IssueId",
                table: "MaintenanceRequests",
                column: "IssueId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_JournalEntryId",
                table: "MaintenanceRequests",
                column: "JournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_MaintenanceCategoryId",
                table: "MaintenanceRequests",
                column: "MaintenanceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_MaintenanceScheduleId",
                table: "MaintenanceRequests",
                column: "MaintenanceScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_SparePartsJournalEntryId",
                table: "MaintenanceRequests",
                column: "SparePartsJournalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceRequests_SupplierId",
                table: "MaintenanceRequests",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_CompanyId",
                table: "MaintenanceSchedules",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_FixedAssetId",
                table: "MaintenanceSchedules",
                column: "FixedAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_IsActive_NextDueDate",
                table: "MaintenanceSchedules",
                columns: new[] { "IsActive", "NextDueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_MaintenanceCategoryId",
                table: "MaintenanceSchedules",
                column: "MaintenanceCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSpareParts_ItemId",
                table: "MaintenanceSpareParts",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSpareParts_MaintenanceRequestId",
                table: "MaintenanceSpareParts",
                column: "MaintenanceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSpareParts_StockTransactionId",
                table: "MaintenanceSpareParts",
                column: "StockTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSpareParts_WarehouseId",
                table: "MaintenanceSpareParts",
                column: "WarehouseId");

            // The sidebar of an existing database: a new group before Settings, with the module's 13 screens.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM MenuItems) AND NOT EXISTS (SELECT 1 FROM MenuItems WHERE Code = N'ASSETS_MAINTENANCE')
BEGIN
    DECLARE @now datetime2 = SYSUTCDATETIME();
    UPDATE MenuItems SET DisplayOrder = 7 WHERE Code = N'SETTINGS';
    INSERT INTO MenuItems (Code, NameAr, NameEn, ParentId, DisplayOrder, RouteKey, IconKey, IsActive, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, IsDeleted)
    VALUES (N'ASSETS_MAINTENANCE', N'الأصول والصيانة', N'Assets & Maintenance', NULL, 6, NULL, NULL, 1, @now, 0, @now, 0, 0);

    DECLARE @assets bigint = (SELECT Id FROM MenuItems WHERE Code = N'ASSETS_MAINTENANCE');
    INSERT INTO MenuItems (Code, NameAr, NameEn, ParentId, DisplayOrder, RouteKey, IconKey, IsActive, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, IsDeleted)
    VALUES
        (N'FIXED_ASSETS_CATEGORIES',        N'فئات الأصول',             N'Asset Categories',              @assets,  1, N'/fixed-assets/categories',            NULL, 1, @now, 0, @now, 0, 0),
        (N'FIXED_ASSETS',                   N'الأصول الثابتة',          N'Fixed Assets',                  @assets,  2, N'/fixed-assets/assets',                NULL, 1, @now, 0, @now, 0, 0),
        (N'FIXED_ASSETS_SCHEDULE',          N'جدول الإهلاك',            N'Depreciation Schedule',         @assets,  3, N'/fixed-assets/depreciation-schedule', NULL, 1, @now, 0, @now, 0, 0),
        (N'FIXED_ASSETS_DEPRECIATION_RUNS', N'تشغيل الإهلاك الشهري',    N'Depreciation Runs',             @assets,  4, N'/fixed-assets/depreciation-runs',     NULL, 1, @now, 0, @now, 0, 0),
        (N'FIXED_ASSETS_TRANSFERS',         N'نقل الأصول',              N'Asset Transfers',               @assets,  5, N'/fixed-assets/transfers',             NULL, 1, @now, 0, @now, 0, 0),
        (N'FIXED_ASSETS_DISPOSALS',         N'استبعاد الأصول',          N'Asset Disposals',               @assets,  6, N'/fixed-assets/disposals',             NULL, 1, @now, 0, @now, 0, 0),
        (N'FIXED_ASSETS_PHYSICAL_COUNTS',   N'جرد الأصول',              N'Asset Counts',                  @assets,  7, N'/fixed-assets/physical-counts',       NULL, 1, @now, 0, @now, 0, 0),
        (N'MAINTENANCE_CATEGORIES',         N'فئات الصيانة',            N'Maintenance Categories',        @assets,  8, N'/maintenance/categories',             NULL, 1, @now, 0, @now, 0, 0),
        (N'MAINTENANCE_ISSUES',             N'بلاغات الأعطال',          N'Fault Reports',                 @assets,  9, N'/maintenance/issues',                 NULL, 1, @now, 0, @now, 0, 0),
        (N'MAINTENANCE_REQUESTS',           N'طلبات الصيانة',           N'Maintenance Requests',          @assets, 10, N'/maintenance/requests',               NULL, 1, @now, 0, @now, 0, 0),
        (N'MAINTENANCE_SCHEDULES',          N'الصيانة الدورية',         N'Preventive Schedules',          @assets, 11, N'/maintenance/schedules',              NULL, 1, @now, 0, @now, 0, 0),
        (N'MAINTENANCE_BOARD',              N'لوحة الصيانة',            N'Maintenance Board',             @assets, 12, N'/maintenance/board',                  NULL, 1, @now, 0, @now, 0, 0),
        (N'FIXED_ASSETS_SETTINGS',          N'إعدادات الأصول والصيانة', N'Assets & Maintenance Settings', @assets, 13, N'/fixed-assets/settings',              NULL, 1, @now, 0, @now, 0, 0);
END");

            // What a new company gets from FixedAssetDefaults, for the companies that already exist.
            // Asset categories are not seeded: each needs its accounts, which the finance manager maps.
            migrationBuilder.Sql(@"
DECLARE @seedNow datetime2 = SYSUTCDATETIME();

INSERT INTO AssetSettings (CompanyId, AutoDepreciationEnabled, DepreciationRunDay, RequireApprovalForDisposal, RequireApprovalForTransfer,
    MaintenanceApprovalThreshold, PhysicalCountFrequency, DefaultFirstMonthProrated, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, IsDeleted)
SELECT c.Id, 0, 28, 1, 0, NULL, NULL, 1, @seedNow, 0, @seedNow, 0, 0
FROM Companies c
WHERE c.IsDeleted = 0 AND NOT EXISTS (SELECT 1 FROM AssetSettings s WHERE s.CompanyId = c.Id);

INSERT INTO MaintenanceCategories (CompanyId, Code, NameAr, NameEn, MaintenanceType, IsActive, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, IsDeleted)
SELECT c.Id, d.Code, d.NameAr, d.NameEn, d.MaintenanceType, 1, @seedNow, 0, @seedNow, 0, 0
FROM Companies c
CROSS JOIN (VALUES
    (N'PREV', N'صيانة وقائية', N'Preventive maintenance', 1),
    (N'CORR', N'إصلاح أعطال', N'Corrective repair', 2),
    (N'INSP', N'فحص دوري', N'Inspection', 3)) AS d(Code, NameAr, NameEn, MaintenanceType)
WHERE c.IsDeleted = 0 AND NOT EXISTS (SELECT 1 FROM MaintenanceCategories m WHERE m.CompanyId = c.Id AND m.Code = d.Code);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM MenuItems WHERE ParentId = (SELECT Id FROM MenuItems WHERE Code = N'ASSETS_MAINTENANCE');
DELETE FROM MenuItems WHERE Code = N'ASSETS_MAINTENANCE';
UPDATE MenuItems SET DisplayOrder = 6 WHERE Code = N'SETTINGS';");

            migrationBuilder.DropTable(
                name: "AssetDisposals");

            migrationBuilder.DropTable(
                name: "AssetPhysicalCountLines");

            migrationBuilder.DropTable(
                name: "AssetSettings");

            migrationBuilder.DropTable(
                name: "AssetTransfers");

            migrationBuilder.DropTable(
                name: "DepreciationSchedules");

            migrationBuilder.DropTable(
                name: "MaintenanceSpareParts");

            migrationBuilder.DropTable(
                name: "AssetPhysicalCounts");

            migrationBuilder.DropTable(
                name: "DepreciationRuns");

            migrationBuilder.DropTable(
                name: "MaintenanceRequests");

            migrationBuilder.DropTable(
                name: "MaintenanceIssues");

            migrationBuilder.DropTable(
                name: "MaintenanceSchedules");

            migrationBuilder.DropTable(
                name: "FixedAssets");

            migrationBuilder.DropTable(
                name: "MaintenanceCategories");

            migrationBuilder.DropTable(
                name: "FixedAssetCategories");
        }
    }
}
