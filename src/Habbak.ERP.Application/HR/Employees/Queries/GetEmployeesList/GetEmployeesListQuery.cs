using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.HR.Employees.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.Employees.Queries.GetEmployeesList;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B5 — unlike the 8 lookups (Batch B4, always
/// unpaginated), Employee can genuinely grow into the hundreds/thousands per company, so this follows
/// the transactional-list convention (PagedResult/ListQuery, same as GetJournalEntriesListQuery) —
/// not the small-reference-data one. CompanyId/BranchId filtering happens automatically via the
/// Global Query Filter (00-Frontend-Specs.md, section 6), never applied by hand here.
/// </summary>
public sealed class GetEmployeesListQuery : ListQuery, IRequest<PagedResult<EmployeeListItemDto>>;

public sealed class GetEmployeesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmployeesListQuery, PagedResult<EmployeeListItemDto>>
{
    public async Task<PagedResult<EmployeeListItemDto>> Handle(GetEmployeesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.Employees.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            var matchingStatuses = Enum.GetValues<EmployeeStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            query = query.Where(e =>
                e.Code.Contains(term) || e.NameAr.Contains(term) || e.NameEn.Contains(term) ||
                matchingStatuses.Contains(e.Status));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "nameAr" => descending ? query.OrderByDescending(e => e.NameAr) : query.OrderBy(e => e.NameAr),
            "hireDate" => descending ? query.OrderByDescending(e => e.HireDate) : query.OrderBy(e => e.HireDate),
            "status" => descending ? query.OrderByDescending(e => e.Status) : query.OrderBy(e => e.Status),
            _ => descending ? query.OrderByDescending(e => e.Code) : query.OrderBy(e => e.Code)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmployeeListItemDto
            {
                Id = e.Id, Code = e.Code, NameAr = e.NameAr, NameEn = e.NameEn, IsActive = e.IsActive,
                BranchId = e.BranchId, OrgUnitId = e.OrgUnitId, JobPositionId = e.JobPositionId, JobGradeId = e.JobGradeId,
                ManagerId = e.ManagerId, UserId = e.UserId, HireDate = e.HireDate, EmploymentType = e.EmploymentType, Status = e.Status
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<EmployeeListItemDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }
}
