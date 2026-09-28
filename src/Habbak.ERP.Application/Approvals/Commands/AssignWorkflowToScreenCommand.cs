using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Approvals;
using Habbak.ERP.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Approvals.Commands;

/// <summary>
/// §12.2 — links a Screen to the workflow that governs it. Deactivates any prior active assignment
/// for the same screen first (the filtered unique index on (CompanyId, ScreenId) WHERE IsActive
/// only allows one), so re-assigning a screen to a different workflow is a normal edit, not a
/// conflict.
/// </summary>
public sealed record AssignWorkflowToScreenCommand(long ScreenId, long ApprovalWorkflowId, decimal? MinAmount) : IRequest<long>;

public sealed class AssignWorkflowToScreenCommandValidator : AbstractValidator<AssignWorkflowToScreenCommand>
{
    public AssignWorkflowToScreenCommandValidator()
    {
        RuleFor(x => x.ScreenId).GreaterThan(0);
        RuleFor(x => x.ApprovalWorkflowId).GreaterThan(0);
        RuleFor(x => x.MinAmount).GreaterThanOrEqualTo(0).When(x => x.MinAmount.HasValue);
    }
}

public sealed class AssignWorkflowToScreenCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<AssignWorkflowToScreenCommand, long>
{
    public async Task<long> Handle(AssignWorkflowToScreenCommand request, CancellationToken cancellationToken)
    {
        var companyId = currentCompanyContext.CompanyId;

        _ = await db.Screens.FirstOrDefaultAsync(s => s.Id == request.ScreenId, cancellationToken)
            ?? throw new NotFoundException(nameof(Screen), request.ScreenId);

        var workflow = await db.ApprovalWorkflows.FirstOrDefaultAsync(
            w => w.Id == request.ApprovalWorkflowId && w.CompanyId == companyId && w.IsCurrentVersion, cancellationToken)
            ?? throw new NotFoundException(nameof(ApprovalWorkflow), request.ApprovalWorkflowId);

        if (!workflow.IsActive)
        {
            throw new BusinessRuleException("APPROVAL-WORKFLOW-INACTIVE", "لا يمكن ربط سلسلة معطّلة بشاشة.");
        }

        var existing = await db.ApprovalWorkflowAssignments.FirstOrDefaultAsync(
            a => a.CompanyId == companyId && a.ScreenId == request.ScreenId && a.IsActive, cancellationToken);
        if (existing is not null)
        {
            existing.IsActive = false;
        }

        var assignment = new ApprovalWorkflowAssignment
        {
            CompanyId = companyId,
            ScreenId = request.ScreenId,
            ApprovalWorkflowId = request.ApprovalWorkflowId,
            IsActive = true,
            MinAmount = request.MinAmount
        };
        db.ApprovalWorkflowAssignments.Add(assignment);

        await db.SaveChangesAsync(cancellationToken);
        return assignment.Id;
    }
}

/// <summary>Unassigns a screen entirely — it falls back to plain direct approval (§12.4 rule 4).</summary>
public sealed record UnassignWorkflowFromScreenCommand(long ScreenId) : IRequest;

public sealed class UnassignWorkflowFromScreenCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UnassignWorkflowFromScreenCommand>
{
    public async Task Handle(UnassignWorkflowFromScreenCommand request, CancellationToken cancellationToken)
    {
        var companyId = currentCompanyContext.CompanyId;
        var existing = await db.ApprovalWorkflowAssignments.FirstOrDefaultAsync(
            a => a.CompanyId == companyId && a.ScreenId == request.ScreenId && a.IsActive, cancellationToken);

        if (existing is not null)
        {
            existing.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
