using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.AttendanceDevices.Commands;

/// <summary>خط واحد كما وصل من الجهاز/الملف — خام تمامًا، بدون أي تفسير (§3.1).</summary>
public sealed record RawPunchLineInput(string DeviceUserId, DateTime PunchTimestampUtc, int? RawStatus, int? RawVerifyType);

public sealed record IngestResultDto(int Received, int Accepted, int Duplicates);

/// <summary>
/// Docs/Implementation/Phase-3B-Research.md §3.1/§6 (3B.3) — استقبال دفعة بصمات خام من جهاز واحد
/// (Push أو File Import، §3B.5 بيستخدم نفس الأمر). Idempotent عند الإدخال نفسه: Unique Index على
/// (AttendanceDeviceId, DeviceUserId, PunchTimestampUtc) هو خط الدفاع الأخير في الـDB، لكن الفحص هنا
/// (Get-or-Create) بيمنع IX violation في السيناريو العادي (نفس Push جالي مرتين). المستدعي (Controller)
/// لازم يكون فاتح BackgroundCompanyScope.Begin(device.CompanyId) قبل النداء — مفيش JWT/CompanyId
/// حقيقي هنا (§7 قرار 4).
/// </summary>
public sealed record IngestDevicePunchesCommand(long AttendanceDeviceId, IReadOnlyList<RawPunchLineInput> Lines, RawPunchSourceType SourceType) : IRequest<IngestResultDto>;

public sealed class IngestDevicePunchesCommandHandler(IApplicationDbContext db) : IRequestHandler<IngestDevicePunchesCommand, IngestResultDto>
{
    public async Task<IngestResultDto> Handle(IngestDevicePunchesCommand request, CancellationToken cancellationToken)
    {
        var device = await db.AttendanceDevices.FindAsync([request.AttendanceDeviceId], cancellationToken)
            ?? throw new Common.Exceptions.NotFoundException(nameof(AttendanceDevice), request.AttendanceDeviceId);

        var utcNow = DateTime.UtcNow;

        if (request.Lines.Count == 0)
        {
            device.LastSeenAtUtc = utcNow;
            db.AttendanceDeviceLogs.Add(NewLog(device, request.SourceType, utcNow, received: 0, processed: 0));
            await db.SaveChangesAsync(cancellationToken);
            return new IngestResultDto(0, 0, 0);
        }

        var timestamps = request.Lines.Select(l => l.PunchTimestampUtc).Distinct().ToList();
        var existing = await db.RawPunches
            .Where(p => p.AttendanceDeviceId == request.AttendanceDeviceId && timestamps.Contains(p.PunchTimestampUtc))
            .Select(p => new { p.DeviceUserId, p.PunchTimestampUtc })
            .ToListAsync(cancellationToken);
        var seen = existing.Select(k => (k.DeviceUserId, k.PunchTimestampUtc)).ToHashSet();

        var accepted = 0;
        var duplicates = 0;
        foreach (var line in request.Lines)
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
                SourceType = request.SourceType,
                ReceivedAtUtc = utcNow,
                ProcessingStatus = RawPunchProcessingStatus.Pending
            });
            accepted++;
        }

        device.LastSeenAtUtc = utcNow;
        db.AttendanceDeviceLogs.Add(NewLog(device, request.SourceType, utcNow, request.Lines.Count, accepted));

        await db.SaveChangesAsync(cancellationToken);
        return new IngestResultDto(request.Lines.Count, accepted, duplicates);
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
