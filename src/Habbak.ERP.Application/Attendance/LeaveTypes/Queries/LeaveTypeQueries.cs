using Habbak.ERP.Application.Attendance.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.LeaveTypes.Queries;

public sealed record GetLeaveTypesListQuery : IRequest<IReadOnlyList<LeaveTypeDto>>;

public sealed class GetLeaveTypesListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLeaveTypesListQuery, IReadOnlyList<LeaveTypeDto>>
{
    public async Task<IReadOnlyList<LeaveTypeDto>> Handle(GetLeaveTypesListQuery request, CancellationToken cancellationToken) =>
        await db.LeaveTypes.AsNoTracking().OrderBy(l => l.Code).Select(ToDto).ToListAsync(cancellationToken);

    internal static readonly System.Linq.Expressions.Expression<Func<LeaveType, LeaveTypeDto>> ToDto = l => new LeaveTypeDto
    {
        Id = l.Id, Code = l.Code, NameAr = l.NameAr, NameEn = l.NameEn, IsActive = l.IsActive,
        AccrualMethod = l.AccrualMethod, AnnualDays = l.AnnualDays, MaxCarryOver = l.MaxCarryOver,
        IsPaid = l.IsPaid, PaidPercentage = l.PaidPercentage, DeductFromLeaveTypeId = l.DeductFromLeaveTypeId,
        RequiresDocument = l.RequiresDocument, MaxDaysPerRequest = l.MaxDaysPerRequest, GenderRestriction = l.GenderRestriction,
        MaxTimesInService = l.MaxTimesInService, IsCashableOnTermination = l.IsCashableOnTermination
    };
}

public sealed record GetLeaveTypeByIdQuery(long Id) : IRequest<LeaveTypeDto>;

public sealed class GetLeaveTypeByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLeaveTypeByIdQuery, LeaveTypeDto>
{
    public async Task<LeaveTypeDto> Handle(GetLeaveTypeByIdQuery request, CancellationToken cancellationToken) =>
        await db.LeaveTypes.AsNoTracking().Where(l => l.Id == request.Id).Select(GetLeaveTypesListQueryHandler.ToDto).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(LeaveType), request.Id);
}
