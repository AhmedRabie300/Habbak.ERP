using Habbak.ERP.Application.Attendance.EmployeeDeviceMappings.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.EmployeeDeviceMappings.Queries;

public sealed record GetEmployeeDeviceMappingsListQuery(long? EmployeeId, long? AttendanceDeviceId) : IRequest<IReadOnlyList<EmployeeDeviceMappingDto>>;

public sealed class GetEmployeeDeviceMappingsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmployeeDeviceMappingsListQuery, IReadOnlyList<EmployeeDeviceMappingDto>>
{
    public async Task<IReadOnlyList<EmployeeDeviceMappingDto>> Handle(GetEmployeeDeviceMappingsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.EmployeeDeviceMappings.AsNoTracking().Include(m => m.Employee).Include(m => m.AttendanceDevice).AsQueryable();

        if (request.EmployeeId is { } employeeId)
        {
            query = query.Where(m => m.EmployeeId == employeeId);
        }

        if (request.AttendanceDeviceId is { } deviceId)
        {
            query = query.Where(m => m.AttendanceDeviceId == deviceId);
        }

        return await query.OrderBy(m => m.Employee.Code)
            .Select(m => new EmployeeDeviceMappingDto
            {
                Id = m.Id, EmployeeId = m.EmployeeId, EmployeeCode = m.Employee.Code, EmployeeNameAr = m.Employee.NameAr,
                AttendanceDeviceId = m.AttendanceDeviceId, AttendanceDeviceCode = m.AttendanceDevice.Code, DeviceUserId = m.DeviceUserId
            })
            .ToListAsync(cancellationToken);
    }
}
