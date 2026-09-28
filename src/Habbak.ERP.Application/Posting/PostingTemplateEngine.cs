using System.Text.Json;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Posting.Resolvers;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Posting;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Posting;

/// <summary>
/// Turns a source document into journal entries using its screen's posting templates
/// (00-Posting-Engine-Architecture.md; Docs/Posting-Engine-Implementation-Plan.md).
///
/// A builder on top of IPostingService rather than a replacement for it. The existing service
/// already takes a finished list of lines and checks balance, postable accounts, mandatory
/// dimensions and closed periods; this only has to produce that list from a template. Every
/// accounting screen that posts today keeps doing so untouched, and those checks run exactly once.
///
/// A screen can have several templates (design notes 2026-09-18, note 1). Each active one whose
/// trigger the document meets produces its own entry, in ExecutionOrder, all sharing one
/// PostingGroupId. Like IPostingService it never saves the entries: the caller commits them, their
/// snapshots and its own document together, so either all of them land or none does.
/// </summary>
public interface IPostingTemplateEngine
{
    /// <summary>Builds the entries without posting them.</summary>
    Task<IReadOnlyList<BuiltPosting>> BuildAsync(TemplatePostingRequest request, CancellationToken cancellationToken = default);

    Task<TemplatePostingResult> PostAsync(TemplatePostingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// What every document calls: posts when the screen has an active template, and returns null
    /// (the document carries on with no entry) when it has none. The entry returned is the first of
    /// the group — the one the document links to.
    /// </summary>
    Task<JournalEntry?> PostIfConfiguredAsync(TemplatePostingRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the screen has any active template. This is the switch that turns automatic posting
    /// on for a screen: a document posted while it is off gets no entry and keeps a null
    /// JournalEntryId. Once it is on, every failure stops the document.
    /// </summary>
    Task<bool> IsConfiguredAsync(long companyId, string screenCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Undoes a document's entries when the document is cancelled: a mirror of every entry in the
    /// given entry's posting group, posted at once and linked by ReversalOfEntryId. Calling it again
    /// returns the existing reversals. Returns the reversal of the entry passed in.
    /// </summary>
    Task<JournalEntry> ReverseAsync(
        long journalEntryId, DateOnly reversalDate, string reason, CancellationToken cancellationToken = default);
}

public sealed record BuiltPosting(PostingTemplate Template, PostingRequest Request);

internal sealed class PostingTemplateEngine(
    IApplicationDbContext db,
    IPostingService postingService,
    IPostingResolverRegistry resolvers,
    IPostingFailureRecorder? failureRecorder = null) : IPostingTemplateEngine
{
    /// <summary>Context field the stock-moving screens set; read by the HasStockMovement trigger.</summary>
    public const string HasStockMovementField = "HasStockMovement";

    public async Task<TemplatePostingResult> PostAsync(TemplatePostingRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await PostCoreAsync(request, cancellationToken);
        }
        catch (Exception e) when (e is BusinessRuleException or PostingValidationException && failureRecorder is not null)
        {
            var (code, message) = e switch
            {
                BusinessRuleException b => (b.Code, b.Message),
                PostingValidationException v => (v.Errors[0].Code, v.Message),
                _ => ("POST-UNKNOWN", e.Message)
            };

            // Not the caller's token: a cancelled request still failed, and that is worth knowing.
            await failureRecorder!.RecordAsync(new PostingFailureRecord(
                request.CompanyId, request.BranchId, request.ScreenCode, request.SourceModule, request.SourceDocumentId,
                request.Description, code, message), CancellationToken.None);
            throw;
        }
    }

    public async Task<JournalEntry?> PostIfConfiguredAsync(TemplatePostingRequest request, CancellationToken cancellationToken = default)
    {
        if (!await IsConfiguredAsync(request.CompanyId, request.ScreenCode, cancellationToken))
        {
            return null;
        }

        return (await PostAsync(request, cancellationToken)).JournalEntry;
    }

    public Task<bool> IsConfiguredAsync(long companyId, string screenCode, CancellationToken cancellationToken = default) =>
        ActiveTemplates(companyId, screenCode).AnyAsync(cancellationToken);

    /// <summary>
    /// The screen's current active templates. The explicit CompanyId check backs up the global query
    /// filter, the same second line of defence PostingService uses for periods.
    /// </summary>
    private IQueryable<PostingTemplate> ActiveTemplates(long companyId, string screenCode) =>
        db.PostingTemplates.Where(t => t.CompanyId == companyId && t.ScreenCode == screenCode && t.IsCurrentVersion && t.IsActive);

    private async Task<TemplatePostingResult> PostCoreAsync(TemplatePostingRequest request, CancellationToken cancellationToken)
    {
        var replay = await FindReplayAsync(request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var built = await BuildAsync(request, cancellationToken);
        var groupId = Guid.NewGuid();
        var entries = new List<PostedTemplateEntry>();
        if (built.Count == 0)
        {
            return new TemplatePostingResult { Entries = entries, PostingGroupId = groupId, IsReplay = false };
        }

        foreach (var posting in built)
        {
            var posted = await postingService.PostAsync(
                new PostingRequest
                {
                    CompanyId = posting.Request.CompanyId,
                    BranchId = posting.Request.BranchId,
                    EntryDate = posting.Request.EntryDate,
                    Description = posting.Request.Description,
                    SourceModule = posting.Request.SourceModule,
                    SourceDocumentType = posting.Request.SourceDocumentType,
                    SourceDocumentId = posting.Request.SourceDocumentId,
                    IsAutoGenerated = true,
                    PostingGroupId = groupId,
                    Lines = posting.Request.Lines
                },
                cancellationToken);

            db.JournalEntryTemplateSnapshots.Add(new JournalEntryTemplateSnapshot
            {
                JournalEntry = posted.JournalEntry,
                PostingTemplateId = posting.Template.Id,
                TemplateVersionNumber = posting.Template.VersionNumber,
                TemplateSnapshotJson = SerializeTemplate(posting.Template),
                IdempotencyKey = PostingKeys.Derive(request.IdempotencyKey, posting.Template.FamilyId),
                RequestIdempotencyKey = request.IdempotencyKey,
                PostingGroupId = groupId
            });

            entries.Add(new PostedTemplateEntry(posted.JournalEntry, posting.Template.Id, posting.Template.VersionNumber));
        }

        // Earlier failed attempts for this document are settled by this posting. Saved with it, so a
        // later failure of the caller's own save leaves them open, which is what they still are.
        var openFailures = await db.PostingFailures
            .Where(f => f.ScreenCode == request.ScreenCode && f.SourceDocumentId == request.SourceDocumentId && !f.IsResolved)
            .ToListAsync(cancellationToken);
        foreach (var failure in openFailures)
        {
            failure.IsResolved = true;
            failure.ResolvedAtUtc = DateTime.UtcNow;
            failure.ResolvedByJournalEntry = entries[0].JournalEntry;
        }

        return new TemplatePostingResult { Entries = entries, PostingGroupId = groupId, IsReplay = false };
    }

    public async Task<IReadOnlyList<BuiltPosting>> BuildAsync(TemplatePostingRequest request, CancellationToken cancellationToken = default)
    {
        var templates = await ActiveTemplates(request.CompanyId, request.ScreenCode)
            .Include(t => t.Lines).ThenInclude(l => l.CostCenters)
            .OrderBy(t => t.ExecutionOrder).ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);

        if (templates.Count == 0)
        {
            throw new BusinessRuleException("POST-TEMPLATE-NOT-FOUND", $"مفيش قالب ترحيل نشط للشاشة '{request.ScreenCode}'.");
        }

        var matching = templates.Where(t => TriggerMatches(t, request.Context)).ToList();

        // Only stock-triggered templates, and no stock moved (a delivery of zero-cost goods): there
        // is simply nothing to post, not a gap.
        if (matching.Count == 0 && templates.All(t => t.TriggerType == PostingTriggerType.HasStockMovement))
        {
            return [];
        }

        if (matching.Count == 0)
        {
            // The screen posts, but nothing here covers this document — a cash-only template and a
            // credit invoice, say. Posting nothing would leave the document silently off the books.
            throw new BusinessRuleException(
                "POST-NO-TEMPLATE-MATCHED",
                $"الشاشة بترحّل قيود، بس ولا قالب من قوالبها ({string.Join("، ", templates.Select(t => t.NameAr))}) بينطبق على المستند ده.");
        }

        var currencyCode = request.CurrencyCode ?? await BaseCurrencyAsync(request.CompanyId, cancellationToken);
        var built = new List<BuiltPosting>();
        foreach (var template in matching)
        {
            built.Add(new BuiltPosting(template, await BuildOneAsync(template, request, currencyCode, cancellationToken)));
        }

        return built;
    }

    internal static bool TriggerMatches(PostingTemplate template, PostingContext context) => template.TriggerType switch
    {
        PostingTriggerType.Always => true,
        PostingTriggerType.HasStockMovement => context.GetValue(HasStockMovementField) is true,
        PostingTriggerType.FieldCondition => string.Equals(
            context.GetText(template.TriggerFieldName!), template.TriggerFieldValue, StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    private async Task<PostingRequest> BuildOneAsync(
        PostingTemplate template, TemplatePostingRequest request, string currencyCode, CancellationToken cancellationToken)
    {
        var lines = new List<PostingLineRequest>();

        foreach (var line in template.Lines.OrderBy(l => l.LineNumber))
        {
            // A false condition is a deliberate skip. Everything after this point failing is not:
            // an unresolved account, amount or dimension stops the whole posting (spec rule 12).
            if (!PostingFormulas.EvaluateCondition(line, request.Context))
            {
                continue;
            }

            var dimensions = new Dictionary<long, long>();
            foreach (var costCenter in line.CostCenters.OrderBy(c => c.DisplayOrder))
            {
                var valueId = await ResolveCostCenterAsync(costCenter, request.Context, cancellationToken)
                    ?? throw Unresolved(template, line, "POST-COSTCENTER-UNRESOLVED", $"قيمة البُعد رقم {costCenter.CostCenterDimensionId} ({DescribeCostCenterSource(costCenter)})");
                dimensions[costCenter.CostCenterDimensionId] = valueId;
            }

            foreach (var (dimensionId, valueId) in request.Dimensions ?? new Dictionary<long, long>())
            {
                dimensions.TryAdd(dimensionId, valueId);
            }

            if (line.AccountSourceType == AccountSourceType.FromGroup)
            {
                // One journal line per account in the group. An empty group, or an account whose
                // share is zero, simply contributes nothing — no card payments this shift is normal.
                foreach (var item in request.Context.GetGroup(line.AmountFieldName!))
                {
                    var itemAmount = Math.Round(item.Amount, 2);
                    if (itemAmount < 0)
                    {
                        throw new BusinessRuleException(
                            "POST-AMOUNT-NOT-POSITIVE",
                            $"{Where(template, line)}: مبلغ سالب ({itemAmount}) في المجموعة '{line.AmountFieldName}'.");
                    }
                    if (itemAmount > 0)
                    {
                        var itemDimensions = new Dictionary<long, long>(dimensions);
                        foreach (var (dimensionId, valueId) in item.Dimensions ?? new Dictionary<long, long>())
                        {
                            itemDimensions[dimensionId] = valueId;
                        }

                        lines.Add(BuildLine(line, item.AccountId, itemAmount, itemDimensions, currencyCode, request));
                    }
                }
                continue;
            }

            var accountId = await ResolveAccountAsync(line, request.Context, cancellationToken)
                ?? throw Unresolved(template, line, "POST-ACCOUNT-UNRESOLVED", $"الحساب ({DescribeAccountSource(line)})");

            var amount = PostingFormulas.EvaluateAmount(line, request.Context);

            // A line whose condition passed but whose amount comes out zero or negative is almost
            // always a template or data mistake; the spec treats it as a failure, not an empty line.
            // A genuinely optional amount belongs behind a FieldGreaterThanZero condition.
            if (amount <= 0)
            {
                throw new BusinessRuleException(
                    "POST-AMOUNT-NOT-POSITIVE",
                    $"{Where(template, line)}: المبلغ طلع {amount} — لازم يكون أكبر من صفر. لو المبلغ ده اختياري، القالب محتاج شرط (أكبر من صفر) على السطر.");
            }

            lines.Add(BuildLine(line, accountId, amount, dimensions, currencyCode, request));
        }

        if (lines.Count == 0)
        {
            throw new BusinessRuleException(
                "POST-EMPTY-ENTRY",
                $"قالب '{template.NameAr}' (إصدار {template.VersionNumber}): كل السطور اتخطّت بالشروط — مفيش قيد يترحّل.");
        }

        // IPostingService checks balance too, but only this layer can say which template and version
        // produced the imbalance, which is the first thing anyone fixing it needs to know.
        var debit = lines.Sum(l => l.DebitAmount);
        var credit = lines.Sum(l => l.CreditAmount);
        if (debit != credit)
        {
            throw new BusinessRuleException(
                "POST-UNBALANCED",
                $"قالب '{template.NameAr}' (إصدار {template.VersionNumber}) أنتج قيد غير متزن: مدين {debit} ≠ دائن {credit}.");
        }

        return new PostingRequest
        {
            CompanyId = request.CompanyId,
            BranchId = request.BranchId,
            EntryDate = request.EntryDate,
            Description = template.ExecutionOrder > 1 || template.TriggerType != PostingTriggerType.Always
                ? $"{request.Description} — {template.NameAr}"
                : request.Description,
            SourceModule = request.SourceModule,
            SourceDocumentType = request.SourceDocumentType,
            SourceDocumentId = request.SourceDocumentId,
            IsAutoGenerated = true,
            Lines = lines
        };
    }

    public async Task<JournalEntry> ReverseAsync(
        long journalEntryId, DateOnly reversalDate, string reason, CancellationToken cancellationToken = default)
    {
        var requested = await db.JournalEntries.FirstOrDefaultAsync(e => e.Id == journalEntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(JournalEntry), journalEntryId);

        // Every entry the document's posting produced goes: undoing only the revenue entry of a sale
        // would leave its cost of sales standing.
        var groupIds = requested.PostingGroupId is { } groupId
            ? await db.JournalEntries
                .Where(e => e.PostingGroupId == groupId && e.ReversalOfEntryId == null)
                .OrderBy(e => e.Id)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken)
            : [journalEntryId];

        var reversalGroupId = Guid.NewGuid();
        JournalEntry? result = null;
        foreach (var id in groupIds)
        {
            var reversal = await ReverseOneAsync(id, reversalDate, reason, reversalGroupId, cancellationToken);
            if (id == journalEntryId)
            {
                result = reversal;
            }
        }

        return result!;
    }

    private async Task<JournalEntry> ReverseOneAsync(
        long journalEntryId, DateOnly reversalDate, string reason, Guid reversalGroupId, CancellationToken cancellationToken)
    {
        var existing = await db.JournalEntries
            .FirstOrDefaultAsync(e => e.ReversalOfEntryId == journalEntryId, cancellationToken);
        if (existing is not null)
        {
            // A manual reversal someone started from the journal screen and never posted: returning
            // it would cancel the document while its entry still stands.
            return existing.Status == JournalEntryStatus.Posted
                ? existing
                : throw new BusinessRuleException(
                    "POST-REVERSAL-PENDING",
                    $"فيه قيد عكسي ({existing.EntryNumber}) للقيد ده لسه مش مرحّل — رحّله أو ارفضه الأول.");
        }

        var original = await db.JournalEntries
            .Include(e => e.Lines).ThenInclude(l => l.DimensionValues)
            .FirstOrDefaultAsync(e => e.Id == journalEntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(JournalEntry), journalEntryId);

        if (original.Status != JournalEntryStatus.Posted)
        {
            throw new BusinessRuleException("POST-REVERSAL-NOT-POSTED", $"القيد {original.EntryNumber} مش مرحّل — مفيش حاجة تتعكس.");
        }

        // Not IPostingService.ReverseAsync: that one leaves a Draft for someone to post by hand and
        // relabels the source as Manual. Cancelling a document is itself the decision, so the mirror
        // entry posts now, stays attributed to the document, and runs the same checks as any posting
        // (a closed period on the reversal date stops the cancellation).
        var result = await postingService.PostAsync(new PostingRequest
        {
            CompanyId = original.CompanyId!.Value,
            BranchId = original.BranchId,
            EntryDate = reversalDate,
            Description = $"{original.Description} — عكس قيد رقم {original.EntryNumber} ({reason})",
            SourceModule = original.SourceModule,
            SourceDocumentType = original.SourceDocumentType,
            SourceDocumentId = original.SourceDocumentId,
            IsAutoGenerated = true,
            ReversalOfEntryId = original.Id,
            PostingGroupId = original.PostingGroupId is null ? null : reversalGroupId,
            Lines = original.Lines.OrderBy(l => l.LineNumber).Select(l => new PostingLineRequest
            {
                AccountId = l.AccountId,
                DebitAmount = l.CreditAmount,
                CreditAmount = l.DebitAmount,
                CurrencyCode = l.CurrencyCode,
                ExchangeRate = l.ExchangeRate,
                BaseCurrencyDebitAmount = l.BaseCurrencyCreditAmount,
                BaseCurrencyCreditAmount = l.BaseCurrencyDebitAmount,
                Description = l.Description,
                DimensionValues = l.DimensionValues.ToDictionary(v => v.CostCenterDimensionId, v => v.CostCenterDimensionValueId)
            }).ToList()
        }, cancellationToken);

        return result.JournalEntry;
    }

    private static PostingLineRequest BuildLine(
        PostingTemplateLine line, long accountId, decimal amount, Dictionary<long, long> dimensions, string currencyCode,
        TemplatePostingRequest request)
    {
        var baseAmount = Math.Round(amount * request.ExchangeRate, 2);
        var isDebit = line.Direction == PostingDirection.Debit;

        return new PostingLineRequest
        {
            AccountId = accountId,
            DebitAmount = isDebit ? amount : 0m,
            CreditAmount = isDebit ? 0m : amount,
            CurrencyCode = currencyCode,
            ExchangeRate = request.ExchangeRate,
            BaseCurrencyDebitAmount = isDebit ? baseAmount : 0m,
            BaseCurrencyCreditAmount = isDebit ? 0m : baseAmount,
            Description = line.LineDescription ?? request.Description,
            DimensionValues = new Dictionary<long, long>(dimensions)
        };
    }

    private async Task<TemplatePostingResult?> FindReplayAsync(TemplatePostingRequest request, CancellationToken cancellationToken)
    {
        var snapshots = await db.JournalEntryTemplateSnapshots
            .Include(s => s.JournalEntry)
            .Where(s => s.RequestIdempotencyKey == request.IdempotencyKey)
            .OrderBy(s => s.JournalEntryId)
            .ToListAsync(cancellationToken);

        if (snapshots.Count == 0)
        {
            return null;
        }

        // Reusing one key for two different documents would silently hand the second document the
        // first one's entries. That is a caller bug, and it has to be loud.
        if (snapshots.Any(s => s.JournalEntry!.SourceDocumentId != request.SourceDocumentId
                || s.JournalEntry.SourceModule != request.SourceModule))
        {
            throw new BusinessRuleException(
                "POST-IDEMPOTENCY-KEY-REUSED",
                "مفتاح منع التكرار ده اتستخدم قبل كده لمستند تاني.");
        }

        return new TemplatePostingResult
        {
            Entries = snapshots.Select(s => new PostedTemplateEntry(s.JournalEntry!, s.PostingTemplateId, s.TemplateVersionNumber)).ToList(),
            PostingGroupId = snapshots[0].PostingGroupId,
            IsReplay = true
        };
    }

    private async Task<long?> ResolveAccountAsync(PostingTemplateLine line, PostingContext context, CancellationToken cancellationToken) =>
        line.AccountSourceType switch
        {
            AccountSourceType.Fixed => line.FixedAccountId,
            AccountSourceType.FromDocument => context.GetLong(line.AccountFieldName!),
            AccountSourceType.FromCompany => await CompanyAccountLookup.FindAsync(
                db, Enum.Parse<CompanyAccountRole>(line.AccountResolverKey!), cancellationToken),
            AccountSourceType.Resolver => await (resolvers.FindAccountResolver(line.AccountResolverKey!)
                    ?? throw new BusinessRuleException("POST-RESOLVER-MISSING", $"الـResolver '{line.AccountResolverKey}' مش مسجّل."))
                .ResolveAsync(context, cancellationToken),
            _ => null
        };

    private async Task<long?> ResolveCostCenterAsync(
        PostingTemplateLineCostCenter costCenter, PostingContext context, CancellationToken cancellationToken)
    {
        switch (costCenter.SourceType)
        {
            case CostCenterSourceType.Fixed:
                return costCenter.FixedValueId;

            case CostCenterSourceType.Dynamic:
                return await (resolvers.FindCostCenterResolver(costCenter.ValueResolverKey!)
                        ?? throw new BusinessRuleException("POST-RESOLVER-MISSING", $"الـResolver '{costCenter.ValueResolverKey}' مش مسجّل."))
                    .ResolveAsync(context, costCenter.CostCenterDimensionId, cancellationToken);
        }

        long? entityId;
        if (costCenter.SourceType == CostCenterSourceType.FromRelatedEntity)
        {
            var related = RelatedEntityCatalog.Find(costCenter.RelatedEntityType)
                ?? throw new BusinessRuleException("POST-RELATED-ENTITY-UNKNOWN", $"الكيان '{costCenter.RelatedEntityType}' مش معروف.");
            var relatedId = context.GetLong(related.IdField);
            entityId = relatedId is null ? null : await related.Read(db, relatedId.Value, costCenter.RelatedEntityField!, cancellationToken);
        }
        else
        {
            entityId = context.GetLong(costCenter.SourceType == CostCenterSourceType.FromContext
                ? costCenter.ContextKey!
                : costCenter.ValueFieldName!);
        }

        if (entityId is null)
        {
            return null;
        }

        var dimension = db.CostCenterDimensions.Local.FirstOrDefault(d => d.Id == costCenter.CostCenterDimensionId)
            ?? await db.CostCenterDimensions.FirstOrDefaultAsync(d => d.Id == costCenter.CostCenterDimensionId, cancellationToken);

        return dimension is null ? null : await PostingEntityValueMapper.MapAsync(db, dimension, entityId.Value, cancellationToken);
    }

    private async Task<string> BaseCurrencyAsync(long companyId, CancellationToken cancellationToken) =>
        await db.Companies
            .Where(c => c.Id == companyId)
            .Select(c => c.BaseCurrency.Code)
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new BusinessRuleException("POST-COMPANY-NO-CURRENCY", "الشركة مالهاش عملة أساسية محددة — القيد محتاجها.");

    private static BusinessRuleException Unresolved(PostingTemplate template, PostingTemplateLine line, string code, string what) =>
        new(code, $"{Where(template, line)}: تعذّر تحديد {what}. راجع إعداد السطر أو بيانات المستند.");

    private static string Where(PostingTemplate template, PostingTemplateLine line) =>
        $"قالب '{template.NameAr}' (إصدار {template.VersionNumber}) — السطر {line.LineNumber}";

    private static string DescribeAccountSource(PostingTemplateLine line) => line.AccountSourceType switch
    {
        AccountSourceType.FromCompany => $"الدور المحاسبي '{line.AccountResolverKey}' — لو مش مربوط، اربطه من شاشة حسابات الترحيل الافتراضية",
        AccountSourceType.Resolver => $"'{line.AccountResolverKey}'",
        AccountSourceType.FromDocument => $"من حقل '{line.AccountFieldName}'",
        _ => line.AccountSourceType.ToString()
    };

    private static string DescribeCostCenterSource(PostingTemplateLineCostCenter costCenter) => costCenter.SourceType switch
    {
        CostCenterSourceType.FromDocument => $"من حقل '{costCenter.ValueFieldName}'",
        CostCenterSourceType.FromContext => $"من '{costCenter.ContextKey}'",
        CostCenterSourceType.FromRelatedEntity => $"من {costCenter.RelatedEntityType}.{costCenter.RelatedEntityField}",
        CostCenterSourceType.Dynamic => $"'{costCenter.ValueResolverKey}'",
        _ => "قيمة ثابتة"
    };

    private static string SerializeTemplate(PostingTemplate template) =>
        JsonSerializer.Serialize(new
        {
            template.Id,
            template.FamilyId,
            template.ScreenCode,
            template.NameAr,
            template.NameEn,
            TriggerType = template.TriggerType.ToString(),
            template.TriggerFieldName,
            template.TriggerFieldValue,
            template.ExecutionOrder,
            template.VersionNumber,
            Lines = template.Lines.OrderBy(l => l.LineNumber).Select(l => new
            {
                l.LineNumber,
                Direction = l.Direction.ToString(),
                AccountSourceType = l.AccountSourceType.ToString(),
                l.FixedAccountId,
                l.AccountFieldName,
                l.AccountResolverKey,
                AmountFormulaType = l.AmountFormulaType.ToString(),
                l.AmountFieldName,
                l.AmountFieldNames,
                l.AmountMultiplier,
                l.AmountPercentage,
                ConditionType = l.ConditionType.ToString(),
                l.ConditionFieldName,
                l.ConditionFieldValue,
                l.LineDescription,
                CostCenters = l.CostCenters.OrderBy(c => c.DisplayOrder).Select(c => new
                {
                    c.CostCenterDimensionId,
                    SourceType = c.SourceType.ToString(),
                    c.FixedValueId,
                    c.ValueFieldName,
                    c.ValueResolverKey,
                    c.RelatedEntityType,
                    c.RelatedEntityField,
                    c.ContextKey
                })
            })
        });
}
