using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.HR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Payroll;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.5. Mirrors
/// FixedAssetsCommon.CostCenterAsync (FixedAssets/FixedAssetsCommon.cs:43) exactly — same rule 27/32
/// shape ("its own value, else its branch's") — for Employee instead of FixedAsset.
/// </summary>
internal static class PayrollPostingRules
{
    public static async Task<IReadOnlyDictionary<long, long>> CostCenterAsync(IApplicationDbContext db, Employee employee, CancellationToken ct)
    {
        if (employee.CostCenterDimensionValueId is { } valueId)
        {
            var dimensionId = await db.CostCenterDimensionValues.Where(v => v.Id == valueId).Select(v => (long?)v.CostCenterDimensionId).FirstOrDefaultAsync(ct)
                              ?? throw new NotFoundException(nameof(CostCenterDimensionValue), valueId);
            return new Dictionary<long, long> { [dimensionId] = valueId };
        }

        if (employee.BranchId is { } branchId)
        {
            var dimension = await db.CostCenterDimensions.FirstOrDefaultAsync(
                d => d.LinkedEntityType == CostCenterLinkedEntityType.Branch && d.IsActive, ct);
            if (dimension is not null && await PostingEntityValueMapper.MapAsync(db, dimension, branchId, ct) is { } branchValueId)
            {
                return new Dictionary<long, long> { [dimension.Id] = branchValueId };
            }
        }

        throw new BusinessRuleException(
            "PAY-COSTCENTER-REQUIRED",
            $"الموظف {employee.Code} مالوش مركز تكلفة، ولا فرعه مربوط بقيمة في بُعد الفروع — كل سطور استحقاق الرواتب لازم تحمل مركز تكلفة.");
    }

    /// <summary>Resolves a set of CompanyAccountRole values to their mapped AccountId in one query —
    /// same "mapped by the finance manager, unmapped is only an error when a template needs it"
    /// philosophy as CompanyAccountMapping's own doc comment; the caller decides what "needs it" means.</summary>
    public static async Task<IReadOnlyDictionary<CompanyAccountRole, long>> ResolveRoleAccountsAsync(
        IApplicationDbContext db, long? companyId, IReadOnlyCollection<CompanyAccountRole> roles, CancellationToken ct) =>
        await db.CompanyAccountMappings
            .Where(m => m.CompanyId == companyId && roles.Contains(m.Role))
            .ToDictionaryAsync(m => m.Role, m => m.AccountId, ct);
}
