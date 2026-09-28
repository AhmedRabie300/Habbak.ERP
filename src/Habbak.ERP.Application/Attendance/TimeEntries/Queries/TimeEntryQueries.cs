using Habbak.ERP.Application.Attendance.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.TimeEntries.Queries;

public sealed record GetTimeEntriesListQuery(long? EmployeeId, DateOnly? FromDate, DateOnly? ToDate, TimeEntryStatus? Status) : IRequest<IReadOnlyList<TimeEntryDto>>;

public sealed class GetTimeEntriesListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetTimeEntriesListQuery, IReadOnlyList<TimeEntryDto>>
{
    public async Task<IReadOnlyList<TimeEntryDto>> Handle(GetTimeEntriesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.TimeEntries.AsNoTracking().AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(t => t.EmployeeId == request.EmployeeId);
        if (request.FromDate is not null) query = query.Where(t => t.TimestampUtc >= request.FromDate.Value.ToDateTime(TimeOnly.MinValue));
        if (request.ToDate is not null) query = query.Where(t => t.TimestampUtc < request.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (request.Status is not null) query = query.Where(t => t.Status == request.Status);

        return await query.OrderByDescending(t => t.TimestampUtc)
            .Select(t => new TimeEntryDto
            {
                Id = t.Id, EmployeeId = t.EmployeeId, EntryType = t.EntryType, TimestampUtc = t.TimestampUtc, Source = t.Source,
                POSShiftId = t.POSShiftId, DeviceId = t.DeviceId, Status = t.Status, IsCorrection = t.IsCorrection, CorrectsTimeEntryId = t.CorrectsTimeEntryId
            })
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetTimeEntryByIdQuery(long Id) : IRequest<TimeEntryDto>;

public sealed class GetTimeEntryByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetTimeEntryByIdQuery, TimeEntryDto>
{
    public async Task<TimeEntryDto> Handle(GetTimeEntryByIdQuery request, CancellationToken cancellationToken) =>
        await db.TimeEntries.AsNoTracking().Where(t => t.Id == request.Id)
            .Select(t => new TimeEntryDto
            {
                Id = t.Id, EmployeeId = t.EmployeeId, EntryType = t.EntryType, TimestampUtc = t.TimestampUtc, Source = t.Source,
                POSShiftId = t.POSShiftId, DeviceId = t.DeviceId, Status = t.Status, IsCorrection = t.IsCorrection, CorrectsTimeEntryId = t.CorrectsTimeEntryId
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(TimeEntry), request.Id);
}
