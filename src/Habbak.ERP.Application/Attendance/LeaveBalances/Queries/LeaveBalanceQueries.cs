using Habbak.ERP.Application.Attendance.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.LeaveBalances.Queries;

public sealed record GetLeaveBalancesListQuery(long? EmployeeId, int? Year) : IRequest<IReadOnlyList<LeaveBalanceDto>>;

public sealed class GetLeaveBalancesListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLeaveBalancesListQuery, IReadOnlyList<LeaveBalanceDto>>
{
    public async Task<IReadOnlyList<LeaveBalanceDto>> Handle(GetLeaveBalancesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.LeaveBalances.AsNoTracking().AsQueryable();
        if (request.EmployeeId is not null) query = query.Where(b => b.EmployeeId == request.EmployeeId);
        if (request.Year is not null) query = query.Where(b => b.Year == request.Year);

        return await query.OrderBy(b => b.EmployeeId).ThenBy(b => b.LeaveTypeId).Select(ToDto).ToListAsync(cancellationToken);
    }

    // b.Available (خاصية محسوبة، Ignored في EF) بتتحسب هنا يدويًا بدل الاعتماد على ترجمة EF
    // للخاصية المحسوبة داخل Select — نفس الصيغة بالظبط الموجودة في LeaveBalance.Available.
    internal static readonly System.Linq.Expressions.Expression<Func<LeaveBalance, LeaveBalanceDto>> ToDto = b => new LeaveBalanceDto
    {
        Id = b.Id, EmployeeId = b.EmployeeId, LeaveTypeId = b.LeaveTypeId, Year = b.Year,
        AccruedThisYear = b.AccruedThisYear, CarriedOver = b.CarriedOver, Used = b.Used, Pending = b.Pending,
        Available = b.AccruedThisYear + b.CarriedOver - b.Used - b.Pending
    };
}

public sealed record GetLeaveBalanceByIdQuery(long Id) : IRequest<LeaveBalanceDto>;

public sealed class GetLeaveBalanceByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLeaveBalanceByIdQuery, LeaveBalanceDto>
{
    public async Task<LeaveBalanceDto> Handle(GetLeaveBalanceByIdQuery request, CancellationToken cancellationToken) =>
        await db.LeaveBalances.AsNoTracking().Where(b => b.Id == request.Id).Select(GetLeaveBalancesListQueryHandler.ToDto).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(LeaveBalance), request.Id);
}

public sealed record GetLeaveBalanceHistoryQuery(long LeaveBalanceId) : IRequest<IReadOnlyList<LeaveBalanceHistoryDto>>;

public sealed class GetLeaveBalanceHistoryQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLeaveBalanceHistoryQuery, IReadOnlyList<LeaveBalanceHistoryDto>>
{
    public async Task<IReadOnlyList<LeaveBalanceHistoryDto>> Handle(GetLeaveBalanceHistoryQuery request, CancellationToken cancellationToken) =>
        await db.LeaveBalanceHistories.AsNoTracking()
            .Where(h => h.LeaveBalanceId == request.LeaveBalanceId)
            .OrderByDescending(h => h.EffectiveDate).ThenByDescending(h => h.Id)
            .Select(h => new LeaveBalanceHistoryDto
            {
                Id = h.Id, MovementType = h.MovementType, Days = h.Days, EffectiveDate = h.EffectiveDate,
                SourceType = h.SourceType, SourceId = h.SourceId, Reason = h.Reason
            })
            .ToListAsync(cancellationToken);
}

/// <summary>نطاق Self — رصيد إجازات الموظف المرتبط بالمستخدم الحالي.</summary>
public sealed record GetMyLeaveBalanceQuery(int? Year) : IRequest<IReadOnlyList<LeaveBalanceDto>>;

public sealed class GetMyLeaveBalanceQueryHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<GetMyLeaveBalanceQuery, IReadOnlyList<LeaveBalanceDto>>
{
    public async Task<IReadOnlyList<LeaveBalanceDto>> Handle(GetMyLeaveBalanceQuery request, CancellationToken cancellationToken)
    {
        if (current.EmployeeId is not { } employeeId)
        {
            return [];
        }

        var year = request.Year ?? DateTime.UtcNow.Year;
        return await db.LeaveBalances.AsNoTracking().Where(b => b.EmployeeId == employeeId && b.Year == year)
            .Select(GetLeaveBalancesListQueryHandler.ToDto).ToListAsync(cancellationToken);
    }
}
