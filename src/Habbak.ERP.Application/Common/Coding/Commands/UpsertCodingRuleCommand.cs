using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Common.Coding.Commands;

/// <summary>PUT /api/v1/settings/coding-rules — the settings screen's save action. LastSequence is
/// never touched here (only ICodeGenerator advances it) so reconfiguring a rule never rewinds or
/// duplicates already-issued codes.</summary>
public sealed record UpsertCodingRuleCommand(
    string ScreenCode, bool IsAutomatic, CodeFormat Format, string? Prefix, int SequenceLength,
    bool IsAttachmentMandatory, bool IsDescriptionMandatory) : IRequest;

public sealed class UpsertCodingRuleCommandValidator : AbstractValidator<UpsertCodingRuleCommand>
{
    public UpsertCodingRuleCommandValidator()
    {
        RuleFor(x => x.ScreenCode).NotEmpty()
            .Must(code => ScreenCodeCatalog.Find(code) is not null)
            .WithMessage("شاشة غير معروفة.");
        RuleFor(x => x.SequenceLength).InclusiveBetween(1, 10);
        RuleFor(x => x.Prefix).NotEmpty().MaximumLength(20)
            .When(x => x.IsAutomatic && x.Format != CodeFormat.NumbersOnly)
            .WithMessage("الحروف اللي يبدأ بيها التكويد إلزامية لهذا التنسيق.");
    }
}

public sealed class UpsertCodingRuleCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpsertCodingRuleCommand>
{
    public async Task Handle(UpsertCodingRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.CodingRules.FirstOrDefaultAsync(
            r => r.ScreenCode == request.ScreenCode && r.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        if (rule is null)
        {
            var def = ScreenCodeCatalog.Find(request.ScreenCode)
                ?? throw new BusinessRuleException("CODING-RULE-UNKNOWN-SCREEN", "شاشة غير معروفة.");

            rule = new CodingRule { CompanyId = currentCompanyContext.CompanyId, ScreenCode = request.ScreenCode, LastSequence = 0 };
            _ = def; // catalog entry only needed to validate existence above
            db.CodingRules.Add(rule);
        }

        rule.IsAutomatic = request.IsAutomatic;
        rule.Format = request.Format;
        rule.Prefix = request.Prefix;
        rule.SequenceLength = request.SequenceLength;
        rule.IsAttachmentMandatory = request.IsAttachmentMandatory;
        rule.IsDescriptionMandatory = request.IsDescriptionMandatory;

        await db.SaveChangesAsync(cancellationToken);
    }
}
