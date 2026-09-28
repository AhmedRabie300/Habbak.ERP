using FluentValidation;
using Habbak.ERP.Domain.Approvals;

namespace Habbak.ERP.Application.Approvals;

public sealed record ApprovalStepApproverInput(ApprovalApproverType ApproverType, long? ApproverReferenceId);

public sealed record ApprovalWorkflowStepInput(int StepOrder, ApprovalStepMode Mode, IReadOnlyList<ApprovalStepApproverInput> Approvers);

/// <summary>
/// The editable body of a workflow — every step and approver rebuilt atomically on Create/Update,
/// the same shape PostingTemplateDefinition uses for a template's lines (Docs/Implementation/
/// Phase-2-Research.md's plan listed separate CRUD commands per step/approver, but a workflow is
/// edited as one document from a single settings screen, never one step at a time — the granular
/// commands would just be a fragile partial-edit API for something the whole-document approach
/// already covers atomically, so this phase consolidates them the same way PostingTemplate did).
/// </summary>
public sealed record ApprovalWorkflowDefinition(string Code, string NameAr, string NameEn, IReadOnlyList<ApprovalWorkflowStepInput> Steps);

public sealed class ApprovalWorkflowDefinitionValidator : AbstractValidator<ApprovalWorkflowDefinition>
{
    public ApprovalWorkflowDefinitionValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Steps).NotEmpty().WithMessage("السلسلة لازم تحتوي على خطوة واحدة على الأقل.");

        RuleForEach(x => x.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.StepOrder).GreaterThan(0);
            step.RuleFor(s => s.Approvers).NotEmpty().WithMessage("كل خطوة لازم يكون لها معتمد واحد على الأقل.");

            step.RuleForEach(s => s.Approvers).ChildRules(approver =>
            {
                approver.RuleFor(a => a.ApproverReferenceId)
                    .NotNull().GreaterThan(0)
                    .When(a => a.ApproverType != ApprovalApproverType.DirectManager)
                    .WithMessage("لازم تحدد الموظف/الدور/الدرجة الوظيفية لهذا المعتمد.");

                approver.RuleFor(a => a.ApproverReferenceId)
                    .Null()
                    .When(a => a.ApproverType == ApprovalApproverType.DirectManager)
                    .WithMessage("المدير المباشر بيتحدد وقت التنفيذ، مش وقت تصميم السلسلة.");
            });
        });

        RuleFor(x => x.Steps)
            .Must(steps => steps.Select(s => s.StepOrder).Distinct().Count() == steps.Count)
            .WithMessage("ترتيب الخطوات (StepOrder) لازم يكون فريد.");
    }
}
