using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.Employees.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Queries.GetEmployeesLookup;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B5 — the same "list is paginated elsewhere, so a
/// separate unpaginated lookup exists" shape as GetUsersLookupQuery (Settings/Users/CompanyUsers.cs)
/// — exposed via EmployeesLookupController with [AnySignedInUser], not this screen's own real
/// permission (EmployeesController's [Screen("HR_EMPLOYEES")], no LookupReads — Employee data is not
/// as harmless as Country/JobGrade to expose broadly).
/// </summary>
public sealed record GetEmployeesLookupQuery : IRequest<IReadOnlyList<EmployeeLookupDto>>;

public sealed class GetEmployeesLookupQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmployeesLookupQuery, IReadOnlyList<EmployeeLookupDto>>
{
    public async Task<IReadOnlyList<EmployeeLookupDto>> Handle(GetEmployeesLookupQuery request, CancellationToken cancellationToken)
    {
        return await db.Employees
            .AsNoTracking()
            .OrderBy(e => e.Code)
            .Select(e => new EmployeeLookupDto { Id = e.Id, Code = e.Code, NameAr = e.NameAr, NameEn = e.NameEn, IsActive = e.IsActive })
            .ToListAsync(cancellationToken);
    }
}
