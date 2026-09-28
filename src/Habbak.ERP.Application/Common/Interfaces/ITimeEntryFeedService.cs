using Habbak.ERP.Domain.POS;

namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>
/// Docs/Implementation/Phase-3-Research.md §3.1 — التلقيم من POS.Shift لـ TimeEntry بنداء مباشر
/// (مش Event، مفيش بنية Event Bus في المشروع أصلًا). بتتنادى من OpenShiftCommandHandler/
/// CloseShiftCommandHandler بعد الـ SaveChangesAsync بتاعتهم هم — صفر تعديل على Shift Entity نفسه.
/// Best-Effort دايمًا: أي فجوة في الربط (كاشير من غير Employee) بتتجاهل بصمت، الوردية مايتأثرش
/// (Phase-3-Research.md §3.2).
/// </summary>
public interface ITimeEntryFeedService
{
    Task SuggestEntryForShiftOpenAsync(Shift shift, CancellationToken cancellationToken);

    Task SuggestEntryForShiftCloseAsync(Shift shift, CancellationToken cancellationToken);
}
