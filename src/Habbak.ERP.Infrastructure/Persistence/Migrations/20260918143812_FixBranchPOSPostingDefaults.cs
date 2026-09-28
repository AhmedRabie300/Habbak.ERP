using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Habbak.ERP.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// AddPostingEngineDocuments first went out with 0 as the default for both new BranchPOSSettings
    /// columns and was applied to both databases like that. 0 is not a POSPostingMode, and a
    /// threshold of 0 would charge every shortage to the cashier. The migration now carries the
    /// right defaults for fresh databases; this puts existing rows on them. No row could have been
    /// set to these values on purpose — nothing wrote the columns in between.
    /// </summary>
    public partial class FixBranchPOSPostingDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [BranchPOSSettings] SET [PostingMode] = 2 WHERE [PostingMode] = 0;");
            migrationBuilder.Sql("UPDATE [BranchPOSSettings] SET [ShiftVarianceEmployeeLiabilityThreshold] = 10 WHERE [ShiftVarianceEmployeeLiabilityThreshold] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
