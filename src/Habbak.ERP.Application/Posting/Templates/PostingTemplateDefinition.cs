using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting.Resolvers;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Posting;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Posting.Templates;

public sealed record PostingTemplateLineCostCenterInput(
    long CostCenterDimensionId,
    CostCenterSourceType SourceType,
    long? FixedValueId,
    string? ValueFieldName,
    string? ValueResolverKey,
    int DisplayOrder,
    string? RelatedEntityType = null,
    string? RelatedEntityField = null,
    string? ContextKey = null);

public sealed record PostingTemplateLineInput(
    int LineNumber,
    PostingDirection Direction,
    AccountSourceType AccountSourceType,
    long? FixedAccountId,
    string? AccountFieldName,
    string? AccountResolverKey,
    AmountFormulaType AmountFormulaType,
    string? AmountFieldName,
    ConditionType ConditionType,
    string? ConditionFieldName,
    string? ConditionFieldValue,
    string? LineDescription,
    IReadOnlyList<PostingTemplateLineCostCenterInput> CostCenters,
    IReadOnlyList<string>? AmountFieldNames = null,
    decimal? AmountMultiplier = null,
    decimal? AmountPercentage = null);

/// <summary>
/// The editable body of a template — including when it runs (its trigger) and in what order among
/// the screen's other templates. Its screen is fixed at creation.
/// </summary>
public sealed record PostingTemplateDefinition(
    string NameAr,
    string NameEn,
    string? Description,
    IReadOnlyList<PostingTemplateLineInput> Lines,
    PostingTriggerType TriggerType = PostingTriggerType.Always,
    string? TriggerFieldName = null,
    string? TriggerFieldValue = null,
    int ExecutionOrder = 1);

/// <summary>
/// Everything that can be checked without the database. The point of checking at save time
/// (spec 4.4) is that a mistake in a template surfaces while someone is editing it — not on the
/// first real invoice, when money is already moving and the person who made it is long gone.
/// </summary>
public sealed class PostingTemplateDefinitionValidator : AbstractValidator<PostingTemplateDefinition>
{
    /// <summary>01-Module-Accounting.md rule 24: at most five dimensions on any one account.</summary>
    public const int MaxCostCentersPerLine = 5;

    /// <summary>Design notes, open question 1: at most five fields in one sum.</summary>
    public const int MaxAmountFields = 5;

    public PostingTemplateDefinitionValidator(IPostingResolverRegistry resolvers)
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.TriggerType).IsInEnum();
        RuleFor(x => x.ExecutionOrder).InclusiveBetween(1, 99);
        RuleFor(x => x.TriggerFieldName).NotEmpty().When(x => x.TriggerType == PostingTriggerType.FieldCondition)
            .WithMessage("شرط التشغيل محتاج الحقل.");
        RuleFor(x => x.TriggerFieldValue).NotEmpty().When(x => x.TriggerType == PostingTriggerType.FieldCondition)
            .WithMessage("شرط التشغيل محتاج القيمة.");

        RuleFor(x => x.Lines).NotEmpty();
        RuleFor(x => x.Lines)
            .Must(ls => ls.Any(l => l.Direction == PostingDirection.Debit) && ls.Any(l => l.Direction == PostingDirection.Credit))
            .WithMessage("القالب لازم يكون فيه سطر مدين واحد وسطر دائن واحد على الأقل.");
        RuleFor(x => x.Lines)
            .Must(ls => ls.Select(l => l.LineNumber).Distinct().Count() == ls.Count)
            .WithMessage("أرقام السطور لازم تكون مختلفة.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.LineNumber).GreaterThan(0);
            line.RuleFor(l => l.Direction).IsInEnum();
            line.RuleFor(l => l.AccountSourceType).IsInEnum();
            line.RuleFor(l => l.AmountFormulaType).IsInEnum();
            line.RuleFor(l => l.ConditionType).IsInEnum();

            line.RuleFor(l => l.FixedAccountId).NotNull().When(l => l.AccountSourceType == AccountSourceType.Fixed)
                .WithMessage("السطر محتاج حساب ثابت.");
            line.RuleFor(l => l.AccountFieldName).NotEmpty().When(l => l.AccountSourceType == AccountSourceType.FromDocument)
                .WithMessage("السطر محتاج اسم الحقل اللي فيه رقم الحساب.");
            // Exact name match, not Enum.TryParse: TryParse also accepts "999", which would save
            // fine and only fail on the first real posting — the very thing saving is meant to catch.
            line.RuleFor(l => l.AccountResolverKey)
                .Must(k => k is not null && Enum.GetNames<CompanyAccountRole>().Contains(k))
                .When(l => l.AccountSourceType == AccountSourceType.FromCompany)
                .WithMessage(l => $"'{l.AccountResolverKey}' مش دور محاسبي معروف.");
            line.RuleFor(l => l.AccountResolverKey)
                .Must(k => k is not null && resolvers.FindAccountResolver(k) is not null)
                .When(l => l.AccountSourceType == AccountSourceType.Resolver)
                .WithMessage(l => $"الـResolver '{l.AccountResolverKey}' مش مسجّل.");

            line.RuleFor(l => l.AmountFieldName).NotEmpty()
                .When(l => l.AmountFormulaType is AmountFormulaType.DirectField or AmountFormulaType.Multiply or AmountFormulaType.PercentageOf)
                .WithMessage("الصيغة دي محتاجة الحقل.");
            line.RuleFor(l => l.AmountMultiplier).NotNull().When(l => l.AmountFormulaType == AmountFormulaType.Multiply)
                .WithMessage("صيغة (ضرب) محتاجة المعامل.");
            line.RuleFor(l => l.AmountPercentage).NotNull().GreaterThan(0).LessThanOrEqualTo(1000)
                .When(l => l.AmountFormulaType == AmountFormulaType.PercentageOf)
                .WithMessage("صيغة (نسبة من) محتاجة نسبة أكبر من صفر.");
            line.RuleFor(l => l.AmountFieldNames)
                .Must(f => f is not null && f.Count >= 2 && f.Count <= MaxAmountFields)
                .When(l => l.AmountFormulaType == AmountFormulaType.AddFields)
                .WithMessage($"الجمع محتاج من 2 لـ{MaxAmountFields} حقول.");
            line.RuleFor(l => l.AmountFieldNames)
                .Must(f => f is not null && f.Count == 2)
                .When(l => l.AmountFormulaType is AmountFormulaType.SubtractFields or AmountFormulaType.DivideFields)
                .WithMessage("الطرح والقسمة بين حقلين بالظبط.");

            // A group line takes both its accounts and its amounts from the group, so the two only
            // ever come together; the group's name rides in AmountFieldName.
            line.RuleFor(l => l)
                .Must(l => (l.AccountSourceType == AccountSourceType.FromGroup) == (l.AmountFormulaType == AmountFormulaType.GroupItemAmount))
                .WithMessage("سطر (حسب المجموعة) لازم الحساب والمبلغ الاتنين يكونوا من المجموعة.");
            line.RuleFor(l => l.AmountFieldName).NotEmpty().When(l => l.AmountFormulaType == AmountFormulaType.GroupItemAmount)
                .WithMessage("سطر المجموعة محتاج اسم المجموعة.");

            line.RuleFor(l => l.ConditionFieldName).NotEmpty().When(l => l.ConditionType != ConditionType.None)
                .WithMessage("الشرط محتاج اسم الحقل.");
            line.RuleFor(l => l.ConditionFieldValue).NotEmpty().When(l => l.ConditionType == ConditionType.FieldEquals)
                .WithMessage("شرط (يساوي) محتاج القيمة.");

            line.RuleFor(l => l.CostCenters).Must(cs => cs.Count <= MaxCostCentersPerLine)
                .WithMessage($"الحد الأقصى {MaxCostCentersPerLine} أبعاد للسطر الواحد.");
            line.RuleFor(l => l.CostCenters).Must(cs => cs.Select(c => c.CostCenterDimensionId).Distinct().Count() == cs.Count)
                .WithMessage("نفس البُعد متكرر على السطر.");

            line.RuleForEach(l => l.CostCenters).ChildRules(cc =>
            {
                cc.RuleFor(c => c.SourceType).IsInEnum();
                cc.RuleFor(c => c.FixedValueId).NotNull().When(c => c.SourceType == CostCenterSourceType.Fixed)
                    .WithMessage("البُعد محتاج قيمة ثابتة.");
                cc.RuleFor(c => c.ValueFieldName).NotEmpty().When(c => c.SourceType == CostCenterSourceType.FromDocument)
                    .WithMessage("البُعد محتاج اسم الحقل اللي فيه القيمة.");
                cc.RuleFor(c => c.ContextKey).NotEmpty().When(c => c.SourceType == CostCenterSourceType.FromContext)
                    .WithMessage("البُعد محتاج مفتاح القيمة.");
                cc.RuleFor(c => c.RelatedEntityType).NotEmpty().When(c => c.SourceType == CostCenterSourceType.FromRelatedEntity)
                    .WithMessage("البُعد محتاج الكيان المرتبط.");
                cc.RuleFor(c => c.RelatedEntityField).NotEmpty().When(c => c.SourceType == CostCenterSourceType.FromRelatedEntity)
                    .WithMessage("البُعد محتاج حقل الكيان المرتبط.");
                cc.RuleFor(c => c.ValueResolverKey)
                    .Must(k => k is not null && resolvers.FindCostCenterResolver(k) is not null)
                    .When(c => c.SourceType == CostCenterSourceType.Dynamic)
                    .WithMessage(c => $"الـResolver '{c.ValueResolverKey}' مش مسجّل.");
            });
        });
    }
}

/// <summary>The checks that need the screen catalog or the database, run by the create and update handlers.</summary>
internal static class PostingTemplateDefinitionChecks
{
    /// <summary>
    /// Every name a template uses must be one the screen actually supplies (the catalog is also what
    /// the documents build their context from). A misspelt field would otherwise save cleanly and
    /// only fail on the first real document.
    /// </summary>
    public static PostingScreenDefinition CheckAgainstScreen(
        IPostingResolverRegistry resolvers, string screenCode, PostingTemplateDefinition definition)
    {
        var screen = PostingScreenCatalog.Find(screenCode)
            ?? throw new BusinessRuleException("POST-TEMPLATE-UNKNOWN-SCREEN", $"الشاشة '{screenCode}' مش من الشاشات اللي بترحّل قيود.");

        var fields = screen.Fields.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        var groups = screen.Groups.Select(g => g.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (definition.TriggerType == PostingTriggerType.FieldCondition)
        {
            if (!fields.TryGetValue(definition.TriggerFieldName!, out var triggerField))
            {
                throw new BusinessRuleException(
                    "POST-TEMPLATE-UNKNOWN-FIELD", $"حقل شرط التشغيل '{definition.TriggerFieldName}' مش من حقول شاشة {screen.NameAr}.");
            }

            // A coded field can only hold its listed values; any other would never match.
            if (triggerField.Choices is { } choices && !choices.Contains(definition.TriggerFieldValue, StringComparer.OrdinalIgnoreCase))
            {
                throw new BusinessRuleException(
                    "POST-TEMPLATE-TRIGGER-VALUE-UNKNOWN",
                    $"القيمة '{definition.TriggerFieldValue}' مش من قيم الحقل '{triggerField.LabelAr}' ({string.Join("، ", choices)}).");
            }
        }

        if (definition.TriggerType == PostingTriggerType.HasStockMovement && !screen.CanMoveStock)
        {
            throw new BusinessRuleException(
                "POST-TEMPLATE-TRIGGER-NOT-APPLICABLE", $"شاشة {screen.NameAr} مابتحرّكش مخزون — التشغيل (عند حركة مخزون) مش هيحصل أبدًا.");
        }

        void RequireField(PostingTemplateLineInput line, string? name, PostingFieldKind? kind, string what)
        {
            if (name is null || !fields.TryGetValue(name, out var field) || (kind is not null && field.Kind != kind))
            {
                throw Fail(line, "POST-TEMPLATE-UNKNOWN-FIELD", $"{what} '{name}' مش من حقول شاشة {screen.NameAr}.");
            }
        }

        foreach (var line in definition.Lines)
        {
            switch (line.AmountFormulaType)
            {
                case AmountFormulaType.DirectField or AmountFormulaType.Multiply or AmountFormulaType.PercentageOf:
                    RequireField(line, line.AmountFieldName, PostingFieldKind.Amount, "حقل المبلغ");
                    break;
                case AmountFormulaType.AddFields or AmountFormulaType.SubtractFields or AmountFormulaType.DivideFields:
                    foreach (var name in line.AmountFieldNames ?? [])
                    {
                        RequireField(line, name, PostingFieldKind.Amount, "حقل المبلغ");
                    }
                    break;
                case AmountFormulaType.GroupItemAmount when line.AmountFieldName is null || !groups.Contains(line.AmountFieldName):
                    throw Fail(line, "POST-TEMPLATE-UNKNOWN-FIELD", $"المجموعة '{line.AmountFieldName}' مش من مجموعات شاشة {screen.NameAr}.");
            }

            if (line.AccountSourceType == AccountSourceType.FromDocument)
            {
                RequireField(line, line.AccountFieldName, PostingFieldKind.Account, "حقل الحساب");
            }

            if (line.AccountSourceType == AccountSourceType.Resolver
                && resolvers.FindAccountResolver(line.AccountResolverKey!) is { } accountResolver)
            {
                RequireField(line, accountResolver.RequiredField, null, $"الـResolver '{accountResolver.Key}' محتاج الحقل");
            }

            if (line.ConditionType != ConditionType.None)
            {
                RequireField(line, line.ConditionFieldName, null, "حقل الشرط");
            }

            foreach (var costCenter in line.CostCenters)
            {
                switch (costCenter.SourceType)
                {
                    case CostCenterSourceType.FromDocument:
                        RequireEntityField(line, costCenter.ValueFieldName, isContext: false, "حقل المستند");
                        break;
                    case CostCenterSourceType.FromContext:
                        RequireEntityField(line, costCenter.ContextKey, isContext: true, "القيمة من الموديول");
                        break;
                    case CostCenterSourceType.FromRelatedEntity:
                        var related = screen.RelatedEntities.FirstOrDefault(r =>
                            string.Equals(r.Name, costCenter.RelatedEntityType, StringComparison.OrdinalIgnoreCase));
                        if (related is null || related.Fields.All(f => !string.Equals(f.Name, costCenter.RelatedEntityField, StringComparison.OrdinalIgnoreCase)))
                        {
                            throw Fail(line, "POST-TEMPLATE-UNKNOWN-FIELD",
                                $"'{costCenter.RelatedEntityType}.{costCenter.RelatedEntityField}' مش متاح من شاشة {screen.NameAr}.");
                        }
                        break;
                    case CostCenterSourceType.Dynamic when resolvers.FindCostCenterResolver(costCenter.ValueResolverKey!) is { } ccResolver:
                        RequireField(line, ccResolver.RequiredField, null, $"الـResolver '{ccResolver.Key}' محتاج الحقل");
                        break;
                }
            }
        }

        return screen;

        void RequireEntityField(PostingTemplateLineInput line, string? name, bool isContext, string what)
        {
            if (name is null || !fields.TryGetValue(name, out var field)
                || field.Kind != PostingFieldKind.Id || field.EntityType == CostCenterLinkedEntityType.None || field.IsContext != isContext)
            {
                throw Fail(line, "POST-TEMPLATE-UNKNOWN-FIELD", $"{what} '{name}' مش متاح لمراكز التكلفة في شاشة {screen.NameAr}.");
            }
        }
    }

    /// <summary>The kind of record a cost center line points at, so the dimension it lands in can be checked against it.</summary>
    private static CostCenterLinkedEntityType? EntityTypeOf(PostingScreenDefinition screen, PostingTemplateLineCostCenterInput costCenter) =>
        costCenter.SourceType switch
        {
            CostCenterSourceType.FromDocument => screen.Fields.First(f => string.Equals(f.Name, costCenter.ValueFieldName, StringComparison.OrdinalIgnoreCase)).EntityType,
            CostCenterSourceType.FromContext => screen.Fields.First(f => string.Equals(f.Name, costCenter.ContextKey, StringComparison.OrdinalIgnoreCase)).EntityType,
            CostCenterSourceType.FromRelatedEntity => screen.RelatedEntities
                .First(r => string.Equals(r.Name, costCenter.RelatedEntityType, StringComparison.OrdinalIgnoreCase))
                .Fields.First(f => string.Equals(f.Name, costCenter.RelatedEntityField, StringComparison.OrdinalIgnoreCase)).EntityType,
            _ => null
        };

    public static async Task CheckAsync(
        IApplicationDbContext db, IPostingResolverRegistry resolvers, PostingScreenDefinition screen, PostingTemplateDefinition definition,
        CancellationToken cancellationToken)
    {
        var fixedAccountIds = definition.Lines
            .Where(l => l.AccountSourceType == AccountSourceType.Fixed)
            .Select(l => l.FixedAccountId!.Value)
            .Distinct()
            .ToList();

        var accounts = await db.Accounts
            .Include(a => a.DimensionLinks)
            .Where(a => fixedAccountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var dimensionIds = definition.Lines.SelectMany(l => l.CostCenters).Select(c => c.CostCenterDimensionId).Distinct().ToList();
        var dimensions = await db.CostCenterDimensions
            .Where(d => dimensionIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        var fixedValueIds = definition.Lines.SelectMany(l => l.CostCenters)
            .Where(c => c.SourceType == CostCenterSourceType.Fixed)
            .Select(c => c.FixedValueId!.Value)
            .Distinct()
            .ToList();
        var values = await db.CostCenterDimensionValues
            .Where(v => fixedValueIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        foreach (var line in definition.Lines)
        {
            Account? fixedAccount = null;

            if (line.AccountSourceType == AccountSourceType.Fixed)
            {
                if (!accounts.TryGetValue(line.FixedAccountId!.Value, out fixedAccount))
                {
                    throw Fail(line, "POST-TEMPLATE-ACCOUNT-NOT-FOUND", $"الحساب رقم {line.FixedAccountId} غير موجود.");
                }
                if (!fixedAccount.IsPostable)
                {
                    throw Fail(line, "POST-TEMPLATE-ACCOUNT-NOT-POSTABLE", $"الحساب {fixedAccount.Code} تجميعي ومبيقبلش ترحيل.");
                }
                if (!fixedAccount.IsActive)
                {
                    throw Fail(line, "POST-TEMPLATE-ACCOUNT-INACTIVE", $"الحساب {fixedAccount.Code} غير نشط.");
                }
            }

            foreach (var costCenter in line.CostCenters)
            {
                if (!dimensions.TryGetValue(costCenter.CostCenterDimensionId, out var dimension) || !dimension.IsActive)
                {
                    throw Fail(line, "POST-TEMPLATE-DIMENSION-INVALID", $"البُعد رقم {costCenter.CostCenterDimensionId} غير موجود أو غير نشط.");
                }

                // Spec 3.3: a template cannot put a dimension on an account that dimension is not
                // linked to. Only checkable when the account is fixed; for company roles and
                // resolvers the account is only known at posting time, where IPostingService checks.
                if (fixedAccount is not null && fixedAccount.DimensionLinks.All(l => l.CostCenterDimensionId != dimension.Id))
                {
                    throw Fail(line, "POST-TEMPLATE-DIMENSION-NOT-LINKED",
                        $"البُعد '{dimension.NameAr}' مش مربوط بالحساب {fixedAccount.Code}.");
                }

                if (costCenter.SourceType == CostCenterSourceType.Fixed)
                {
                    // Spec 3.3: the value must belong to this line's dimension, and be active when the
                    // template is saved. It is never deleted afterwards, so old entries keep reading.
                    if (!values.TryGetValue(costCenter.FixedValueId!.Value, out var value) || value.CostCenterDimensionId != dimension.Id)
                    {
                        throw Fail(line, "POST-TEMPLATE-VALUE-WRONG-DIMENSION",
                            $"القيمة رقم {costCenter.FixedValueId} مش تابعة للبُعد '{dimension.NameAr}'.");
                    }
                    if (!value.IsActive)
                    {
                        throw Fail(line, "POST-TEMPLATE-VALUE-INACTIVE", $"القيمة '{value.NameAr}' غير نشطة.");
                    }
                }

                if (costCenter.SourceType == CostCenterSourceType.Dynamic
                    && resolvers.FindCostCenterResolver(costCenter.ValueResolverKey!)?.RequiresLinkedEntityType is { } required
                    && dimension.LinkedEntityType != required)
                {
                    throw Fail(line, "POST-TEMPLATE-RESOLVER-DIMENSION-MISMATCH",
                        $"الـResolver '{costCenter.ValueResolverKey}' محتاج بُعد مربوط بـ{required}، والبُعد '{dimension.NameAr}' مش كده.");
                }

                // An entity-based source can only fill a dimension whose values stand for that kind of
                // entity: a terminal id matched against a branch dimension's codes would pick nonsense.
                if (EntityTypeOf(screen, costCenter) is { } entityType && dimension.LinkedEntityType != entityType)
                {
                    throw Fail(line, "POST-TEMPLATE-DIMENSION-ENTITY-MISMATCH",
                        $"البُعد '{dimension.NameAr}' مش مربوط بـ{entityType} — اربطه بيه من شاشة مراكز التكلفة، أو اختار قيمة ثابتة.");
                }
            }
        }
    }

    private static BusinessRuleException Fail(PostingTemplateLineInput line, string code, string message) =>
        new(code, $"السطر {line.LineNumber}: {message}");
}

internal static class PostingTemplateLineFactory
{
    /// <summary>Builds lines keeping only the fields their source types actually use.</summary>
    public static IEnumerable<PostingTemplateLine> Build(IEnumerable<PostingTemplateLineInput> inputs) =>
        inputs.OrderBy(l => l.LineNumber).Select(input =>
        {
            var formula = input.AmountFormulaType;
            var line = new PostingTemplateLine
            {
                LineNumber = input.LineNumber,
                Direction = input.Direction,
                AccountSourceType = input.AccountSourceType,
                FixedAccountId = input.AccountSourceType == AccountSourceType.Fixed ? input.FixedAccountId : null,
                AccountFieldName = input.AccountSourceType == AccountSourceType.FromDocument ? input.AccountFieldName : null,
                AccountResolverKey = input.AccountSourceType is AccountSourceType.FromCompany or AccountSourceType.Resolver
                    ? input.AccountResolverKey
                    : null,
                AmountFormulaType = formula,
                AmountFieldName = formula is AmountFormulaType.DirectField or AmountFormulaType.GroupItemAmount
                    or AmountFormulaType.Multiply or AmountFormulaType.PercentageOf
                    ? input.AmountFieldName
                    : null,
                AmountFieldNames = formula is AmountFormulaType.AddFields or AmountFormulaType.SubtractFields or AmountFormulaType.DivideFields
                    ? string.Join(',', input.AmountFieldNames ?? [])
                    : null,
                AmountMultiplier = formula == AmountFormulaType.Multiply ? input.AmountMultiplier : null,
                AmountPercentage = formula == AmountFormulaType.PercentageOf ? input.AmountPercentage : null,
                ConditionType = input.ConditionType,
                ConditionFieldName = input.ConditionType == ConditionType.None ? null : input.ConditionFieldName,
                ConditionFieldValue = input.ConditionType == ConditionType.FieldEquals ? input.ConditionFieldValue : null,
                LineDescription = input.LineDescription
            };

            foreach (var cc in input.CostCenters)
            {
                line.CostCenters.Add(new PostingTemplateLineCostCenter
                {
                    CostCenterDimensionId = cc.CostCenterDimensionId,
                    SourceType = cc.SourceType,
                    FixedValueId = cc.SourceType == CostCenterSourceType.Fixed ? cc.FixedValueId : null,
                    ValueFieldName = cc.SourceType == CostCenterSourceType.FromDocument ? cc.ValueFieldName : null,
                    ValueResolverKey = cc.SourceType == CostCenterSourceType.Dynamic ? cc.ValueResolverKey : null,
                    RelatedEntityType = cc.SourceType == CostCenterSourceType.FromRelatedEntity ? cc.RelatedEntityType : null,
                    RelatedEntityField = cc.SourceType == CostCenterSourceType.FromRelatedEntity ? cc.RelatedEntityField : null,
                    ContextKey = cc.SourceType == CostCenterSourceType.FromContext ? cc.ContextKey : null,
                    DisplayOrder = cc.DisplayOrder
                });
            }

            return line;
        });

    /// <summary>The input form of a stored line — what the editor loads and a copy starts from.</summary>
    public static PostingTemplateLineInput ToInput(PostingTemplateLine l) => new(
        l.LineNumber, l.Direction, l.AccountSourceType, l.FixedAccountId, l.AccountFieldName, l.AccountResolverKey,
        l.AmountFormulaType, l.AmountFieldName, l.ConditionType, l.ConditionFieldName, l.ConditionFieldValue, l.LineDescription,
        l.CostCenters.OrderBy(c => c.DisplayOrder).Select(c => new PostingTemplateLineCostCenterInput(
            c.CostCenterDimensionId, c.SourceType, c.FixedValueId, c.ValueFieldName, c.ValueResolverKey, c.DisplayOrder,
            c.RelatedEntityType, c.RelatedEntityField, c.ContextKey)).ToList(),
        l.AmountFieldNames?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
        l.AmountMultiplier,
        l.AmountPercentage);
}
