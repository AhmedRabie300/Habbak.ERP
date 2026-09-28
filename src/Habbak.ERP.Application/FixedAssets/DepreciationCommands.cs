using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.FixedAssets;
using Habbak.ERP.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.FixedAssets;

public sealed record DepreciationRunDto(
    long Id, string RunNumber, DateOnly RunDate, int Year, int Month, DepreciationRunStatus Status, decimal TotalDepreciation, int AssetCount,
    long? JournalEntryId, DateTime? PostedAtUtc, DateTime? ReversedAtUtc, long? ReversalJournalEntryId);

public sealed record DepreciationRunLineDto(
    long ScheduleId, long FixedAssetId, string AssetNumber, string AssetNameAr, int PeriodNumber, DateOnly PeriodStart, DateOnly PeriodEnd, decimal Amount);

public sealed record DepreciationRunDetailDto(DepreciationRunDto Run, IReadOnlyList<DepreciationRunLineDto> Lines);

/// <summary>What creating a run answers: the run, and whether it already existed for the month (rule 28).</summary>
public sealed record DepreciationRunResult(long Id, bool AlreadyExisted, DepreciationRunStatus Status);

internal static class DepreciationRunKeys
{
    /// <summary>The month's key (rule 28): (company, year, month) — the same on every retry and every click.</summary>
    public static Guid For(long companyId, int year, int month)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{companyId}:DepreciationRun:{year:D4}-{month:D2}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static DepreciationRunDto Map(DepreciationRun r) => new(
        r.Id, r.RunNumber, r.RunDate, r.Year, r.Month, r.Status, r.TotalDepreciation, r.AssetCount, r.JournalEntryId, r.PostedAtUtc,
        r.ReversedAtUtc, r.ReversalJournalEntryId);
}

// ------------------------------------------------------------------ queries

public sealed record GetDepreciationRunsQuery : IRequest<IReadOnlyList<DepreciationRunDto>>;

public sealed class GetDepreciationRunsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDepreciationRunsQuery, IReadOnlyList<DepreciationRunDto>>
{
    public async Task<IReadOnlyList<DepreciationRunDto>> Handle(GetDepreciationRunsQuery request, CancellationToken cancellationToken) =>
        (await db.DepreciationRuns.AsNoTracking().OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken))
        .Select(DepreciationRunKeys.Map).ToList();
}

public sealed record GetDepreciationRunQuery(long Id) : IRequest<DepreciationRunDetailDto>;

public sealed class GetDepreciationRunQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDepreciationRunQuery, DepreciationRunDetailDto>
{
    public async Task<DepreciationRunDetailDto> Handle(GetDepreciationRunQuery request, CancellationToken cancellationToken)
    {
        var run = await db.DepreciationRuns.AsNoTracking().Include(r => r.Periods).ThenInclude(p => p.FixedAsset)
                      .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                  ?? throw new NotFoundException(nameof(DepreciationRun), request.Id);

        // A reversed run released its periods back to the schedule, so it lists none — its entry and reversal stay linked.
        var lines = run.Periods
            .OrderBy(p => p.FixedAsset!.AssetNumber).ThenBy(p => p.PeriodNumber)
            .Select(p => new DepreciationRunLineDto(p.Id, p.FixedAssetId, p.FixedAsset!.AssetNumber, p.FixedAsset.NameAr, p.PeriodNumber, p.PeriodStart, p.PeriodEnd, p.Amount))
            .ToList();
        return new DepreciationRunDetailDto(DepreciationRunKeys.Map(run), lines);
    }
}

// ------------------------------------------------------------------ create (idempotent)

/// <summary>
/// Gathers the month's due periods into a Draft run. Every Scheduled period ending by the month's end
/// is picked up, so a month that was skipped is caught up. Asking again for a month that already has
/// a (non-reversed) run returns that run — never a second one (rule 28).
/// </summary>
public sealed record CreateDepreciationRunCommand(int Year, int Month) : IRequest<DepreciationRunResult>;

public sealed class CreateDepreciationRunCommandValidator : AbstractValidator<CreateDepreciationRunCommand>
{
    public CreateDepreciationRunCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class CreateDepreciationRunCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codes)
    : IRequestHandler<CreateDepreciationRunCommand, DepreciationRunResult>
{
    private static readonly FixedAssetStatus[] Depreciating = [FixedAssetStatus.Active, FixedAssetStatus.InMaintenance, FixedAssetStatus.Transferred];

    public async Task<DepreciationRunResult> Handle(CreateDepreciationRunCommand request, CancellationToken cancellationToken)
    {
        var key = DepreciationRunKeys.For(current.CompanyId, request.Year, request.Month);
        if (await ExistingAsync(key, cancellationToken) is { } existing)
        {
            return existing;
        }

        var monthEnd = new DateOnly(request.Year, request.Month, DateTime.DaysInMonth(request.Year, request.Month));
        var due = await db.DepreciationSchedules
            .Where(s => s.Status == DepreciationScheduleStatus.Scheduled && s.DepreciationRunId == null && s.PeriodEnd <= monthEnd
                        && Depreciating.Contains(s.FixedAsset!.Status))
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            throw new BusinessRuleException("FA-RUN-NOTHING-DUE", $"مفيش إهلاك مستحق لحد شهر {request.Month}/{request.Year}.");
        }

        var run = new DepreciationRun
        {
            CompanyId = current.CompanyId,
            RunNumber = await codes.ResolveCodeAsync(FixedAssetScreens.DepreciationRuns, null, cancellationToken),
            RunDate = monthEnd,
            Year = request.Year,
            Month = request.Month,
            IdempotencyKey = key,
            TotalDepreciation = due.Sum(s => s.Amount),
            AssetCount = due.Select(s => s.FixedAssetId).Distinct().Count()
        };
        foreach (var period in due)
        {
            run.Periods.Add(period);
        }

        db.DepreciationRuns.Add(run);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two clicks (or the job and a user) raced for the month: the unique key let only one through.
            throw new BusinessRuleException("FA-RUN-EXISTS", "فيه تشغيل إهلاك للشهر ده اتعمل في نفس اللحظة — حدّث الشاشة.");
        }

        return new DepreciationRunResult(run.Id, false, run.Status);
    }

    private async Task<DepreciationRunResult?> ExistingAsync(Guid key, CancellationToken ct) =>
        await db.DepreciationRuns.AsNoTracking()
            .Where(r => r.IdempotencyKey == key && r.Status != DepreciationRunStatus.Reversed)
            .Select(r => new DepreciationRunResult(r.Id, true, r.Status))
            .FirstOrDefaultAsync(ct);
}

// ------------------------------------------------------------------ post / reverse / delete

/// <summary>
/// Posts a Draft run as ONE entry (rule 22): a debit and a credit line per asset on its category's
/// accounts, each carrying that asset's cost center (rule 27). Posting a run that is already posted
/// is refused; the entry's own idempotency key would return the same entry anyway.
/// </summary>
public sealed record PostDepreciationRunCommand(long Id) : IRequest;

public sealed class PostDepreciationRunCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, IPostingTemplateEngine posting)
    : IRequestHandler<PostDepreciationRunCommand>
{
    public async Task Handle(PostDepreciationRunCommand request, CancellationToken cancellationToken)
    {
        var run = await db.DepreciationRuns
                      .Include(r => r.Periods).ThenInclude(p => p.FixedAsset).ThenInclude(a => a!.Category)
                      .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                  ?? throw new NotFoundException(nameof(DepreciationRun), request.Id);
        if (run.Status != DepreciationRunStatus.Draft)
        {
            throw new BusinessRuleException("FA-RUN-ALREADY-POSTED", $"تشغيل الإهلاك {run.RunNumber} مترحّل قبل كده — مفيش ترحيل تاني لنفس الشهر.");
        }

        if (run.Periods.FirstOrDefault(p => p.Status != DepreciationScheduleStatus.Scheduled
                                            || p.FixedAsset!.Status is FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff) is { } stale)
        {
            throw new BusinessRuleException(
                "FA-RUN-STALE", $"الأصل {stale.FixedAsset!.AssetNumber} اتغيّر بعد إنشاء التشغيل — احذف التشغيل واعمله من جديد.");
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var perAsset = run.Periods.GroupBy(p => p.FixedAsset!).Select(g => (Asset: g.Key, Amount: g.Sum(p => p.Amount))).ToList();

        if (await posting.IsConfiguredAsync(current.CompanyId, FixedAssetScreens.DepreciationRuns, cancellationToken))
        {
            var expense = new List<PostingGroupItem>();
            var accumulated = new List<PostingGroupItem>();
            foreach (var (asset, amount) in perAsset.Where(x => x.Amount > 0))
            {
                var costCenter = await FixedAssetRules.CostCenterAsync(db, asset, cancellationToken);
                expense.Add(new PostingGroupItem(asset.Category!.DepreciationExpenseAccountId, amount, costCenter));
                accumulated.Add(new PostingGroupItem(asset.Category.AccumulatedDepreciationAccountId, amount, costCenter));
            }

            run.JournalEntry = await posting.PostIfConfiguredAsync(new TemplatePostingRequest
            {
                CompanyId = current.CompanyId,
                ScreenCode = FixedAssetScreens.DepreciationRuns,
                SourceModule = SourceModule.FixedAssets,
                SourceDocumentType = SourceDocumentType.Depreciation,
                SourceDocumentId = run.Id,
                EntryDate = run.RunDate,
                Description = $"إهلاك شهر {run.Month}/{run.Year} — {run.RunNumber}",
                IdempotencyKey = PostingKeys.For(current.CompanyId, "DepreciationRun.Post", run.Id),
                Context = PostingContext.Create(
                    new Dictionary<string, object?> { ["TotalDepreciation"] = run.TotalDepreciation },
                    groups: new Dictionary<string, IReadOnlyList<PostingGroupItem>>
                    {
                        [PostingScreenCatalog.DepreciationExpenseGroup] = expense,
                        [PostingScreenCatalog.AccumulatedDepreciationGroup] = accumulated
                    })
            }, cancellationToken);
        }

        var now = DateTime.UtcNow;
        foreach (var period in run.Periods)
        {
            period.Status = DepreciationScheduleStatus.Posted;
            period.PostedAtUtc = now;
            period.JournalEntry = run.JournalEntry;
        }

        foreach (var (asset, amount) in perAsset)
        {
            asset.AccumulatedDepreciation += amount;
        }

        run.Status = DepreciationRunStatus.Posted;
        run.PostedAtUtc = now;
        run.PostedByUserId = current.UserId;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

/// <summary>
/// Undoes a posted run: reverses its entry, puts its periods back on the schedule and takes the
/// amounts off the assets. Refused once an asset of the run was disposed of, or a later month of
/// one of its assets is already posted — undo that one first.
/// </summary>
public sealed record ReverseDepreciationRunCommand(long Id, string Reason) : IRequest;

public sealed class ReverseDepreciationRunCommandValidator : AbstractValidator<ReverseDepreciationRunCommand>
{
    public ReverseDepreciationRunCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
}

public sealed class ReverseDepreciationRunCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, IPostingTemplateEngine posting)
    : IRequestHandler<ReverseDepreciationRunCommand>
{
    public async Task Handle(ReverseDepreciationRunCommand request, CancellationToken cancellationToken)
    {
        var run = await db.DepreciationRuns.Include(r => r.Periods).ThenInclude(p => p.FixedAsset)
                      .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                  ?? throw new NotFoundException(nameof(DepreciationRun), request.Id);
        if (run.Status != DepreciationRunStatus.Posted)
        {
            throw new BusinessRuleException("FA-RUN-NOT-POSTED", "التشغيل مش مترحّل.");
        }

        if (run.Periods.FirstOrDefault(p => p.FixedAsset!.Status is FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff) is { } disposed)
        {
            throw new BusinessRuleException("FA-RUN-ASSET-DISPOSED", $"الأصل {disposed.FixedAsset!.AssetNumber} اتستبعد — مينفعش نلغي إهلاكه.");
        }

        var assetIds = run.Periods.Select(p => p.FixedAssetId).Distinct().ToList();
        var lastByAsset = run.Periods.GroupBy(p => p.FixedAssetId).ToDictionary(g => g.Key, g => g.Max(p => p.PeriodNumber));
        var later = await db.DepreciationSchedules
            .Where(s => assetIds.Contains(s.FixedAssetId) && s.Status == DepreciationScheduleStatus.Posted && s.DepreciationRunId != run.Id)
            .Select(s => new { s.FixedAssetId, s.PeriodNumber })
            .ToListAsync(cancellationToken);
        if (later.Any(s => s.PeriodNumber > lastByAsset[s.FixedAssetId]))
        {
            throw new BusinessRuleException("FA-RUN-LATER-POSTED", "فيه تشغيل إهلاك لشهر بعده مترحّل لنفس الأصول — الغيه الأول.");
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        if (run.JournalEntryId is { } entryId)
        {
            run.ReversalJournalEntry = await posting.ReverseAsync(
                entryId, DateOnly.FromDateTime(DateTime.UtcNow) is var today && today > run.RunDate ? today : run.RunDate,
                $"إلغاء إهلاك {run.RunNumber}: {request.Reason}", cancellationToken);
        }

        foreach (var group in run.Periods.GroupBy(p => p.FixedAsset!))
        {
            group.Key.AccumulatedDepreciation -= group.Sum(p => p.Amount);
        }

        foreach (var period in run.Periods.ToList())
        {
            period.Status = DepreciationScheduleStatus.Scheduled;
            period.PostedAtUtc = null;
            period.JournalEntryId = null;
            period.DepreciationRunId = null;
        }

        run.Status = DepreciationRunStatus.Reversed;
        run.ReversedAtUtc = DateTime.UtcNow;
        run.ReversedByUserId = current.UserId;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}

/// <summary>A Draft run can be thrown away; its periods go back to waiting.</summary>
public sealed record DeleteDepreciationRunCommand(long Id) : IRequest;

public sealed class DeleteDepreciationRunCommandHandler(IApplicationDbContext db) : IRequestHandler<DeleteDepreciationRunCommand>
{
    public async Task Handle(DeleteDepreciationRunCommand request, CancellationToken cancellationToken)
    {
        var run = await db.DepreciationRuns.Include(r => r.Periods).FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                  ?? throw new NotFoundException(nameof(DepreciationRun), request.Id);
        if (run.Status != DepreciationRunStatus.Draft)
        {
            throw new BusinessRuleException("FA-RUN-NOT-DRAFT", "التشغيل المترحّل مابيتحذفش — اعمله إلغاء.");
        }

        foreach (var period in run.Periods.ToList())
        {
            period.DepreciationRunId = null;
        }

        db.DepreciationRuns.Remove(run);
        await db.SaveChangesAsync(cancellationToken);
    }
}

// ------------------------------------------------------------------ the job's entry point

/// <summary>
/// What the daily job asks for each company (section 3.1): when automatic depreciation is on and the
/// run day has come, create the month's run — or find it (rule 28) — and post it if still a Draft.
/// Running it twice the same day, or on every day after the run day, never posts twice.
/// </summary>
public sealed record RunDueDepreciationCommand(DateOnly Today) : IRequest<string>;

public sealed class RunDueDepreciationCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, ISender sender)
    : IRequestHandler<RunDueDepreciationCommand, string>
{
    public async Task<string> Handle(RunDueDepreciationCommand request, CancellationToken cancellationToken)
    {
        var settings = await FixedAssetRules.SettingsAsync(db, current.CompanyId, cancellationToken);
        if (!settings.AutoDepreciationEnabled)
        {
            return "disabled";
        }

        var today = request.Today;
        var runDay = Math.Min(Math.Max(settings.DepreciationRunDay, 1), DateTime.DaysInMonth(today.Year, today.Month));
        if (today.Day < runDay)
        {
            return "not-due";
        }

        var key = DepreciationRunKeys.For(current.CompanyId, today.Year, today.Month);
        var existing = await db.DepreciationRuns.AsNoTracking()
            .Where(r => r.IdempotencyKey == key && r.Status != DepreciationRunStatus.Reversed)
            .Select(r => new { r.Id, r.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (existing?.Status == DepreciationRunStatus.Posted)
        {
            return "already-posted";
        }

        long runId;
        if (existing is not null)
        {
            runId = existing.Id;
        }
        else
        {
            var monthEnd = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
            var anyDue = await db.DepreciationSchedules.AnyAsync(
                s => s.Status == DepreciationScheduleStatus.Scheduled && s.DepreciationRunId == null && s.PeriodEnd <= monthEnd
                     && (s.FixedAsset!.Status == FixedAssetStatus.Active || s.FixedAsset.Status == FixedAssetStatus.InMaintenance
                         || s.FixedAsset.Status == FixedAssetStatus.Transferred),
                cancellationToken);
            if (!anyDue)
            {
                return "nothing-due";
            }

            runId = (await sender.Send(new CreateDepreciationRunCommand(today.Year, today.Month), cancellationToken)).Id;
        }

        await sender.Send(new PostDepreciationRunCommand(runId), cancellationToken);
        return "posted";
    }
}
