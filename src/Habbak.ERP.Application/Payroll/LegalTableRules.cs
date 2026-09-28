using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Application.Payroll;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.6 — the overlap validation
/// Phase-4-Research.md §2.1's LegalTablesConfigurations.cs comment deferred to "the Create/Update
/// command's job" (no DB unique index can express interval overlap, same reasoning as
/// ShiftScheduleConfiguration). One shared throwing wrapper over
/// Domain/Common/IEffectiveDatedEntity.EffectiveDateRules.Overlaps — Domain itself can't throw an
/// Application-layer exception, so this is the smallest place both sides meet.
/// </summary>
internal static class LegalTableRules
{
    /// <summary><paramref name="candidatesSameKey"/> must already be narrowed to one logical key by
    /// the caller (e.g. same OvertimeType, same Sector, same LeaveTypeId+MinServiceYears+MinAge tier)
    /// — this only ever reasons about time.</summary>
    public static void EnsureNoOverlap<T>(IEnumerable<T> candidatesSameKey, DateOnly effectiveFrom, DateOnly? effectiveTo, long excludeId = 0)
        where T : AuditableEntity, IEffectiveDatedEntity
    {
        var conflict = candidatesSameKey.FirstOrDefault(
            c => c.Id != excludeId && EffectiveDateRules.Overlaps(c.EffectiveFrom, c.EffectiveTo, effectiveFrom, effectiveTo));
        if (conflict is not null)
        {
            throw new BusinessRuleException(
                "PAY-LEGAL-TABLE-OVERLAP",
                $"الفترة دي بتتداخل مع صف موجود بالفعل (من {conflict.EffectiveFrom:yyyy-MM-dd} لحد " +
                $"{(conflict.EffectiveTo is { } to ? to.ToString("yyyy-MM-dd") : "الآن")}).");
        }
    }
}
