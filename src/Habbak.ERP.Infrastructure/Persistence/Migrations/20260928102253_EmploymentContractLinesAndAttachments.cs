using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EmploymentContractLinesAndAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AttachmentId",
                table: "EmploymentContracts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AttachmentId",
                table: "EmployeeCertifications",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmploymentContractLines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmploymentContractId = table.Column<long>(type: "bigint", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    IsTaxable = table.Column<bool>(type: "bit", nullable: false),
                    IsInsurable = table.Column<bool>(type: "bit", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_EmploymentContractLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmploymentContractLines_EmploymentContracts_EmploymentContractId",
                        column: x => x.EmploymentContractId,
                        principalTable: "EmploymentContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmploymentContractLines_Users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmploymentContractLines_Users_DeletedBy",
                        column: x => x.DeletedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmploymentContractLines_Users_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentContracts_AttachmentId",
                table: "EmploymentContracts",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCertifications_AttachmentId",
                table: "EmployeeCertifications",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmploymentContractLines_EmploymentContractId",
                table: "EmploymentContractLines",
                column: "EmploymentContractId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeCertifications_Attachments_AttachmentId",
                table: "EmployeeCertifications",
                column: "AttachmentId",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmploymentContracts_Attachments_AttachmentId",
                table: "EmploymentContracts",
                column: "AttachmentId",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeCertifications_Attachments_AttachmentId",
                table: "EmployeeCertifications");

            migrationBuilder.DropForeignKey(
                name: "FK_EmploymentContracts_Attachments_AttachmentId",
                table: "EmploymentContracts");

            migrationBuilder.DropTable(
                name: "EmploymentContractLines");

            migrationBuilder.DropIndex(
                name: "IX_EmploymentContracts_AttachmentId",
                table: "EmploymentContracts");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeCertifications_AttachmentId",
                table: "EmployeeCertifications");

            migrationBuilder.DropColumn(
                name: "AttachmentId",
                table: "EmploymentContracts");

            migrationBuilder.DropColumn(
                name: "AttachmentId",
                table: "EmployeeCertifications");
        }
    }
}
