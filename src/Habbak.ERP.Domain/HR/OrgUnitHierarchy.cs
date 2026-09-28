namespace Habbak.ERP.Domain.HR;

/// <summary>
/// Cycle prevention for <see cref="OrgUnit.ParentId"/> — a real Walk-to-root, not the shallow
/// "parent == self" check that is the only precedent in this codebase (ItemGroup). No entity in
/// this project had a deep cycle check before this (Docs/Implementation/Phase-1.1-Research.md §2.2),
/// so this is new logic, not copied from anywhere. Pure and DB-free on purpose: the caller (a future
/// Command handler, Batch 1.1.3) supplies the current Id→ParentId map, so this stays testable without
/// a database.
/// </summary>
public static class OrgUnitHierarchy
{
    /// <summary>True if giving <paramref name="candidateId"/> the parent <paramref name="proposedParentId"/> would create a cycle.</summary>
    public static bool WouldCreateCycle(long candidateId, long? proposedParentId, IReadOnlyDictionary<long, long?> parentsById)
    {
        var current = proposedParentId;
        var visited = new HashSet<long>();

        while (current is not null)
        {
            if (current.Value == candidateId)
            {
                return true;
            }

            if (!visited.Add(current.Value))
            {
                // A cycle exists further up the tree, unrelated to this change — do not loop forever,
                // and do not report it as caused by this candidate.
                return false;
            }

            current = parentsById.GetValueOrDefault(current.Value);
        }

        return false;
    }
}
