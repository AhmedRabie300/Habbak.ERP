using Habbak.ERP.Application.Common.Exceptions;

namespace Habbak.ERP.Application.Common;

/// <summary>
/// Bug-002: a document that carries a BranchId must always end up with one — never silently null —
/// since PostingService's mandatory-dimension auto-resolve (Bug-001) depends on it. The UI makes the
/// field required, so this is the second line of defense: prefer what the request explicitly sent,
/// else fall back to the current session's own branch scope, else refuse with a clear error.
/// </summary>
internal static class BranchRequirement
{
    public static long Resolve(long? requestBranchId, long? contextBranchId) =>
        requestBranchId ?? contextBranchId
        ?? throw new BusinessRuleException("ACC-BRANCH-REQUIRED", "المستند لازم يكون مرتبط بفرع — اختر الفرع قبل الحفظ.");
}
