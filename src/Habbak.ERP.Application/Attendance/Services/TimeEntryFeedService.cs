using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.Services;

/// <summary>التنفيذ الفعلي لـ ITimeEntryFeedService — قاعدة 11: فتح=دخول مقترح، قفل=خروج مقترح،
/// اتقبل تلقائيًا لو مفيش تعارض. Idempotency: فحص وجود مسبق بـ (POSShiftId, EntryType) بدل مفتاح
/// منفصل (Phase-3-Research.md §3.3) — فريد أصلًا لأن Shift.Id نفسه فريد.</summary>
public sealed class TimeEntryFeedService(IApplicationDbContext db) : ITimeEntryFeedService
{
    public Task SuggestEntryForShiftOpenAsync(Shift shift, CancellationToken cancellationToken) =>
        SuggestAsync(shift, TimeEntryType.In, shift.OpenedAtUtc, cancellationToken);

    public Task SuggestEntryForShiftCloseAsync(Shift shift, CancellationToken cancellationToken) =>
        shift.ClosedAtUtc is { } closedAtUtc ? SuggestAsync(shift, TimeEntryType.Out, closedAtUtc, cancellationToken) : Task.CompletedTask;

    private async Task SuggestAsync(Shift shift, TimeEntryType entryType, DateTime timestampUtc, CancellationToken cancellationToken)
    {
        if (shift.BranchId is null)
        {
            return; // Silent Ignore (§3.2) — مفيش فرع، مفيش تلقيم.
        }

        var employee = await db.Employees.FirstOrDefaultAsync(e => e.UserId == shift.CashierUserId, cancellationToken);
        if (employee is null)
        {
            return; // Silent Ignore (§3.2) — الكاشير مش مربوط بموظف؛ تقرير التعارضات (3.5) هيكشف الحالة دي.
        }

        var alreadyExists = await db.TimeEntries.AnyAsync(t => t.POSShiftId == shift.Id && t.EntryType == entryType, cancellationToken);
        if (alreadyExists)
        {
            return; // Idempotency (§3.3).
        }

        var dayStart = timestampUtc.Date;
        var dayEnd = dayStart.AddDays(1);
        var hasConflict = await db.TimeEntries.AnyAsync(
            t => t.EmployeeId == employee.Id && t.POSShiftId != shift.Id && t.Status != TimeEntryStatus.Dismissed
                 && t.TimestampUtc >= dayStart && t.TimestampUtc < dayEnd,
            cancellationToken);

        db.TimeEntries.Add(new TimeEntry
        {
            CompanyId = employee.CompanyId,
            BranchId = shift.BranchId,
            EmployeeId = employee.Id,
            EntryType = entryType,
            TimestampUtc = timestampUtc,
            Source = TimeEntrySource.POSShift,
            POSShiftId = shift.Id,
            Status = hasConflict ? TimeEntryStatus.Suggested : TimeEntryStatus.Accepted
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
