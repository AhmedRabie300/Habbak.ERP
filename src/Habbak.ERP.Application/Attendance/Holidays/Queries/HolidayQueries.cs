using Habbak.ERP.Application.Attendance.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Attendance.Holidays.Queries;

public sealed record GetHolidaysListQuery(int? Year) : IRequest<IReadOnlyList<HolidayDto>>;

public sealed class GetHolidaysListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetHolidaysListQuery, IReadOnlyList<HolidayDto>>
{
    public async Task<IReadOnlyList<HolidayDto>> Handle(GetHolidaysListQuery request, CancellationToken cancellationToken)
    {
        var query = db.Holidays.AsNoTracking().AsQueryable();
        if (request.Year is not null) query = query.Where(h => h.Year == request.Year);

        return await query.OrderBy(h => h.StartDate).Select(ToDto).ToListAsync(cancellationToken);
    }

    internal static readonly System.Linq.Expressions.Expression<Func<Holiday, HolidayDto>> ToDto = h => new HolidayDto
    {
        Id = h.Id, Code = h.Code, NameAr = h.NameAr, NameEn = h.NameEn, IsActive = h.IsActive,
        StartDate = h.StartDate, EndDate = h.EndDate, Year = h.Year, IsNational = h.IsNational, BranchId = h.BranchId
    };
}

public sealed record GetHolidayByIdQuery(long Id) : IRequest<HolidayDto>;

public sealed class GetHolidayByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetHolidayByIdQuery, HolidayDto>
{
    public async Task<HolidayDto> Handle(GetHolidayByIdQuery request, CancellationToken cancellationToken) =>
        await db.Holidays.AsNoTracking().Where(h => h.Id == request.Id).Select(GetHolidaysListQueryHandler.ToDto).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(nameof(Holiday), request.Id);
}
