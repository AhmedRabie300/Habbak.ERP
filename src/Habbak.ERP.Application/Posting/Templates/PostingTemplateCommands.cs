using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting.Resolvers;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Posting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Posting.Templates;

// ---------------------------------------------------------------------------- create

/// <summary>
/// Adds a template to a screen. A screen may carry several (design notes, note 1) — a revenue
/// template and a cost-of-sales one, a cash-sale and a credit-sale one — each with its own trigger.
/// </summary>
public sealed record CreatePostingTemplateCommand(
    string ScreenCode,
    PostingTemplateDefinition Definition,
    bool IsActive = true) : IRequest<long>;

public sealed class CreatePostingTemplateCommandValidator : AbstractValidator<CreatePostingTemplateCommand>
{
    public CreatePostingTemplateCommandValidator(IPostingResolverRegistry resolvers)
    {
        RuleFor(x => x.ScreenCode).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Definition).SetValidator(new PostingTemplateDefinitionValidator(resolvers));
    }
}

public sealed class CreatePostingTemplateCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, IPostingResolverRegistry resolvers)
    : IRequestHandler<CreatePostingTemplateCommand, long>
{
    public Task<long> Handle(CreatePostingTemplateCommand request, CancellationToken cancellationToken) =>
        PostingTemplateCreation.CreateAsync(
            db, resolvers, currentCompanyContext.CompanyId, request.ScreenCode, request.Definition, request.IsActive,
            isSystemTemplate: false, cancellationToken);
}

/// <summary>
/// Adds the screen's standard templates from PostingScreenCatalog — built only from company roles
/// and resolvers, so they fit any chart of accounts once the roles are mapped. A screen whose
/// standard is two entries (sales and cost of sales) gets both.
///
/// Created switched off: the finance manager reviews them (and adds cost centers) before the screen
/// starts posting with them. Refused once the screen has any template, so pressing it twice cannot
/// double every entry.
/// </summary>
public sealed record CreateDefaultPostingTemplatesCommand(string ScreenCode) : IRequest<IReadOnlyList<long>>;

public sealed class CreateDefaultPostingTemplatesCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, IPostingResolverRegistry resolvers)
    : IRequestHandler<CreateDefaultPostingTemplatesCommand, IReadOnlyList<long>>
{
    public async Task<IReadOnlyList<long>> Handle(CreateDefaultPostingTemplatesCommand request, CancellationToken cancellationToken)
    {
        var screen = PostingScreenCatalog.Find(request.ScreenCode)
            ?? throw new BusinessRuleException("POST-TEMPLATE-UNKNOWN-SCREEN", $"الشاشة '{request.ScreenCode}' مش من الشاشات اللي بترحّل قيود.");

        if (screen.DefaultTemplates.Count == 0)
        {
            throw new BusinessRuleException("POST-TEMPLATE-NO-DEFAULT", $"مفيش قالب قياسي لشاشة {screen.NameAr} — اعمله من المحرر.");
        }

        if (await db.PostingTemplates.AnyAsync(t => t.ScreenCode == screen.ScreenCode && t.IsCurrentVersion, cancellationToken))
        {
            throw new BusinessRuleException(
                "POST-TEMPLATE-DEFAULTS-EXIST", $"شاشة {screen.NameAr} عندها قوالب بالفعل — القوالب القياسية بتتضاف لشاشة فاضية بس.");
        }

        var ids = new List<long>();
        foreach (var definition in screen.DefaultTemplates)
        {
            ids.Add(await PostingTemplateCreation.CreateAsync(
                db, resolvers, currentCompanyContext.CompanyId, screen.ScreenCode, definition,
                isActive: false, isSystemTemplate: true, cancellationToken));
        }

        return ids;
    }
}

internal static class PostingTemplateCreation
{
    public static async Task<long> CreateAsync(
        IApplicationDbContext db, IPostingResolverRegistry resolvers, long companyId, string screenCode,
        PostingTemplateDefinition definition, bool isActive, bool isSystemTemplate, CancellationToken cancellationToken)
    {
        var screen = PostingTemplateDefinitionChecks.CheckAgainstScreen(resolvers, screenCode, definition);
        await PostingTemplateDefinitionChecks.CheckAsync(db, resolvers, screen, definition, cancellationToken);

        var template = new PostingTemplate
        {
            CompanyId = companyId,
            ScreenCode = screen.ScreenCode,
            FamilyId = Guid.NewGuid(),
            VersionNumber = 1,
            IsCurrentVersion = true,
            IsActive = isActive,
            IsSystemTemplate = isSystemTemplate
        };
        Apply(template, definition);

        db.PostingTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);

        return template.Id;
    }

    public static void Apply(PostingTemplate template, PostingTemplateDefinition definition)
    {
        template.NameAr = definition.NameAr;
        template.NameEn = definition.NameEn;
        template.Description = definition.Description;
        template.TriggerType = definition.TriggerType;
        template.TriggerFieldName = definition.TriggerType == PostingTriggerType.FieldCondition ? definition.TriggerFieldName : null;
        template.TriggerFieldValue = definition.TriggerType == PostingTriggerType.FieldCondition ? definition.TriggerFieldValue : null;
        template.ExecutionOrder = definition.ExecutionOrder;

        foreach (var line in PostingTemplateLineFactory.Build(definition.Lines))
        {
            template.Lines.Add(line);
        }
    }
}

// ---------------------------------------------------------------------------- update

public sealed record UpdatePostingTemplateResult(long Id, int VersionNumber, bool IsNewVersion);

/// <summary>
/// Edits in place while the template has never posted anything, and creates the next version once
/// it has (spec 3.1, same rule as Recipe's rule 31). An entry is always explained by the version
/// that built it, so a template that has produced entries is never changed underneath them.
/// </summary>
public sealed record UpdatePostingTemplateCommand(long Id, PostingTemplateDefinition Definition) : IRequest<UpdatePostingTemplateResult>;

public sealed class UpdatePostingTemplateCommandValidator : AbstractValidator<UpdatePostingTemplateCommand>
{
    public UpdatePostingTemplateCommandValidator(IPostingResolverRegistry resolvers)
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Definition).SetValidator(new PostingTemplateDefinitionValidator(resolvers));
    }
}

public sealed class UpdatePostingTemplateCommandHandler(IApplicationDbContext db, IPostingResolverRegistry resolvers)
    : IRequestHandler<UpdatePostingTemplateCommand, UpdatePostingTemplateResult>
{
    public async Task<UpdatePostingTemplateResult> Handle(UpdatePostingTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await db.PostingTemplates
            .Include(t => t.Lines).ThenInclude(l => l.CostCenters)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PostingTemplate), request.Id);

        if (!template.IsCurrentVersion)
        {
            throw new BusinessRuleException(
                "POST-TEMPLATE-NOT-CURRENT",
                "ده إصدار قديم من القالب — التعديل بيكون على الإصدار الحالي بس.");
        }

        var screen = PostingTemplateDefinitionChecks.CheckAgainstScreen(resolvers, template.ScreenCode, request.Definition);
        await PostingTemplateDefinitionChecks.CheckAsync(db, resolvers, screen, request.Definition, cancellationToken);

        var hasPosted = await db.JournalEntryTemplateSnapshots.AnyAsync(s => s.PostingTemplateId == template.Id, cancellationToken);

        // Two saves in one transaction, in both branches. Unique filtered indexes (one current
        // version per template; one live line per number) mean the old rows must be retired before
        // their replacements exist, and a single SaveChanges does not promise that order.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        PostingTemplate result;
        if (hasPosted)
        {
            template.IsCurrentVersion = false;
            await db.SaveChangesAsync(cancellationToken);

            result = new PostingTemplate
            {
                CompanyId = template.CompanyId,
                ScreenCode = template.ScreenCode,
                FamilyId = template.FamilyId,
                VersionNumber = template.VersionNumber + 1,
                PreviousVersionId = template.Id,
                IsCurrentVersion = true,
                IsActive = template.IsActive,
                IsSystemTemplate = template.IsSystemTemplate
            };
            db.PostingTemplates.Add(result);
        }
        else
        {
            foreach (var line in template.Lines.ToList())
            {
                foreach (var costCenter in line.CostCenters.ToList())
                {
                    db.PostingTemplateLineCostCenters.Remove(costCenter);
                }
                db.PostingTemplateLines.Remove(line);
            }
            await db.SaveChangesAsync(cancellationToken);

            result = template;
        }

        PostingTemplateCreation.Apply(result, request.Definition);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new UpdatePostingTemplateResult(result.Id, result.VersionNumber, hasPosted);
    }
}

// ---------------------------------------------------------------------------- activate / deactivate

/// <summary>
/// There is no delete (spec rule 9): a template that has produced entries must stay readable, and
/// switching it off stops new postings just as well.
/// </summary>
public sealed record SetPostingTemplateActiveCommand(long Id, bool IsActive) : IRequest;

public sealed class SetPostingTemplateActiveCommandHandler(IApplicationDbContext db) : IRequestHandler<SetPostingTemplateActiveCommand>
{
    public async Task Handle(SetPostingTemplateActiveCommand request, CancellationToken cancellationToken)
    {
        var template = await db.PostingTemplates.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PostingTemplate), request.Id);

        if (!template.IsCurrentVersion)
        {
            throw new BusinessRuleException("POST-TEMPLATE-NOT-CURRENT", "التفعيل والتعطيل بيكون على الإصدار الحالي بس.");
        }

        template.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// "الشاشة بترحّل قيود؟" for the whole screen: switches every current template of it on or off at
/// once. Switching on with no template is refused — a screen cannot post with nothing to post by.
/// </summary>
public sealed record SetPostingScreenActiveCommand(string ScreenCode, bool IsActive) : IRequest;

public sealed class SetPostingScreenActiveCommandHandler(IApplicationDbContext db) : IRequestHandler<SetPostingScreenActiveCommand>
{
    public async Task Handle(SetPostingScreenActiveCommand request, CancellationToken cancellationToken)
    {
        var templates = await db.PostingTemplates
            .Where(t => t.ScreenCode == request.ScreenCode && t.IsCurrentVersion)
            .ToListAsync(cancellationToken);

        if (request.IsActive && templates.Count == 0)
        {
            throw new BusinessRuleException(
                "POST-SCREEN-NO-TEMPLATES", "الشاشة مالهاش قوالب — ضيف قالب (أو ابدأ من القالب القياسي) الأول.");
        }

        foreach (var template in templates)
        {
            template.IsActive = request.IsActive;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
