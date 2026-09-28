using Habbak.ERP.Domain.Common;

namespace Habbak.ERP.Domain.HR;

/// <summary>
/// System-wide reference catalog — أدّى، معافى، مؤجل، لا ينطبق. Seeded once, no admin screen in this
/// phase (Docs/Implementation/HR-Core-Plan.md §1.1, Hybrid decision 2026-09-26).
/// </summary>
public class MilitaryStatus : AuditableEntity, ILookupEntity
{
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
