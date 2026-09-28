using FluentValidation;
using Habbak.ERP.Application.Attendance.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.LeaveRequests.Queries;

public sealed record GetLeaveRequestsListQuery(long? EmployeeId) : IRequest<IReadOnlyList<LeaveRequestDto>>;

public sealed class GetLeaveRequestsListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLeaveRequestsListQuery, IReadOnlyList<LeaveRequestDto>>
{
    public async Task<IReadOnlyList<LeaveRequestDto>> Handle(GetLeaveRequestsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.LeaveRequests.AsNoTracking().AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(r => r.EmployeeId == request.EmployeeId);

        return await query.OrderByDescending(r => r.StartDate).Select(ToDto).ToListAsync(cancellationToken);
    }

    internal static readonly System.Linq.Expressions.Expression<Func<LeaveRequest, LeaveRequestDto>> ToDto = r => new LeaveRequestDto
    {
        Id = r.Id, EmployeeId = r.EmployeeId, LeaveTypeId = r.LeaveTypeId, StartDate = r.StartDate, EndDate = r.EndDate,
        Days = r.Days, Reason = r.Reason, Status = r.Status, ApprovalInstanceId = r.ApprovalInstanceId
    };
}

public sealed record GetLeaveRequestByIdQuery(long Id) : IRequest<LeaveRequestDto>;

public sealed class GetLeaveRequestByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLeaveRequestByIdQuery, LeaveRequestDto>
{
    public async Task<LeaveRequestDto> Handle(GetLeaveRequestByIdQuery request, CancellationToken cancellationToken) =>
        await db.LeaveRequests.AsNoTracking().Where(r => r.Id == request.Id).Select(GetLeaveRequestsListQueryHandler.ToDto).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(LeaveRequest), request.Id);
}

/// <summary>Preview للـ Frontend قبل إنشاء الطلب فعليًا — نفس LeaveDaysCalculator المُستخدَم في
/// CreateLeaveRequestCommand بالظبط، عشان الطلب النهائي يطابق المعاينة.</summary>
public sealed record CalculateLeaveDaysQuery(long EmployeeId, DateOnly StartDate, DateOnly EndDate) : IRequest<CalculateLeaveDaysResultDto>;

public sealed class CalculateLeaveDaysQueryValidator : AbstractValidator<CalculateLeaveDaysQuery>
{
    public CalculateLeaveDaysQueryValidator()
    {
        RuleFor(x => x.EmployeeId).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate);
    }
}

public sealed class CalculateLeaveDaysQueryHandler(IApplicationDbContext db) : IRequestHandler<CalculateLeaveDaysQuery, CalculateLeaveDaysResultDto>
{
    public async Task<CalculateLeaveDaysResultDto> Handle(CalculateLeaveDaysQuery request, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(e => e.Id == request.EmployeeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Habbak.ERP.Domain.HR.Employee), request.EmployeeId);

        var result = await LeaveDaysCalculator.CalculateAsync(db, request.EmployeeId, employee.CompanyId, request.StartDate, request.EndDate, cancellationToken);
        return new CalculateLeaveDaysResultDto { Days = result.Days, Mode = result.Mode, UsedFallback = result.UsedFallback };
    }
}
