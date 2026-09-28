namespace Habbak.ERP.Domain.Common;

/// <summary>
/// Marks a row as one of several revisions of the same legal/policy value over time — a minimum
/// wage, a tax bracket set, an overtime multiplier — rather than a single mutable settings row.
/// <see cref="EffectiveTo"/> null means "still in force". First real consumer: the ten dated legal
/// tables of Docs/Implementation/HR-MASTER-PLAN.md §Phase 4 (Docs/Modules/10-Module-HR-Payroll.md
/// §2.4) plus <c>EndOfServicePolicy</c> (Phase-4-Research.md §2.2) — no entity implemented this
/// before Phase 4 (Phase-4-Research.md §1.4: the one close analogue, <c>EmployeeWeeklyRestDays</c>,
/// is never queried "as of a date", so this is the first real AsOf pattern in the codebase).
/// </summary>
public interface IEffectiveDatedEntity
{
    DateOnly EffectiveFrom { get; set; }
    DateOnly? EffectiveTo { get; set; }
}

/// <summary>
/// Pure, DB-independent resolution logic for <see cref="IEffectiveDatedEntity"/> rows. Deliberately
/// operates on already-loaded <see cref="IEnumerable{T}"/> rather than <see cref="IQueryable{T}"/>:
/// every Phase 4 legal table has at most a handful of rows per company (a law changes a few times a
/// year, not per request), so callers load the whole table once and resolve in memory instead of
/// risking an interface-typed LINQ expression that may or may not translate to SQL.
///
/// Callers are responsible for narrowing <paramref name="candidates"/> to one logical "key" first
/// (e.g. same <c>OvertimeType</c>, same <c>Sector</c>, same <c>LeaveTypeId</c>+<c>MinServiceYears</c>+
/// <c>MinAge</c> tier) — this class only ever reasons about time, never about which rows are
/// "the same thing at different times" for a given table, because that shape differs per table.
/// </summary>
public static class EffectiveDateRules
{
    /// <summary>Whether a single row's range covers <paramref name="asOf"/> (inclusive both ends).</summary>
    public static bool IsActiveOn(DateOnly effectiveFrom, DateOnly? effectiveTo, DateOnly asOf) =>
        effectiveFrom <= asOf && (effectiveTo is null || asOf <= effectiveTo);

    /// <summary>Whether two rows' ranges share any day (inclusive both ends) — the check a Create/Update
    /// command runs against the other rows already sharing the same key before saving a new range.</summary>
    public static bool Overlaps(DateOnly from1, DateOnly? to1, DateOnly from2, DateOnly? to2) =>
        from1 <= (to2 ?? DateOnly.MaxValue) && from2 <= (to1 ?? DateOnly.MaxValue);

    /// <summary>
    /// The row in force on <paramref name="asOf"/>, already narrowed to one key by the caller. More
    /// than one match means an overlap slipped past Create/Update validation — a data bug, not a
    /// legitimate case — so this resolves it deterministically (latest <see cref="IEffectiveDatedEntity.EffectiveFrom"/>
    /// wins, same "most recent amendment governs" rule a human would apply) rather than throwing, so a
    /// payroll run can still complete and the exceptions review (Docs/Modules/10-Module-HR-Payroll.md
    /// rule 35) surfaces the bad data instead of the run failing outright.
    /// </summary>
    public static T? ActiveAsOf<T>(IEnumerable<T> candidates, DateOnly asOf) where T : IEffectiveDatedEntity =>
        candidates
            .Where(c => IsActiveOn(c.EffectiveFrom, c.EffectiveTo, asOf))
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefault();
}
