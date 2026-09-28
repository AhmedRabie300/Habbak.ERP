using Habbak.ERP.Application.Attendance.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.OvertimeRequests.Queries;

public sealed record GetOvertimeRequestsListQuery(long? EmployeeId) : IRequest<IReadOnlyList<OvertimeRequestDto>>;

public sealed class GetOvertimeRequestsListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetOvertimeRequestsListQuery, IReadOnlyList<OvertimeRequestDto>>
{
    public async Task<IReadOnlyList<OvertimeRequestDto>> Handle(GetOvertimeRequestsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.OvertimeRequests.AsNoTracking().AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(o => o.EmployeeId == request.EmployeeId);

        return await query.OrderByDescending(o => o.WorkDate).Select(ToDto).ToListAsync(cancellationToken);
    }

    internal static readonly System.Linq.Expressions.Expression<Func<OvertimeRequest, OvertimeRequestDto>> ToDto = o => new OvertimeRequestDto
    {
        Id = o.Id, EmployeeId = o.EmployeeId, WorkDate = o.WorkDate, PlannedMinutes = o.PlannedMinutes,
        OvertimeType = o.OvertimeType, Reason = o.Reason, Status = o.Status, ApprovalInstanceId = o.ApprovalInstanceId, ActualMinutes = o.ActualMinutes
    };
}

public sealed record GetOvertimeRequestByIdQuery(long Id) : IRequest<OvertimeRequestDto>;

public sealed class GetOvertimeRequestByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetOvertimeRequestByIdQuery, OvertimeRequestDto>
{
    public async Task<OvertimeRequestDto> Handle(GetOvertimeRequestByIdQuery request, CancellationToken cancellationToken) =>
        await db.OvertimeRequests.AsNoTracking().Where(o => o.Id == request.Id).Select(GetOvertimeRequestsListQueryHandler.ToDto).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(OvertimeRequest), request.Id);
}
