using System.Globalization;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Posting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Posting.Templates;

public sealed class PostingPreviewLineDto
{
    public required int LineNumber { get; init; }
    public required string Direction { get; init; }
    public required string AccountDisplay { get; init; }

    /// <summary>False when the account is only known per document (a supplier's, a terminal's).</summary>
    public required bool AccountKnownNow { get; init; }

    public decimal Amount { get; init; }

    /// <summary>How the amount was worked out, e.g. "الإجمالي قبل الخصم × 14% = 1,000.00 × 0.14".</summary>
    public string? AmountFormula { get; init; }

    public required bool Skipped { get; init; }
    public string? Note { get; init; }
    public required IReadOnlyList<string> CostCenters { get; init; }
}

public sealed class PostingPreviewDto
{
    /// <summary>Whether the template's trigger admits the sample document at all.</summary>
    public required bool TriggerMatched { get; init; }
    public required string TriggerDescription { get; init; }

    public required IReadOnlyList<PostingPreviewLineDto> Lines { get; init; }
    public required decimal TotalDebit { get; init; }
    public required decimal TotalCredit { get; init; }
    public required bool IsBalanced { get; init; }

    /// <summary>Problems that would stop a real posting: an unmapped role, a missing field.</summary>
    public required IReadOnlyList<string> Problems { get; init; }
}

/// <summary>A sample document: amounts, coded values (PaymentType = Cash), and whether it moved stock.</summary>
public sealed record PostingPreviewSample(
    IReadOnlyDictionary<string, decimal>? Values = null,
    IReadOnlyDictionary<string, string>? Texts = null,
    bool HasStockMovement = true);

/// <summary>
/// What one template would post for a sample document — before it is saved or switched on
/// (spec 13: the preview belongs to the first release, since a template's mistakes must show up
/// before the first real document, not on it). Nothing is written.
/// </summary>
public sealed record PreviewPostingTemplateQuery(
    string ScreenCode,
    PostingTemplateDefinition Definition,
    PostingPreviewSample? Sample = null) : IRequest<PostingPreviewDto>;

public sealed class PreviewPostingTemplateQueryHandler(IApplicationDbContext db) : IRequestHandler<PreviewPostingTemplateQuery, PostingPreviewDto>
{
    public async Task<PostingPreviewDto> Handle(PreviewPostingTemplateQuery request, CancellationToken cancellationToken)
    {
        var screen = PostingScreenCatalog.Find(request.ScreenCode)
            ?? throw new BusinessRuleException("POST-TEMPLATE-UNKNOWN-SCREEN", $"الشاشة '{request.ScreenCode}' مش من الشاشات اللي بترحّل قيود.");

        var previewer = await PostingPreviewer.LoadAsync(db, screen, [request.Definition], request.Sample ?? new PostingPreviewSample(), cancellationToken);
        return previewer.Preview(request.Definition);
    }
}

public sealed class PostingScreenPreviewItemDto
{
    public required long TemplateId { get; init; }
    public required string NameAr { get; init; }
    public required int ExecutionOrder { get; init; }
    public required bool IsActive { get; init; }
    public required PostingPreviewDto Preview { get; init; }
}

/// <summary>
/// Every entry the screen's current templates would post for one sample document, in execution
/// order (design notes, decision 7) — the view that shows a revenue template and a cost-of-sales
/// template together, and a cash-sale template staying out of a credit sale.
/// </summary>
public sealed record PreviewPostingScreenQuery(string ScreenCode, PostingPreviewSample? Sample = null)
    : IRequest<IReadOnlyList<PostingScreenPreviewItemDto>>;

public sealed class PreviewPostingScreenQueryHandler(IApplicationDbContext db)
    : IRequestHandler<PreviewPostingScreenQuery, IReadOnlyList<PostingScreenPreviewItemDto>>
{
    public async Task<IReadOnlyList<PostingScreenPreviewItemDto>> Handle(PreviewPostingScreenQuery request, CancellationToken cancellationToken)
    {
        var screen = PostingScreenCatalog.Find(request.ScreenCode)
            ?? throw new BusinessRuleException("POST-TEMPLATE-UNKNOWN-SCREEN", $"الشاشة '{request.ScreenCode}' مش من الشاشات اللي بترحّل قيود.");

        var templates = await db.PostingTemplates.AsNoTracking()
            .Include(t => t.Lines).ThenInclude(l => l.CostCenters)
            .Where(t => t.ScreenCode == screen.ScreenCode && t.IsCurrentVersion)
            .OrderBy(t => t.ExecutionOrder).ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

        var definitions = templates.Select(t => (Template: t, Definition: new PostingTemplateDefinition(
            t.NameAr, t.NameEn, t.Description, t.Lines.OrderBy(l => l.LineNumber).Select(PostingTemplateLineFactory.ToInput).ToList(),
            t.TriggerType, t.TriggerFieldName, t.TriggerFieldValue, t.ExecutionOrder))).ToList();

        var previewer = await PostingPreviewer.LoadAsync(
            db, screen, definitions.Select(d => d.Definition).ToList(), request.Sample ?? new PostingPreviewSample(), cancellationToken);

        return definitions.Select(d => new PostingScreenPreviewItemDto
        {
            TemplateId = d.Template.Id,
            NameAr = d.Template.NameAr,
            ExecutionOrder = d.Template.ExecutionOrder,
            IsActive = d.Template.IsActive,
            Preview = previewer.Preview(d.Definition)
        }).ToList();
    }
}

/// <summary>
/// The display side of the engine: evaluates templates against a sample the way PostingTemplateEngine
/// evaluates them against a document, but describes what it cannot know yet (a supplier's own account,
/// the branch's cost center value) instead of looking it up.
/// </summary>
internal sealed class PostingPreviewer
{
    private readonly PostingScreenDefinition _screen;
    private readonly PostingContext _context;
    private readonly IReadOnlyDictionary<string, decimal> _groupAmounts;
    private readonly IReadOnlyDictionary<long, string> _accounts;
    private readonly IReadOnlyDictionary<CompanyAccountRole, string> _mappings;
    private readonly IReadOnlyDictionary<long, string> _dimensions;
    private readonly IReadOnlyDictionary<long, string> _values;
    private readonly Dictionary<string, string> _labels;

    private PostingPreviewer(
        PostingScreenDefinition screen, PostingContext context, IReadOnlyDictionary<string, decimal> groupAmounts,
        IReadOnlyDictionary<long, string> accounts, IReadOnlyDictionary<CompanyAccountRole, string> mappings,
        IReadOnlyDictionary<long, string> dimensions, IReadOnlyDictionary<long, string> values)
    {
        _screen = screen;
        _context = context;
        _groupAmounts = groupAmounts;
        _accounts = accounts;
        _mappings = mappings;
        _dimensions = dimensions;
        _values = values;
        _labels = screen.Fields.ToDictionary(f => f.Name, f => f.LabelAr, StringComparer.OrdinalIgnoreCase);
        foreach (var group in screen.Groups)
        {
            _labels[group.Name] = group.LabelAr;
        }
    }

    public static async Task<PostingPreviewer> LoadAsync(
        IApplicationDbContext db, PostingScreenDefinition screen, IReadOnlyList<PostingTemplateDefinition> definitions,
        PostingPreviewSample sample, CancellationToken cancellationToken)
    {
        decimal ValueOf(string name, decimal? fallback) =>
            sample.Values?.FirstOrDefault(o => string.Equals(o.Key, name, StringComparison.OrdinalIgnoreCase)) is { Key: not null } o
                ? o.Value
                : fallback ?? 0m;
        string? TextOf(PostingScreenField field) =>
            sample.Texts?.FirstOrDefault(o => string.Equals(o.Key, field.Name, StringComparison.OrdinalIgnoreCase)) is { Key: not null } o
                ? o.Value
                : field.Choices?.FirstOrDefault();

        var fields = screen.Fields.ToDictionary(
            f => f.Name,
            f => f.Kind switch
            {
                PostingFieldKind.Amount => (object?)ValueOf(f.Name, f.Sample),
                PostingFieldKind.Text => TextOf(f),
                _ => null
            });
        if (screen.CanMoveStock)
        {
            fields[PostingScreenCatalog.HasStockMovementField] = sample.HasStockMovement;
        }

        var lines = definitions.SelectMany(d => d.Lines).ToList();
        var accountIds = lines.Where(l => l.FixedAccountId is not null).Select(l => l.FixedAccountId!.Value).Distinct().ToList();
        var dimensionIds = lines.SelectMany(l => l.CostCenters).Select(c => c.CostCenterDimensionId).Distinct().ToList();
        var valueIds = lines.SelectMany(l => l.CostCenters).Where(c => c.FixedValueId is not null).Select(c => c.FixedValueId!.Value).Distinct().ToList();

        return new PostingPreviewer(
            screen,
            PostingContext.Create(fields),
            screen.Groups.ToDictionary(g => g.Name, g => ValueOf(g.Name, g.Sample), StringComparer.OrdinalIgnoreCase),
            await db.Accounts.AsNoTracking().Where(a => accountIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => $"{a.Code} — {a.NameAr}", cancellationToken),
            await db.CompanyAccountMappings.AsNoTracking()
                .Select(m => new { m.Role, m.Account!.Code, m.Account.NameAr })
                .ToDictionaryAsync(m => m.Role, m => $"{m.Code} — {m.NameAr}", cancellationToken),
            await db.CostCenterDimensions.AsNoTracking().Where(d => dimensionIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.NameAr, cancellationToken),
            await db.CostCenterDimensionValues.AsNoTracking().Where(v => valueIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => v.NameAr, cancellationToken));
    }

    public PostingPreviewDto Preview(PostingTemplateDefinition definition)
    {
        var template = new PostingTemplate
        {
            TriggerType = definition.TriggerType,
            TriggerFieldName = definition.TriggerFieldName,
            TriggerFieldValue = definition.TriggerFieldValue
        };
        var matched = PostingTemplateEngine.TriggerMatches(template, _context);
        var triggerDescription = definition.TriggerType switch
        {
            PostingTriggerType.HasStockMovement => "بيشتغل لما المستند يحرّك مخزون بتكلفة",
            PostingTriggerType.FieldCondition =>
                $"بيشتغل لما {Label(definition.TriggerFieldName)} = {definition.TriggerFieldValue}",
            _ => "بيشتغل دايمًا"
        };

        var problems = new List<string>();
        var lines = new List<PostingPreviewLineDto>();

        foreach (var input in definition.Lines.OrderBy(l => l.LineNumber))
        {
            var line = PostingTemplateLineFactory.Build([input]).Single();
            var direction = input.Direction.ToString();
            var costCenters = input.CostCenters.OrderBy(c => c.DisplayOrder).Select(DescribeCostCenter).ToList();

            bool conditionHolds;
            try
            {
                conditionHolds = PostingFormulas.EvaluateCondition(line, _context);
            }
            catch (BusinessRuleException e)
            {
                problems.Add($"السطر {input.LineNumber}: {e.Message}");
                continue;
            }

            if (!conditionHolds)
            {
                lines.Add(new PostingPreviewLineDto
                {
                    LineNumber = input.LineNumber, Direction = direction, AccountDisplay = "—", AccountKnownNow = true,
                    Skipped = true, Note = "متخطّى — الشرط مش متحقق في المثال", CostCenters = costCenters
                });
                continue;
            }

            decimal amount;
            string account;
            string? formula = null;
            var known = true;

            if (input.AccountSourceType == AccountSourceType.FromGroup)
            {
                amount = _groupAmounts.GetValueOrDefault(input.AmountFieldName ?? "", 0m);
                account = $"حسب المجموعة: {Label(input.AmountFieldName)}";
                known = false;
            }
            else
            {
                try
                {
                    amount = PostingFormulas.EvaluateAmount(line, _context);
                    formula = DescribeFormula(line);
                }
                catch (BusinessRuleException e)
                {
                    problems.Add($"السطر {input.LineNumber}: {e.Message}");
                    continue;
                }

                (account, known) = input.AccountSourceType switch
                {
                    AccountSourceType.Fixed => (_accounts.GetValueOrDefault(input.FixedAccountId ?? 0, "حساب غير موجود"), true),
                    AccountSourceType.FromCompany when Enum.TryParse<CompanyAccountRole>(input.AccountResolverKey, out var role) =>
                        _mappings.TryGetValue(role, out var mapped) ? (mapped, true) : ($"الدور '{input.AccountResolverKey}' — مش مربوط", true),
                    AccountSourceType.FromDocument => ($"من المستند: {Label(input.AccountFieldName)}", false),
                    AccountSourceType.Resolver => (DescribeAccountResolver(input.AccountResolverKey), false),
                    _ => ("؟", false)
                };

                if (input.AccountSourceType == AccountSourceType.FromCompany && account.EndsWith("مش مربوط"))
                {
                    problems.Add($"السطر {input.LineNumber}: الدور '{input.AccountResolverKey}' مش مربوط بحساب — القيد هيفشل. اربطه من شاشة حسابات الترحيل الافتراضية.");
                }
            }

            if (amount <= 0 && input.AccountSourceType != AccountSourceType.FromGroup)
            {
                problems.Add($"السطر {input.LineNumber}: المبلغ في المثال {amount} — لو ده ممكن يحصل فعلًا، حط شرط (أكبر من صفر) على السطر.");
            }

            lines.Add(new PostingPreviewLineDto
            {
                LineNumber = input.LineNumber, Direction = direction, AccountDisplay = account, AccountKnownNow = known,
                Amount = amount, AmountFormula = formula, Skipped = false, CostCenters = costCenters
            });
        }

        var debit = lines.Where(l => !l.Skipped && l.Direction == nameof(PostingDirection.Debit)).Sum(l => l.Amount);
        var credit = lines.Where(l => !l.Skipped && l.Direction == nameof(PostingDirection.Credit)).Sum(l => l.Amount);
        if (matched && debit != credit)
        {
            problems.Add($"القيد في المثال مش متزن: مدين {debit:0.00} ≠ دائن {credit:0.00}.");
        }

        return new PostingPreviewDto
        {
            TriggerMatched = matched,
            TriggerDescription = triggerDescription,
            Lines = lines,
            TotalDebit = debit,
            TotalCredit = credit,
            IsBalanced = debit == credit,
            Problems = problems
        };
    }

    private string Label(string? name) => name is null ? "؟" : _labels.GetValueOrDefault(name, name);

    private static string Money(decimal value) => value.ToString("#,##0.00", CultureInfo.InvariantCulture);

    /// <summary>The design notes' preview line: the formula, then the same with the sample's figures.</summary>
    private string? DescribeFormula(PostingTemplateLine line)
    {
        string V(string name) => Money(_context.GetDecimal(name));
        var fields = PostingFormulas.AmountFields(line);

        return line.AmountFormulaType switch
        {
            AmountFormulaType.Multiply =>
                $"{Label(line.AmountFieldName)} × {line.AmountMultiplier} = {V(line.AmountFieldName!)} × {line.AmountMultiplier}",
            AmountFormulaType.PercentageOf =>
                $"{Label(line.AmountFieldName)} × {line.AmountPercentage}% = {V(line.AmountFieldName!)} × {(line.AmountPercentage!.Value / 100m).ToString(CultureInfo.InvariantCulture)}",
            AmountFormulaType.AddFields =>
                $"{string.Join(" + ", fields.Select(Label))} = {string.Join(" + ", fields.Select(V))}",
            AmountFormulaType.SubtractFields => $"{Label(fields[0])} − {Label(fields[1])} = {V(fields[0])} − {V(fields[1])}",
            AmountFormulaType.DivideFields => $"{Label(fields[0])} ÷ {Label(fields[1])} = {V(fields[0])} ÷ {V(fields[1])}",
            _ => null
        };
    }

    private string DescribeCostCenter(PostingTemplateLineCostCenterInput c)
    {
        var dimension = _dimensions.GetValueOrDefault(c.CostCenterDimensionId, $"#{c.CostCenterDimensionId}");
        var value = c.SourceType switch
        {
            CostCenterSourceType.Fixed => _values.GetValueOrDefault(c.FixedValueId ?? 0, "؟"),
            CostCenterSourceType.FromDocument => $"من المستند: {Label(c.ValueFieldName)}",
            CostCenterSourceType.FromContext => $"من الموديول: {Label(c.ContextKey)}",
            CostCenterSourceType.FromRelatedEntity => DescribeRelated(c),
            _ => c.ValueResolverKey == "Branch.ToCostCenterValue" ? "فرع المستند" : c.ValueResolverKey ?? "؟"
        };
        return $"{dimension}: {value}";
    }

    private string DescribeRelated(PostingTemplateLineCostCenterInput c)
    {
        var related = _screen.RelatedEntities.FirstOrDefault(r => string.Equals(r.Name, c.RelatedEntityType, StringComparison.OrdinalIgnoreCase));
        var field = related?.Fields.FirstOrDefault(f => string.Equals(f.Name, c.RelatedEntityField, StringComparison.OrdinalIgnoreCase));
        return related is null || field is null ? $"{c.RelatedEntityType}.{c.RelatedEntityField}" : $"{field.LabelAr} ({related.LabelAr})";
    }

    private string DescribeAccountResolver(string? key) => key switch
    {
        "Supplier.PayableAccountId" => $"حساب المورد — ولو مالوش: {_mappings.GetValueOrDefault(CompanyAccountRole.DefaultPayable, "الموردين الافتراضي (مش مربوط)")}",
        "Customer.ReceivableAccountId" => $"حساب العميل — ولو مالوش: {_mappings.GetValueOrDefault(CompanyAccountRole.DefaultReceivable, "العملاء الافتراضي (مش مربوط)")}",
        "POSTerminal.CashTreasuryAccount" => "خزينة الكاش بتاعة الجهاز",
        _ => key ?? "؟"
    };
}
