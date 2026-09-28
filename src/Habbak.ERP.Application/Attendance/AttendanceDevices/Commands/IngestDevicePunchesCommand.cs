using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.AttendanceDevices.Commands;

/// <summary>خط واحد كما وصل من الجهاز/الملف — خام تمامًا، بدون أي تفسير (§3.1).</summary>
public sealed record RawPunchLineInput(string DeviceUserId, DateTime PunchTimestampUtc, int? RawStatus, int? RawVerifyType);

public sealed record IngestResultDto(int Received, int Accepted, int Duplicates);

/// <summary>
/// Docs/Implementation/Phase-3B-Research.md §3.1/§6 (3B.3/3B.5) — الاستقبال الفعلي، مشترك بين
/// IngestDevicePunchesCommandHandler (Push) وImportRawPunchesFromFileCommandHandler (File Import)
/// — Static عادي (مش Command تاني عن طريق ISender) عشان يفضل قابل للاستدعاء المباشر في الاختبارات
/// من غير أي بنية DI/Mediator (نفس فلسفة باقي هاندلرز Phase 3، Attendance/Services/TimeEntryFeedService
/// مثلًا). Idempotent عند الإدخال نفسه: Unique Index على (AttendanceDeviceId, DeviceUserId,
/// PunchTimestampUtc) هو خط الدفاع الأخير في الـDB، لكن الفحص هنا (Get-or-Create) بيمنع IX violation
/// في السيناريو العادي (نفس Push جالي مرتين).
/// </summary>
internal static class RawPunchIngestor
{
    public static async Task<IngestResultDto> IngestAsync(IApplicationDbContext db, long attendanceDeviceId, IReadOnlyList<RawPunchLineInput> lines, RawPunchSourceType sourceType, CancellationToken cancellationToken)
    {
        var device = await db.AttendanceDevices.FindAsync([attendanceDeviceId], cancellationToken)
            ?? throw new NotFoundException(nameof(AttendanceDevice), attendanceDeviceId);

        var utcNow = DateTime.UtcNow;

        if (lines.Count == 0)
        {
            device.LastSeenAtUtc = utcNow;
            db.AttendanceDeviceLogs.Add(NewLog(device, sourceType, utcNow, received: 0, processed: 0));
            await db.SaveChangesAsync(cancellationToken);
            return new IngestResultDto(0, 0, 0);
        }

        var timestamps = lines.Select(l => l.PunchTimestampUtc).Distinct().ToList();
        var existing = await db.RawPunches
            .Where(p => p.AttendanceDeviceId == attendanceDeviceId && timestamps.Contains(p.PunchTimestampUtc))
            .Select(p => new { p.DeviceUserId, p.PunchTimestampUtc })
            .ToListAsync(cancellationToken);
        var seen = existing.Select(k => (k.DeviceUserId, k.PunchTimestampUtc)).ToHashSet();

        var accepted = 0;
        var duplicates = 0;
        foreach (var line in lines)
        {
            var key = (line.DeviceUserId, line.PunchTimestampUtc);
            if (!seen.Add(key))
            {
                duplicates++;
                continue;
            }

            db.RawPunches.Add(new RawPunch
            {
                CompanyId = device.CompanyId,
                AttendanceDeviceId = device.Id,
                DeviceUserId = line.DeviceUserId,
                PunchTimestampUtc = line.PunchTimestampUtc,
                RawStatus = line.RawStatus,
                RawVerifyType = line.RawVerifyType,
                SourceType = sourceType,
                ReceivedAtUtc = utcNow,
                ProcessingStatus = RawPunchProcessingStatus.Pending
            });
            accepted++;
        }

        device.LastSeenAtUtc = utcNow;
        db.AttendanceDeviceLogs.Add(NewLog(device, sourceType, utcNow, lines.Count, accepted));

        await db.SaveChangesAsync(cancellationToken);
        return new IngestResultDto(lines.Count, accepted, duplicates);
    }

    private static AttendanceDeviceLog NewLog(AttendanceDevice device, RawPunchSourceType sourceType, DateTime utcNow, int received, int processed) => new()
    {
        CompanyId = device.CompanyId,
        AttendanceDeviceId = device.Id,
        SyncType = sourceType,
        StartedAtUtc = utcNow,
        FinishedAtUtc = utcNow,
        Status = AttendanceDeviceLogStatus.Success,
        PunchesReceived = received,
        PunchesProcessed = processed
    };
}

/// <summary>Wrapper رقيق عشان الـControllers (Push JSON/ADMS) تقدر تستدعي عن طريق ISender زي أي Command تاني.</summary>
public sealed record IngestDevicePunchesCommand(long AttendanceDeviceId, IReadOnlyList<RawPunchLineInput> Lines, RawPunchSourceType SourceType) : IRequest<IngestResultDto>;

public sealed class IngestDevicePunchesCommandHandler(IApplicationDbContext db) : IRequestHandler<IngestDevicePunchesCommand, IngestResultDto>
{
    public Task<IngestResultDto> Handle(IngestDevicePunchesCommand request, CancellationToken cancellationToken) =>
        RawPunchIngestor.IngestAsync(db, request.AttendanceDeviceId, request.Lines, request.SourceType, cancellationToken);
}
