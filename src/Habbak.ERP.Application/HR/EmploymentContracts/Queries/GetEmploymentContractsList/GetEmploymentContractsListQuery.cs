using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.HR.EmploymentContracts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.EmploymentContracts.Queries.GetEmploymentContractsList;

/// <summary>Docs/Implementation/HR-Core-Plan.md §1.1, Batch B6 — paginated per the plan's own explicit choice (unlike EmployeeDocuments/EmployeeCertifications below, which stay unpaginated).</summary>
public sealed class GetEmploymentContractsListQuery : ListQuery, IRequest<PagedResult<EmploymentContractDto>>
{
    public required long EmployeeId { get; init; }
}

public sealed class GetEmploymentContractsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetEmploymentContractsListQuery, PagedResult<EmploymentContractDto>>
{
    public async Task<PagedResult<EmploymentContractDto>> Handle(GetEmploymentContractsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.EmploymentContracts.AsNoTracking().Where(c => c.EmployeeId == request.EmployeeId);

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "startDate" => descending ? query.OrderByDescending(c => c.StartDate) : query.OrderBy(c => c.StartDate),
            "status" => descending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            _ => descending ? query.OrderByDescending(c => c.StartDate) : query.OrderBy(c => c.StartDate)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new EmploymentContractDto
            {
                Id = c.Id, EmployeeId = c.EmployeeId, BranchId = c.BranchId, ContractType = c.ContractType,
                StartDate = c.StartDate, EndDate = c.EndDate, ProbationEndDate = c.ProbationEndDate,
                BasicSalary = c.BasicSalary, InsurableWage = c.InsurableWage, WorkingHoursPerDay = c.WorkingHoursPerDay,
                Status = c.Status, PreviousContractId = c.PreviousContractId, ApprovalInstanceId = c.ApprovalInstanceId,
                AttachmentId = c.AttachmentId, Lines = Array.Empty<ContractLineDto>()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<EmploymentContractDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }
}
