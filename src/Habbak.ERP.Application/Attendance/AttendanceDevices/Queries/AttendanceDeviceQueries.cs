using Habbak.ERP.Application.Attendance.AttendanceDevices.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.AttendanceDevices.Queries;

public sealed record GetAttendanceDevicesListQuery : IRequest<IReadOnlyList<AttendanceDeviceDto>>;

public sealed class GetAttendanceDevicesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAttendanceDevicesListQuery, IReadOnlyList<AttendanceDeviceDto>>
{
    public async Task<IReadOnlyList<AttendanceDeviceDto>> Handle(GetAttendanceDevicesListQuery request, CancellationToken cancellationToken) =>
        await db.AttendanceDevices.AsNoTracking().OrderBy(d => d.Code)
            .Select(d => new AttendanceDeviceDto
            {
                Id = d.Id, Code = d.Code, NameAr = d.NameAr, NameEn = d.NameEn, Model = d.Model,
                SerialNumber = d.SerialNumber, BranchId = d.BranchId, IsActive = d.IsActive, LastSeenAtUtc = d.LastSeenAtUtc
            })
            .ToListAsync(cancellationToken);
}

public sealed record GetAttendanceDeviceByIdQuery(long Id) : IRequest<AttendanceDeviceDto>;

public sealed class GetAttendanceDeviceByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAttendanceDeviceByIdQuery, AttendanceDeviceDto>
{
    public async Task<AttendanceDeviceDto> Handle(GetAttendanceDeviceByIdQuery request, CancellationToken cancellationToken) =>
        await db.AttendanceDevices.AsNoTracking().Where(d => d.Id == request.Id)
            .Select(d => new AttendanceDeviceDto
            {
                Id = d.Id, Code = d.Code, NameAr = d.NameAr, NameEn = d.NameEn, Model = d.Model,
                SerialNumber = d.SerialNumber, BranchId = d.BranchId, IsActive = d.IsActive, LastSeenAtUtc = d.LastSeenAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(AttendanceDevice), request.Id);
}

public sealed record GetAttendanceDeviceLogsQuery(long? AttendanceDeviceId, DateOnly? FromDate, DateOnly? ToDate) : IRequest<IReadOnlyList<AttendanceDeviceLogDto>>;

public sealed class GetAttendanceDeviceLogsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAttendanceDeviceLogsQuery, IReadOnlyList<AttendanceDeviceLogDto>>
{
    public async Task<IReadOnlyList<AttendanceDeviceLogDto>> Handle(GetAttendanceDeviceLogsQuery request, CancellationToken cancellationToken)
    {
        var query = db.AttendanceDeviceLogs.AsNoTracking().Include(l => l.AttendanceDevice).AsQueryable();

        if (request.AttendanceDeviceId is { } deviceId)
        {
            query = query.Where(l => l.AttendanceDeviceId == deviceId);
        }

        if (request.FromDate is { } from)
        {
            var fromUtc = from.ToDateTime(TimeOnly.MinValue);
            query = query.Where(l => l.StartedAtUtc >= fromUtc);
        }

        if (request.ToDate is { } to)
        {
            var toUtc = to.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(l => l.StartedAtUtc <= toUtc);
        }

        return await query.OrderByDescending(l => l.StartedAtUtc).Take(500)
            .Select(l => new AttendanceDeviceLogDto
            {
                Id = l.Id, AttendanceDeviceId = l.AttendanceDeviceId, DeviceCode = l.AttendanceDevice.Code,
                SyncType = (int)l.SyncType, StartedAtUtc = l.StartedAtUtc, FinishedAtUtc = l.FinishedAtUtc,
                Status = (int)l.Status, PunchesReceived = l.PunchesReceived, PunchesProcessed = l.PunchesProcessed, ErrorMessage = l.ErrorMessage
            })
            .ToListAsync(cancellationToken);
    }
}

/// <summary>§5 — Reconciliation. عدد البصمات الخام مقابل عدد TimeEntry الناتجة + قائمة الاستثناءات.</summary>
public sealed record GetRawPunchReconciliationQuery(long? AttendanceDeviceId, DateOnly FromDate, DateOnly ToDate)
    : IRequest<(RawPunchReconciliationSummaryDto Summary, IReadOnlyList<RawPunchExceptionDto> Exceptions)>;

public sealed class GetRawPunchReconciliationQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetRawPunchReconciliationQuery, (RawPunchReconciliationSummaryDto, IReadOnlyList<RawPunchExceptionDto>)>
{
    public async Task<(RawPunchReconciliationSummaryDto, IReadOnlyList<RawPunchExceptionDto>)> Handle(GetRawPunchReconciliationQuery request, CancellationToken cancellationToken)
    {
        var fromUtc = request.FromDate.ToDateTime(TimeOnly.MinValue);
        var toUtc = request.ToDate.ToDateTime(TimeOnly.MaxValue);

        var query = db.RawPunches.AsNoTracking().Include(p => p.AttendanceDevice)
            .Where(p => p.PunchTimestampUtc >= fromUtc && p.PunchTimestampUtc <= toUtc);

        if (request.AttendanceDeviceId is { } deviceId)
        {
            query = query.Where(p => p.AttendanceDeviceId == deviceId);
        }

        var rows = await query.ToListAsync(cancellationToken);

        var summary = new RawPunchReconciliationSummaryDto
        {
            TotalReceived = rows.Count,
            Processed = rows.Count(p => p.ProcessingStatus == RawPunchProcessingStatus.Processed),
            Pending = rows.Count(p => p.ProcessingStatus == RawPunchProcessingStatus.Pending),
            SkippedNoEmployeeMapping = rows.Count(p => p.SkipReason == RawPunchSkipReason.NoEmployeeMapping),
            SkippedOther = rows.Count(p => p.ProcessingStatus == RawPunchProcessingStatus.Skipped && p.SkipReason != RawPunchSkipReason.NoEmployeeMapping)
        };

        var exceptions = rows.Where(p => p.ProcessingStatus != RawPunchProcessingStatus.Processed)
            .OrderByDescending(p => p.PunchTimestampUtc)
            .Take(500)
            .Select(p => new RawPunchExceptionDto
            {
                Id = p.Id, AttendanceDeviceId = p.AttendanceDeviceId, DeviceCode = p.AttendanceDevice.Code,
                DeviceUserId = p.DeviceUserId, PunchTimestampUtc = p.PunchTimestampUtc,
                ProcessingStatus = p.ProcessingStatus.ToString(), SkipReason = p.SkipReason?.ToString()
            })
            .ToList();

        return (summary, exceptions);
    }
}
