using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Organization;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Branches;

/// <summary>
/// Keeps every Branch-linked cost center type's values (CostCenterDimensionValue) mirroring the
/// Branches screen's own records, so the rest of the system (journal entry lines, posting
/// validation, the account-dimension-link FK) keeps working against a real
/// CostCenterDimensionValue row exactly as it does for a manually-entered cost center type —
/// nothing downstream needs to know a value originated from a Branch instead.
/// </summary>
internal static class BranchDimensionSync
{
    public static async Task UpsertAsync(IApplicationDbContext db, Branch branch, CancellationToken cancellationToken)
    {
        var linkedDimensionIds = await db.CostCenterDimensions
            .Where(d => d.LinkedEntityType == CostCenterLinkedEntityType.Branch)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        foreach (var dimensionId in linkedDimensionIds)
        {
            var mirrored = await db.CostCenterDimensionValues
                .FirstOrDefaultAsync(v => v.CostCenterDimensionId == dimensionId && v.Code == branch.Code, cancellationToken);

            if (mirrored is null)
            {
                db.CostCenterDimensionValues.Add(new CostCenterDimensionValue
                {
                    CostCenterDimensionId = dimensionId,
                    Code = branch.Code,
                    NameAr = branch.NameAr,
                    NameEn = branch.NameEn,
                    ParentId = null,
                    Level = 0,
                    IsActive = branch.IsActive
                });
            }
            else
            {
                mirrored.NameAr = branch.NameAr;
                mirrored.NameEn = branch.NameEn;
                mirrored.IsActive = branch.IsActive;
            }
        }
    }
}
