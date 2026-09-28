using Habbak.ERP.Application.Approvals.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Domain.Approvals;
using Habbak.ERP.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Approvals.Queries;

public sealed class GetApprovalWorkflowsListQuery : ListQuery, IRequest<PagedResult<ApprovalWorkflowListItemDto>>;

public sealed class GetApprovalWorkflowsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetApprovalWorkflowsListQuery, PagedResult<ApprovalWorkflowListItemDto>>
{
    public async Task<PagedResult<ApprovalWorkflowListItemDto>> Handle(GetApprovalWorkflowsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.ApprovalWorkflows.AsNoTracking().Where(w => w.IsCurrentVersion);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var q = request.Search.Trim();
            query = query.Where(w => w.Code.Contains(q) || w.NameAr.Contains(q) || w.NameEn.Contains(q));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = descending ? query.OrderByDescending(w => w.Code) : query.OrderBy(w => w.Code);

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(w => new ApprovalWorkflowListItemDto(w.Id, w.Code, w.NameAr, w.NameEn, w.VersionNumber, w.IsActive, w.Steps.Count))
            .ToListAsync(cancellationToken);

        return new PagedResult<ApprovalWorkflowListItemDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }
}

public sealed record GetApprovalWorkflowByIdQuery(long Id) : IRequest<ApprovalWorkflowDetailDto>;

public sealed class GetApprovalWorkflowByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetApprovalWorkflowByIdQuery, ApprovalWorkflowDetailDto>
{
    public async Task<ApprovalWorkflowDetailDto> Handle(GetApprovalWorkflowByIdQuery request, CancellationToken cancellationToken)
    {
        var workflow = await db.ApprovalWorkflows
            .AsNoTracking()
            .Include(w => w.Steps).ThenInclude(s => s.Approvers)
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ApprovalWorkflow), request.Id);

        var employeeIds = workflow.Steps.SelectMany(s => s.Approvers)
            .Where(a => a.ApproverType == ApprovalApproverType.SpecificEmployee && a.ApproverReferenceId is not null)
            .Select(a => a.ApproverReferenceId!.Value).Distinct().ToList();
        var roleIds = workflow.Steps.SelectMany(s => s.Approvers)
            .Where(a => a.ApproverType == ApprovalApproverType.Role && a.ApproverReferenceId is not null)
            .Select(a => a.ApproverReferenceId!.Value).Distinct().ToList();
        var jobGradeIds = workflow.Steps.SelectMany(s => s.Approvers)
            .Where(a => a.ApproverType == ApprovalApproverType.JobGrade && a.ApproverReferenceId is not null)
            .Select(a => a.ApproverReferenceId!.Value).Distinct().ToList();

        var employeeNames = await db.Employees.Where(e => employeeIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.NameAr, cancellationToken);
        var roleNames = await db.Roles.Where(r => roleIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.NameAr, cancellationToken);
        var jobGradeNames = await db.JobGrades.Where(g => jobGradeIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.NameAr, cancellationToken);

        string? LabelFor(ApprovalStepApprover a) => a.ApproverType switch
        {
            ApprovalApproverType.SpecificEmployee => a.ApproverReferenceId is not null ? employeeNames.GetValueOrDefault(a.ApproverReferenceId.Value) : null,
            ApprovalApproverType.Role => a.ApproverReferenceId is not null ? roleNames.GetValueOrDefault(a.ApproverReferenceId.Value) : null,
            ApprovalApproverType.JobGrade => a.ApproverReferenceId is not null ? jobGradeNames.GetValueOrDefault(a.ApproverReferenceId.Value) : null,
            ApprovalApproverType.DirectManager => "المدير المباشر",
            _ => null
        };

        var steps = workflow.Steps.OrderBy(s => s.StepOrder).Select(s => new ApprovalWorkflowStepDto(
            s.Id, s.StepOrder, s.Mode,
            s.Approvers.Select(a => new ApprovalStepApproverDto(a.Id, a.ApproverType, a.ApproverReferenceId, LabelFor(a))).ToList())).ToList();

        return new ApprovalWorkflowDetailDto(workflow.Id, workflow.Code, workflow.NameAr, workflow.NameEn, workflow.VersionNumber, workflow.IsActive, steps);
    }
}

public sealed class GetScreensListQuery : IRequest<IReadOnlyList<ScreenDto>>;

public sealed class GetScreensListQueryHandler(IApplicationDbContext db) : IRequestHandler<GetScreensListQuery, IReadOnlyList<ScreenDto>>
{
    public async Task<IReadOnlyList<ScreenDto>> Handle(GetScreensListQuery request, CancellationToken cancellationToken) =>
        await db.Screens.AsNoTracking().OrderBy(s => s.ModuleCode).ThenBy(s => s.NameAr)
            .Select(s => new ScreenDto(s.Id, s.Code, s.NameAr, s.NameEn, s.ModuleCode))
            .ToListAsync(cancellationToken);
}

public sealed record GetWorkflowAssignmentsListQuery : IRequest<IReadOnlyList<ApprovalWorkflowAssignmentDto>>;

public sealed class GetWorkflowAssignmentsListQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<GetWorkflowAssignmentsListQuery, IReadOnlyList<ApprovalWorkflowAssignmentDto>>
{
    public async Task<IReadOnlyList<ApprovalWorkflowAssignmentDto>> Handle(GetWorkflowAssignmentsListQuery request, CancellationToken cancellationToken)
    {
        var companyId = currentCompanyContext.CompanyId;
        return await db.ApprovalWorkflowAssignments
            .AsNoTracking()
            .Where(a => a.CompanyId == companyId && a.IsActive)
            .Join(db.Screens, a => a.ScreenId, s => s.Id, (a, s) => new { a, s })
            .Join(db.ApprovalWorkflows, x => x.a.ApprovalWorkflowId, w => w.Id, (x, w) => new ApprovalWorkflowAssignmentDto(
                x.a.Id, x.s.Id, x.s.Code, x.s.NameAr, x.s.NameEn, w.Id, w.NameAr, w.NameEn, x.a.IsActive, x.a.MinAmount))
            .ToListAsync(cancellationToken);
    }
}
