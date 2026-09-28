using Habbak.ERP.Application.Approvals.Dtos;
using Habbak.ERP.Application.Approvals.Services;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Approvals;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Approvals.Queries;

/// <summary>"بانتظار اعتمادي" (00-Frontend-Specs.md §19) — every Pending instance whose current step the calling user is actually eligible to act on right now.</summary>
public sealed record GetMyPendingApprovalsQuery : IRequest<IReadOnlyList<PendingApprovalDto>>;

public sealed class GetMyPendingApprovalsQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, IApprovalStepResolutionService resolution)
    : IRequestHandler<GetMyPendingApprovalsQuery, IReadOnlyList<PendingApprovalDto>>
{
    public async Task<IReadOnlyList<PendingApprovalDto>> Handle(GetMyPendingApprovalsQuery request, CancellationToken cancellationToken)
    {
        var companyId = currentCompanyContext.CompanyId;
        var userId = currentCompanyContext.UserId;

        var pending = await db.ApprovalInstances
            .AsNoTracking()
            .Where(i => i.CompanyId == companyId && i.Status == ApprovalInstanceStatus.Pending)
            .Include(i => i.ApprovalWorkflow)
            .ToListAsync(cancellationToken);

        var result = new List<PendingApprovalDto>();
        foreach (var instance in pending)
        {
            if (instance.RequestedByUserId == userId)
            {
                continue; // §12.4 rule 7 — never offered your own request to act on
            }

            var step = await db.ApprovalWorkflowSteps.FirstOrDefaultAsync(
                s => s.ApprovalWorkflowId == instance.ApprovalWorkflowId && s.StepOrder == instance.CurrentStepOrder, cancellationToken);
            if (step is null)
            {
                continue;
            }

            var eligible = await resolution.GetEligibleApproverUserIdsAsync(instance, step, cancellationToken);
            if (!eligible.Contains(userId))
            {
                continue;
            }

            var totalSteps = await db.ApprovalWorkflowSteps.CountAsync(s => s.ApprovalWorkflowId == instance.ApprovalWorkflowId, cancellationToken);
            result.Add(new PendingApprovalDto(
                instance.Id, instance.EntityType, instance.EntityId,
                instance.ApprovalWorkflow?.NameAr ?? "", instance.ApprovalWorkflow?.NameEn ?? "",
                instance.RequestedByUserId, instance.RequestedAtUtc, instance.Amount, instance.CurrentStepOrder, totalSteps));
        }

        return result.OrderBy(r => r.RequestedAtUtc).ToList();
    }
}

public sealed record GetApprovalInstanceByIdQuery(long Id) : IRequest<ApprovalInstanceDto>;

public sealed class GetApprovalInstanceByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetApprovalInstanceByIdQuery, ApprovalInstanceDto>
{
    public async Task<ApprovalInstanceDto> Handle(GetApprovalInstanceByIdQuery request, CancellationToken cancellationToken)
    {
        var instance = await db.ApprovalInstances
            .AsNoTracking()
            .Include(i => i.ApprovalWorkflow)
            .Include(i => i.Actions)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ApprovalInstance), request.Id);

        return ToDto(instance);
    }

    private static ApprovalInstanceDto ToDto(ApprovalInstance instance) => new(
        instance.Id, instance.EntityType, instance.EntityId, instance.ApprovalWorkflowId,
        instance.ApprovalWorkflow?.NameAr ?? "", instance.ApprovalWorkflow?.NameEn ?? "", instance.WorkflowVersionNumber,
        instance.RequestedByUserId, instance.RequestedAtUtc, instance.Amount, instance.CurrentStepOrder,
        instance.ApprovalWorkflow?.Steps.Count ?? 0, instance.Status,
        instance.Actions.OrderBy(a => a.ActionAtUtc)
            .Select(a => new ApprovalActionDto(a.Id, a.StepOrder, a.ActionByUserId, a.ActionType, a.Reason, a.ReassignedToUserId, a.ActionAtUtc))
            .ToList());
}

/// <summary>The full approval trail of one document — usually a single instance (§12.4 rule 2: a rejected instance is never resumed, a resubmission is a brand-new document with its own instance).</summary>
public sealed record GetApprovalHistoryQuery(string EntityType, long EntityId) : IRequest<IReadOnlyList<ApprovalInstanceDto>>;

public sealed class GetApprovalHistoryQueryHandler(IApplicationDbContext db) : IRequestHandler<GetApprovalHistoryQuery, IReadOnlyList<ApprovalInstanceDto>>
{
    public async Task<IReadOnlyList<ApprovalInstanceDto>> Handle(GetApprovalHistoryQuery request, CancellationToken cancellationToken)
    {
        var instances = await db.ApprovalInstances
            .AsNoTracking()
            .Include(i => i.ApprovalWorkflow).ThenInclude(w => w!.Steps)
            .Include(i => i.Actions)
            .Where(i => i.EntityType == request.EntityType && i.EntityId == request.EntityId)
            .OrderBy(i => i.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return instances.Select(instance => new ApprovalInstanceDto(
            instance.Id, instance.EntityType, instance.EntityId, instance.ApprovalWorkflowId,
            instance.ApprovalWorkflow?.NameAr ?? "", instance.ApprovalWorkflow?.NameEn ?? "", instance.WorkflowVersionNumber,
            instance.RequestedByUserId, instance.RequestedAtUtc, instance.Amount, instance.CurrentStepOrder,
            instance.ApprovalWorkflow?.Steps.Count ?? 0, instance.Status,
            instance.Actions.OrderBy(a => a.ActionAtUtc)
                .Select(a => new ApprovalActionDto(a.Id, a.StepOrder, a.ActionByUserId, a.ActionType, a.Reason, a.ReassignedToUserId, a.ActionAtUtc))
                .ToList())).ToList();
    }
}
