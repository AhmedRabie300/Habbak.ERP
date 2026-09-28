using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Approvals;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Approvals.Commands;

// ---------------------------------------------------------------------------- create

public sealed record CreateApprovalWorkflowCommand(ApprovalWorkflowDefinition Definition) : IRequest<long>;

public sealed class CreateApprovalWorkflowCommandValidator : AbstractValidator<CreateApprovalWorkflowCommand>
{
    public CreateApprovalWorkflowCommandValidator() => RuleFor(x => x.Definition).SetValidator(new ApprovalWorkflowDefinitionValidator());
}

public sealed class CreateApprovalWorkflowCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<CreateApprovalWorkflowCommand, long>
{
    public async Task<long> Handle(CreateApprovalWorkflowCommand request, CancellationToken cancellationToken)
    {
        var companyId = currentCompanyContext.CompanyId;
        var codeExists = await db.ApprovalWorkflows.AnyAsync(
            w => w.CompanyId == companyId && w.Code == request.Definition.Code && w.IsCurrentVersion, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("APPROVAL-WORKFLOW-CODE-EXISTS", "يوجد سلسلة اعتماد بنفس الكود بالفعل.");
        }

        var workflow = new ApprovalWorkflow
        {
            CompanyId = companyId,
            Code = request.Definition.Code,
            FamilyId = Guid.NewGuid(),
            VersionNumber = 1,
            IsCurrentVersion = true,
            IsActive = true
        };
        ApprovalWorkflowSteps.Apply(workflow, request.Definition);

        db.ApprovalWorkflows.Add(workflow);
        await db.SaveChangesAsync(cancellationToken);

        return workflow.Id;
    }
}

internal static class ApprovalWorkflowSteps
{
    public static void Apply(ApprovalWorkflow workflow, ApprovalWorkflowDefinition definition)
    {
        workflow.NameAr = definition.NameAr;
        workflow.NameEn = definition.NameEn;

        foreach (var stepInput in definition.Steps)
        {
            var step = new ApprovalWorkflowStep { StepOrder = stepInput.StepOrder, Mode = stepInput.Mode };
            foreach (var approverInput in stepInput.Approvers)
            {
                step.Approvers.Add(new ApprovalStepApprover
                {
                    ApproverType = approverInput.ApproverType,
                    ApproverReferenceId = approverInput.ApproverReferenceId
                });
            }
            workflow.Steps.Add(step);
        }
    }
}

// ---------------------------------------------------------------------------- update

public sealed record UpdateApprovalWorkflowResult(long Id, int VersionNumber, bool IsNewVersion);

/// <summary>
/// Edits in place while the workflow has never started an ApprovalInstance, and creates the next
/// version once it has — same rule PostingTemplate uses (spec §3.1): a historical instance stays
/// explainable by the rules that actually governed it.
/// </summary>
public sealed record UpdateApprovalWorkflowCommand(long Id, ApprovalWorkflowDefinition Definition) : IRequest<UpdateApprovalWorkflowResult>;

public sealed class UpdateApprovalWorkflowCommandValidator : AbstractValidator<UpdateApprovalWorkflowCommand>
{
    public UpdateApprovalWorkflowCommandValidator() => RuleFor(x => x.Definition).SetValidator(new ApprovalWorkflowDefinitionValidator());
}

public sealed class UpdateApprovalWorkflowCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateApprovalWorkflowCommand, UpdateApprovalWorkflowResult>
{
    public async Task<UpdateApprovalWorkflowResult> Handle(UpdateApprovalWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = await db.ApprovalWorkflows
            .Include(w => w.Steps).ThenInclude(s => s.Approvers)
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ApprovalWorkflow), request.Id);

        if (!workflow.IsCurrentVersion)
        {
            throw new BusinessRuleException("APPROVAL-WORKFLOW-NOT-CURRENT", "ده إصدار قديم من السلسلة — التعديل بيكون على الإصدار الحالي بس.");
        }

        var hasRun = await db.ApprovalInstances.AnyAsync(i => i.ApprovalWorkflowId == workflow.Id, cancellationToken);

        // Two saves, same reason as UpdatePostingTemplateCommand: the filtered unique index (one
        // current version per family) means the old row must be retired before its replacement
        // exists, and one SaveChanges does not promise that order.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        ApprovalWorkflow result;
        if (hasRun)
        {
            workflow.IsCurrentVersion = false;
            await db.SaveChangesAsync(cancellationToken);

            result = new ApprovalWorkflow
            {
                CompanyId = workflow.CompanyId,
                Code = workflow.Code,
                FamilyId = workflow.FamilyId,
                VersionNumber = workflow.VersionNumber + 1,
                PreviousVersionId = workflow.Id,
                IsCurrentVersion = true,
                IsActive = workflow.IsActive
            };
            db.ApprovalWorkflows.Add(result);
        }
        else
        {
            foreach (var step in workflow.Steps.ToList())
            {
                foreach (var approver in step.Approvers.ToList())
                {
                    db.ApprovalStepApprovers.Remove(approver);
                }
                db.ApprovalWorkflowSteps.Remove(step);
            }
            await db.SaveChangesAsync(cancellationToken);

            result = workflow;
        }

        ApprovalWorkflowSteps.Apply(result, request.Definition);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new UpdateApprovalWorkflowResult(result.Id, result.VersionNumber, hasRun);
    }
}

// ---------------------------------------------------------------------------- activate / deactivate

/// <summary>
/// Docs/Implementation/Phase-2-Research.md — consolidates the plan's separate Activate/Deactivate
/// commands into one, same as PostingTemplate's SetPostingTemplateActiveCommand.
/// </summary>
public sealed record SetApprovalWorkflowActiveCommand(long Id, bool IsActive) : IRequest;

public sealed class SetApprovalWorkflowActiveCommandHandler(IApplicationDbContext db) : IRequestHandler<SetApprovalWorkflowActiveCommand>
{
    public async Task Handle(SetApprovalWorkflowActiveCommand request, CancellationToken cancellationToken)
    {
        var workflow = await db.ApprovalWorkflows.FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ApprovalWorkflow), request.Id);

        if (!workflow.IsCurrentVersion)
        {
            throw new BusinessRuleException("APPROVAL-WORKFLOW-NOT-CURRENT", "التفعيل والتعطيل بيكون على الإصدار الحالي بس.");
        }

        workflow.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}
