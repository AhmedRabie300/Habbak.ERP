using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.FixedAssets;
using Habbak.ERP.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.FixedAssets;

// ================================================================== transfers (screen #5)

public sealed record AssetTransferDto(
    long Id, string TransferNumber, long FixedAssetId, string AssetNumber, string AssetNameAr, long? FromBranchId, string? FromBranchNameAr,
    long ToBranchId, string ToBranchNameAr, DateOnly TransferDate, string? Reason, long CustodyOfficerId, string CustodyOfficerName,
    AssetTransferStatus Status, DateTime? PostedAtUtc, string? Notes);

public sealed record GetAssetTransfersQuery(long? FixedAssetId = null) : IRequest<IReadOnlyList<AssetTransferDto>>;

public sealed class GetAssetTransfersQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAssetTransfersQuery, IReadOnlyList<AssetTransferDto>>
{
    public async Task<IReadOnlyList<AssetTransferDto>> Handle(GetAssetTransfersQuery request, CancellationToken cancellationToken) =>
        await db.AssetTransfers.AsNoTracking()
            .Where(t => request.FixedAssetId == null || t.FixedAssetId == request.FixedAssetId)
            .OrderByDescending(t => t.TransferDate).ThenByDescending(t => t.Id)
            .Select(t => new AssetTransferDto(
                t.Id, t.TransferNumber, t.FixedAssetId, t.FixedAsset!.AssetNumber, t.FixedAsset.NameAr, t.FromBranchId,
                t.FromBranch != null ? t.FromBranch.NameAr : null, t.ToBranchId, t.ToBranch!.NameAr, t.TransferDate, t.Reason,
                t.CustodyOfficerId, t.CustodyOfficerNameSnapshot, t.Status, t.PostedAtUtc, t.Notes))
            .ToListAsync(cancellationToken);
}

/// <summary>
/// Drafts a transfer: the asset shows as "in transfer" until it is posted. The custody officer is the
/// Inventory module's (a real key), with the name kept as it is today.
/// </summary>
public sealed record CreateAssetTransferCommand(
    long FixedAssetId, long ToBranchId, DateOnly TransferDate, string? Reason, long CustodyOfficerId, string? Notes) : IRequest<long>;

public sealed class CreateAssetTransferCommandValidator : AbstractValidator<CreateAssetTransferCommand>
{
    public CreateAssetTransferCommandValidator()
    {
        RuleFor(x => x.FixedAssetId).GreaterThan(0);
        RuleFor(x => x.ToBranchId).GreaterThan(0);
        RuleFor(x => x.CustodyOfficerId).GreaterThan(0);
        RuleFor(x => x.TransferDate).NotEqual(default(DateOnly));
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed record PostAssetTransferCommand(long Id) : IRequest;
public sealed record RejectAssetTransferCommand(long Id) : IRequest;
public sealed record CancelAssetTransferCommand(long Id) : IRequest;

public sealed class AssetTransferCommandsHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codes, IUserAccessService access)
    : IRequestHandler<CreateAssetTransferCommand, long>, IRequestHandler<PostAssetTransferCommand>,
      IRequestHandler<RejectAssetTransferCommand>, IRequestHandler<CancelAssetTransferCommand>
{
    public async Task<long> Handle(CreateAssetTransferCommand request, CancellationToken cancellationToken)
    {
        var asset = await FixedAssetRules.FindAssetAsync(db, request.FixedAssetId, cancellationToken);
        if (asset.Status != FixedAssetStatus.Active)
        {
            throw new BusinessRuleException("FA-TRANSFER-ASSET-NOT-ACTIVE", "النقل متاح للأصل النشط بس (مش مسودة ولا في صيانة ولا في نقل تاني ولا مستبعد).");
        }

        if (asset.BranchId == request.ToBranchId)
        {
            throw new BusinessRuleException("FA-TRANSFER-SAME-BRANCH", "الأصل موجود في الفرع ده أصلاً.");
        }

        if (!await db.Branches.AnyAsync(b => b.Id == request.ToBranchId, cancellationToken))
        {
            throw new NotFoundException("Branch", request.ToBranchId);
        }

        var officer = await db.CustodyOfficers.FirstOrDefaultAsync(o => o.Id == request.CustodyOfficerId, cancellationToken)
                      ?? throw new NotFoundException("CustodyOfficer", request.CustodyOfficerId);

        var transfer = new AssetTransfer
        {
            CompanyId = current.CompanyId,
            TransferNumber = await codes.ResolveCodeAsync(FixedAssetScreens.Transfers, null, cancellationToken),
            FixedAssetId = asset.Id,
            FromBranchId = asset.BranchId,
            ToBranchId = request.ToBranchId,
            TransferDate = request.TransferDate,
            Reason = request.Reason,
            CustodyOfficerId = officer.Id,
            CustodyOfficerNameSnapshot = officer.NameAr,
            Notes = request.Notes
        };
        asset.Status = FixedAssetStatus.Transferred;
        db.AssetTransfers.Add(transfer);
        await db.SaveChangesAsync(cancellationToken);
        return transfer.Id;
    }

    /// <summary>Moves the asset to the new branch and hands it to the officer. The depreciation schedule is untouched (rule 33).</summary>
    public async Task Handle(PostAssetTransferCommand request, CancellationToken cancellationToken)
    {
        var (transfer, asset) = await DraftAsync(request.Id, cancellationToken);
        var settings = await FixedAssetRules.SettingsAsync(db, current.CompanyId, cancellationToken);
        await FixedAssetRules.EnsureApproverAsync(access, settings.RequireApprovalForTransfer, FixedAssetScreens.Transfers, cancellationToken);

        asset.BranchId = transfer.ToBranchId;
        asset.CustodyOfficerId = transfer.CustodyOfficerId;
        asset.CustodyOfficerName = transfer.CustodyOfficerNameSnapshot;
        asset.Status = FixedAssetStatus.Active;
        transfer.Status = AssetTransferStatus.Posted;
        transfer.PostedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(RejectAssetTransferCommand request, CancellationToken cancellationToken) =>
        await CloseAsync(request.Id, AssetTransferStatus.Rejected, cancellationToken);

    public async Task Handle(CancelAssetTransferCommand request, CancellationToken cancellationToken) =>
        await CloseAsync(request.Id, AssetTransferStatus.Cancelled, cancellationToken);

    private async Task CloseAsync(long id, AssetTransferStatus status, CancellationToken ct)
    {
        var (transfer, asset) = await DraftAsync(id, ct);
        transfer.Status = status;
        asset.Status = FixedAssetStatus.Active;
        await db.SaveChangesAsync(ct);
    }

    private async Task<(AssetTransfer Transfer, FixedAsset Asset)> DraftAsync(long id, CancellationToken ct)
    {
        // The asset may sit in a branch outside the user's scope once it moved; the transfer itself is company-wide.
        var transfer = await db.AssetTransfers.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException(nameof(AssetTransfer), id);
        if (transfer.Status != AssetTransferStatus.Draft)
        {
            throw new BusinessRuleException("FA-TRANSFER-NOT-DRAFT", "النقل ده اتقفل قبل كده.");
        }

        return (transfer, await FixedAssetRules.FindAssetAsync(db, transfer.FixedAssetId, ct));
    }
}

// ================================================================== disposals (screen #6)

public sealed record AssetDisposalDto(
    long Id, string DisposalNumber, long FixedAssetId, string AssetNumber, string AssetNameAr, DateOnly DisposalDate, DisposalType DisposalType,
    decimal? Proceeds, long? ProceedsAccountId, string? BuyerName, decimal CostAtDisposal, decimal AccumulatedAtDisposal,
    decimal BookValueAtDisposal, decimal GainOrLoss, AssetDisposalStatus Status, long? JournalEntryId, DateTime? PostedAtUtc, string? Notes);

public sealed record GetAssetDisposalsQuery(long? FixedAssetId = null) : IRequest<IReadOnlyList<AssetDisposalDto>>;

public sealed class GetAssetDisposalsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAssetDisposalsQuery, IReadOnlyList<AssetDisposalDto>>
{
    public async Task<IReadOnlyList<AssetDisposalDto>> Handle(GetAssetDisposalsQuery request, CancellationToken cancellationToken) =>
        await db.AssetDisposals.AsNoTracking()
            .Where(d => request.FixedAssetId == null || d.FixedAssetId == request.FixedAssetId)
            .OrderByDescending(d => d.DisposalDate).ThenByDescending(d => d.Id)
            .Select(d => new AssetDisposalDto(
                d.Id, d.DisposalNumber, d.FixedAssetId, d.FixedAsset!.AssetNumber, d.FixedAsset.NameAr, d.DisposalDate, d.DisposalType,
                d.Proceeds, d.ProceedsAccountId, d.BuyerName,
                // A draft shows today's figures; a posted one what it was posted at.
                d.Status == AssetDisposalStatus.Posted ? d.CostAtDisposal : d.FixedAsset.BaseCurrencyAmount,
                d.Status == AssetDisposalStatus.Posted ? d.AccumulatedAtDisposal : d.FixedAsset.AccumulatedDepreciation,
                d.Status == AssetDisposalStatus.Posted ? d.BookValueAtDisposal : d.FixedAsset.BaseCurrencyAmount - d.FixedAsset.AccumulatedDepreciation,
                d.Status == AssetDisposalStatus.Posted
                    ? d.GainOrLoss
                    : (d.Proceeds ?? 0m) - (d.FixedAsset.BaseCurrencyAmount - d.FixedAsset.AccumulatedDepreciation),
                d.Status, d.JournalEntryId, d.PostedAtUtc, d.Notes))
            .ToListAsync(cancellationToken);
}

public sealed record CreateAssetDisposalCommand(
    long FixedAssetId, DateOnly DisposalDate, DisposalType DisposalType, decimal? Proceeds, long? ProceedsAccountId, string? BuyerName, string? Notes)
    : IRequest<long>;

public sealed class CreateAssetDisposalCommandValidator : AbstractValidator<CreateAssetDisposalCommand>
{
    public CreateAssetDisposalCommandValidator()
    {
        RuleFor(x => x.FixedAssetId).GreaterThan(0);
        RuleFor(x => x.DisposalDate).NotEqual(default(DateOnly));
        RuleFor(x => x.DisposalType).IsInEnum();
        RuleFor(x => x.Proceeds).GreaterThanOrEqualTo(0).When(x => x.Proceeds is not null);
        RuleFor(x => x.Proceeds).Must(p => p is null or 0).When(x => x.DisposalType == DisposalType.Loss)
            .WithMessage("الأصل المفقود مالوش محصَّل.");
        RuleFor(x => x.ProceedsAccountId).NotNull().When(x => x.Proceeds > 0)
            .WithMessage("حدد الحساب اللي دخل فيه المحصَّل (خزينة أو مدين المشتري).");
    }
}

public sealed record PostAssetDisposalCommand(long Id) : IRequest;
public sealed record RejectAssetDisposalCommand(long Id) : IRequest;
public sealed record CancelAssetDisposalCommand(long Id) : IRequest;

public sealed class AssetDisposalCommandsHandler(
    IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codes, IUserAccessService access, IPostingTemplateEngine posting)
    : IRequestHandler<CreateAssetDisposalCommand, long>, IRequestHandler<PostAssetDisposalCommand>,
      IRequestHandler<RejectAssetDisposalCommand>, IRequestHandler<CancelAssetDisposalCommand>
{
    public async Task<long> Handle(CreateAssetDisposalCommand request, CancellationToken cancellationToken)
    {
        var asset = await FixedAssetRules.FindAssetAsync(db, request.FixedAssetId, cancellationToken);
        if (asset.Status is not (FixedAssetStatus.Active or FixedAssetStatus.InMaintenance))
        {
            throw new BusinessRuleException("FA-DISPOSAL-ASSET-NOT-ACTIVE", "الاستبعاد متاح للأصل النشط أو اللي في الصيانة بس.");
        }

        if (await db.AssetDisposals.AnyAsync(d => d.FixedAssetId == asset.Id && d.Status == AssetDisposalStatus.Draft, cancellationToken))
        {
            throw new BusinessRuleException("FA-DISPOSAL-OPEN", "فيه استبعاد مسودة للأصل ده بالفعل.");
        }

        var disposal = new AssetDisposal
        {
            CompanyId = current.CompanyId,
            DisposalNumber = await codes.ResolveCodeAsync(FixedAssetScreens.Disposals, null, cancellationToken),
            FixedAssetId = asset.Id,
            DisposalDate = request.DisposalDate,
            DisposalType = request.DisposalType,
            Proceeds = request.Proceeds is > 0 ? request.Proceeds : null,
            ProceedsAccountId = request.Proceeds is > 0 ? request.ProceedsAccountId : null,
            BuyerName = request.BuyerName,
            Notes = request.Notes
        };
        db.AssetDisposals.Add(disposal);
        await db.SaveChangesAsync(cancellationToken);
        return disposal.Id;
    }

    /// <summary>
    /// Takes the asset off the books (section 2.1.6): the cost and depreciation so far out, the
    /// proceeds in, the difference to gain or loss — and cancels the periods not yet depreciated (rule 32).
    /// The months before the disposal's month must be depreciated first.
    /// </summary>
    public async Task Handle(PostAssetDisposalCommand request, CancellationToken cancellationToken)
    {
        var disposal = await DraftAsync(request.Id, cancellationToken);
        var settings = await FixedAssetRules.SettingsAsync(db, current.CompanyId, cancellationToken);
        await FixedAssetRules.EnsureApproverAsync(access, settings.RequireApprovalForDisposal, FixedAssetScreens.Disposals, cancellationToken);

        var asset = await FixedAssetRules.FindAssetAsync(db, disposal.FixedAssetId, cancellationToken);
        if (asset.Status is not (FixedAssetStatus.Active or FixedAssetStatus.InMaintenance))
        {
            throw new BusinessRuleException("FA-DISPOSAL-ASSET-NOT-ACTIVE", "الأصل مش نشط دلوقتي (في نقل أو مستبعد).");
        }

        var schedule = await db.DepreciationSchedules.Where(s => s.FixedAssetId == asset.Id).ToListAsync(cancellationToken);
        var monthStart = new DateOnly(disposal.DisposalDate.Year, disposal.DisposalDate.Month, 1);
        if (schedule.Any(s => s.Status == DepreciationScheduleStatus.Posted && s.PeriodEnd >= monthStart.AddMonths(1)))
        {
            throw new BusinessRuleException("FA-DISPOSAL-AFTER-DEPRECIATION", "فيه إهلاك مترحّل بعد شهر الاستبعاد — الغي تشغيل الإهلاك ده الأول أو غيّر التاريخ.");
        }

        if (schedule.Any(s => s.Status == DepreciationScheduleStatus.Scheduled && s.PeriodEnd < monthStart))
        {
            throw new BusinessRuleException("FA-DISPOSAL-DEPRECIATION-DUE", "فيه شهور إهلاك قبل شهر الاستبعاد لسه مترحّلتش — شغّل الإهلاك الأول.");
        }

        if (schedule.Any(s => s.Status == DepreciationScheduleStatus.Scheduled && s.DepreciationRunId != null))
        {
            throw new BusinessRuleException("FA-DISPOSAL-RUN-PENDING", "الأصل داخل في تشغيل إهلاك مسودة — رحّله أو احذفه الأول.");
        }

        var cost = asset.BaseCurrencyAmount;
        var accumulated = asset.AccumulatedDepreciation;
        var bookValue = cost - accumulated;
        var proceeds = disposal.Proceeds ?? 0m;
        var gainOrLoss = proceeds - bookValue;

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        if (await posting.IsConfiguredAsync(current.CompanyId, FixedAssetScreens.Disposals, cancellationToken))
        {
            var category = asset.Category!;
            if (gainOrLoss > 0 && category.DisposalGainAccountId is null)
            {
                throw new BusinessRuleException("FA-CATEGORY-GAIN-ACCOUNT", $"فئة {category.NameAr} مالهاش حساب أرباح استبعاد.");
            }

            if (gainOrLoss < 0 && category.DisposalLossAccountId is null)
            {
                throw new BusinessRuleException("FA-CATEGORY-LOSS-ACCOUNT", $"فئة {category.NameAr} مالهاش حساب خسائر استبعاد.");
            }

            disposal.JournalEntry = await posting.PostIfConfiguredAsync(new TemplatePostingRequest
            {
                CompanyId = current.CompanyId,
                BranchId = asset.BranchId,
                ScreenCode = FixedAssetScreens.Disposals,
                SourceModule = SourceModule.FixedAssets,
                SourceDocumentType = SourceDocumentType.AssetDisposal,
                SourceDocumentId = disposal.Id,
                EntryDate = disposal.DisposalDate,
                Description = $"استبعاد أصل {asset.AssetNumber} — {asset.NameAr} ({disposal.DisposalNumber})",
                IdempotencyKey = PostingKeys.For(current.CompanyId, "AssetDisposal.Post", disposal.Id),
                Dimensions = await FixedAssetRules.CostCenterAsync(db, asset, cancellationToken),
                Context = PostingContext.Create(new Dictionary<string, object?>
                {
                    ["BranchId"] = asset.BranchId,
                    ["FixedAssetId"] = asset.Id,
                    ["CostAmount"] = cost,
                    ["AccumulatedDepreciation"] = accumulated,
                    ["Proceeds"] = proceeds,
                    ["Gain"] = Math.Max(gainOrLoss, 0m),
                    ["Loss"] = Math.Max(-gainOrLoss, 0m),
                    ["AssetAccountId"] = category.AssetAccountId,
                    ["AccumulatedDepreciationAccountId"] = category.AccumulatedDepreciationAccountId,
                    ["ProceedsAccountId"] = disposal.ProceedsAccountId,
                    ["GainAccountId"] = category.DisposalGainAccountId,
                    ["LossAccountId"] = category.DisposalLossAccountId
                })
            }, cancellationToken);
        }

        foreach (var period in schedule.Where(s => s.Status == DepreciationScheduleStatus.Scheduled))
        {
            period.Status = DepreciationScheduleStatus.Cancelled;   // rule 32 — kept, never deleted
        }

        disposal.CostAtDisposal = cost;
        disposal.AccumulatedAtDisposal = accumulated;
        disposal.BookValueAtDisposal = bookValue;
        disposal.GainOrLoss = gainOrLoss;
        disposal.Status = AssetDisposalStatus.Posted;
        disposal.PostedAtUtc = DateTime.UtcNow;

        asset.Status = disposal.DisposalType == DisposalType.Loss ? FixedAssetStatus.WrittenOff : FixedAssetStatus.Disposed;
        asset.DisposalDate = disposal.DisposalDate;
        asset.DisposalReason = disposal.DisposalType.ToString();
        asset.DisposalProceeds = disposal.Proceeds;
        asset.DisposalJournalEntry = disposal.JournalEntry;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task Handle(RejectAssetDisposalCommand request, CancellationToken cancellationToken)
    {
        (await DraftAsync(request.Id, cancellationToken)).Status = AssetDisposalStatus.Rejected;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(CancelAssetDisposalCommand request, CancellationToken cancellationToken)
    {
        (await DraftAsync(request.Id, cancellationToken)).Status = AssetDisposalStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AssetDisposal> DraftAsync(long id, CancellationToken ct)
    {
        var disposal = await db.AssetDisposals.FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException(nameof(AssetDisposal), id);
        if (disposal.Status != AssetDisposalStatus.Draft)
        {
            throw new BusinessRuleException("FA-DISPOSAL-NOT-DRAFT", "الاستبعاد ده اتقفل قبل كده.");
        }

        return disposal;
    }
}

// ================================================================== physical counts (screen #7)

public sealed record AssetPhysicalCountDto(
    long Id, string CountNumber, long? BranchId, string? BranchNameAr, DateOnly CountDate, AssetPhysicalCountStatus Status,
    int LineCount, int FoundCount, int MissingCount, int DamagedCount, string? Notes);

public sealed record AssetPhysicalCountLineDto(
    long Id, long FixedAssetId, string AssetNumber, string AssetNameAr, string? ExpectedLocation, string? ActualLocation,
    bool? IsFound, AssetCondition? Condition, string? Notes);

public sealed record AssetPhysicalCountDetailDto(AssetPhysicalCountDto Count, IReadOnlyList<AssetPhysicalCountLineDto> Lines);

/// <summary>When the next count is due under the settings' frequency; null when no frequency is set.</summary>
public sealed record AssetCountScheduleDto(AssetPhysicalCountFrequency? Frequency, DateOnly? LastCountDate, DateOnly? NextDueDate, bool IsOverdue);

public sealed record GetAssetPhysicalCountsQuery : IRequest<IReadOnlyList<AssetPhysicalCountDto>>;
public sealed record GetAssetPhysicalCountQuery(long Id) : IRequest<AssetPhysicalCountDetailDto>;
public sealed record GetAssetCountScheduleQuery(DateOnly Today) : IRequest<AssetCountScheduleDto>;

public sealed class AssetPhysicalCountQueriesHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<GetAssetPhysicalCountsQuery, IReadOnlyList<AssetPhysicalCountDto>>,
      IRequestHandler<GetAssetPhysicalCountQuery, AssetPhysicalCountDetailDto>,
      IRequestHandler<GetAssetCountScheduleQuery, AssetCountScheduleDto>
{
    public async Task<IReadOnlyList<AssetPhysicalCountDto>> Handle(GetAssetPhysicalCountsQuery request, CancellationToken cancellationToken) =>
        await db.AssetPhysicalCounts.AsNoTracking()
            .OrderByDescending(c => c.CountDate).ThenByDescending(c => c.Id)
            .Select(c => new AssetPhysicalCountDto(
                c.Id, c.CountNumber, c.BranchId, c.Branch != null ? c.Branch.NameAr : null, c.CountDate, c.Status,
                c.Lines.Count, c.Lines.Count(l => l.IsFound == true), c.Lines.Count(l => l.IsFound == false),
                c.Lines.Count(l => l.Condition == AssetCondition.Damaged), c.Notes))
            .ToListAsync(cancellationToken);

    public async Task<AssetPhysicalCountDetailDto> Handle(GetAssetPhysicalCountQuery request, CancellationToken cancellationToken)
    {
        var count = await db.AssetPhysicalCounts.AsNoTracking().Include(c => c.Branch).Include(c => c.Lines).ThenInclude(l => l.FixedAsset)
                        .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
                    ?? throw new NotFoundException(nameof(AssetPhysicalCount), request.Id);
        var lines = count.Lines.OrderBy(l => l.FixedAsset!.AssetNumber)
            .Select(l => new AssetPhysicalCountLineDto(
                l.Id, l.FixedAssetId, l.FixedAsset!.AssetNumber, l.FixedAsset.NameAr, l.ExpectedLocation, l.ActualLocation, l.IsFound, l.Condition, l.Notes))
            .ToList();
        return new AssetPhysicalCountDetailDto(
            new AssetPhysicalCountDto(
                count.Id, count.CountNumber, count.BranchId, count.Branch?.NameAr, count.CountDate, count.Status, lines.Count,
                lines.Count(l => l.IsFound == true), lines.Count(l => l.IsFound == false), lines.Count(l => l.Condition == AssetCondition.Damaged),
                count.Notes),
            lines);
    }

    public async Task<AssetCountScheduleDto> Handle(GetAssetCountScheduleQuery request, CancellationToken cancellationToken)
    {
        var settings = await FixedAssetRules.SettingsAsync(db, current.CompanyId, cancellationToken);
        var last = await db.AssetPhysicalCounts.Where(c => c.Status == AssetPhysicalCountStatus.Completed)
            .OrderByDescending(c => c.CountDate).Select(c => (DateOnly?)c.CountDate).FirstOrDefaultAsync(cancellationToken);
        if (settings.PhysicalCountFrequency is not { } frequency)
        {
            return new AssetCountScheduleDto(null, last, null, false);
        }

        var months = frequency switch
        {
            AssetPhysicalCountFrequency.Quarterly => 3,
            AssetPhysicalCountFrequency.SemiAnnual => 6,
            _ => 12
        };
        var next = last?.AddMonths(months) ?? request.Today;
        return new AssetCountScheduleDto(frequency, last, next, next <= request.Today);
    }
}

public sealed record CreateAssetPhysicalCountCommand(long BranchId, DateOnly CountDate, string? Notes) : IRequest<long>;
public sealed record StartAssetPhysicalCountCommand(long Id) : IRequest;
public sealed record AssetCountLineInput(long LineId, bool? IsFound, string? ActualLocation, AssetCondition? Condition, string? Notes);
public sealed record RecordAssetPhysicalCountCommand(long Id, IReadOnlyList<AssetCountLineInput> Lines) : IRequest;
public sealed record CompleteAssetPhysicalCountCommand(long Id) : IRequest;
public sealed record RejectAssetPhysicalCountCommand(long Id) : IRequest;

public sealed class CreateAssetPhysicalCountCommandValidator : AbstractValidator<CreateAssetPhysicalCountCommand>
{
    public CreateAssetPhysicalCountCommandValidator()
    {
        RuleFor(x => x.BranchId).GreaterThan(0);
        RuleFor(x => x.CountDate).NotEqual(default(DateOnly));
    }
}

public sealed class AssetPhysicalCountCommandsHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codes)
    : IRequestHandler<CreateAssetPhysicalCountCommand, long>, IRequestHandler<StartAssetPhysicalCountCommand>,
      IRequestHandler<RecordAssetPhysicalCountCommand>, IRequestHandler<CompleteAssetPhysicalCountCommand>,
      IRequestHandler<RejectAssetPhysicalCountCommand>
{
    private static readonly FixedAssetStatus[] OnSite = [FixedAssetStatus.Active, FixedAssetStatus.InMaintenance];

    /// <summary>Lists every asset the branch should have (active or in maintenance), where it is expected to be.</summary>
    public async Task<long> Handle(CreateAssetPhysicalCountCommand request, CancellationToken cancellationToken)
    {
        if (await db.AssetPhysicalCounts.AnyAsync(
                c => c.BranchId == request.BranchId && (c.Status == AssetPhysicalCountStatus.Draft || c.Status == AssetPhysicalCountStatus.InProgress),
                cancellationToken))
        {
            throw new BusinessRuleException("FA-COUNT-OPEN", "فيه جرد مفتوح للفرع ده بالفعل.");
        }

        var assets = await db.FixedAssets.Where(a => a.BranchId == request.BranchId && OnSite.Contains(a.Status))
            .OrderBy(a => a.AssetNumber).ToListAsync(cancellationToken);
        if (assets.Count == 0)
        {
            throw new BusinessRuleException("FA-COUNT-NO-ASSETS", "مفيش أصول نشطة في الفرع ده.");
        }

        var count = new AssetPhysicalCount
        {
            CompanyId = current.CompanyId,
            BranchId = request.BranchId,
            CountNumber = await codes.ResolveCodeAsync(FixedAssetScreens.PhysicalCounts, null, cancellationToken),
            CountDate = request.CountDate,
            Notes = request.Notes
        };
        foreach (var asset in assets)
        {
            count.Lines.Add(new AssetPhysicalCountLine { FixedAssetId = asset.Id, ExpectedLocation = asset.Location });
        }

        db.AssetPhysicalCounts.Add(count);
        await db.SaveChangesAsync(cancellationToken);
        return count.Id;
    }

    public async Task Handle(StartAssetPhysicalCountCommand request, CancellationToken cancellationToken)
    {
        var count = await OpenAsync(request.Id, cancellationToken);
        count.Status = AssetPhysicalCountStatus.InProgress;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(RecordAssetPhysicalCountCommand request, CancellationToken cancellationToken)
    {
        var count = await OpenAsync(request.Id, cancellationToken);
        foreach (var input in request.Lines)
        {
            var line = count.Lines.FirstOrDefault(l => l.Id == input.LineId)
                       ?? throw new NotFoundException(nameof(AssetPhysicalCountLine), input.LineId);
            line.IsFound = input.IsFound;
            line.ActualLocation = input.IsFound == true ? input.ActualLocation ?? line.ExpectedLocation : null;
            line.Condition = input.IsFound == true ? input.Condition ?? AssetCondition.Good : null;
            line.Notes = input.Notes;
        }

        count.Status = AssetPhysicalCountStatus.InProgress;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Every line must be counted. A found asset's location is updated to where it was found; a missing
    /// one stays on the books — writing it off is a disposal (type Loss), a decision of its own.
    /// </summary>
    public async Task Handle(CompleteAssetPhysicalCountCommand request, CancellationToken cancellationToken)
    {
        var count = await OpenAsync(request.Id, cancellationToken);
        if (count.Lines.Any(l => l.IsFound is null))
        {
            throw new BusinessRuleException("FA-COUNT-INCOMPLETE", "فيه أصول لسه ماتجردتش — حدد لكل أصل موجود ولا مش موجود.");
        }

        var ids = count.Lines.Select(l => l.FixedAssetId).ToList();
        var assets = await db.FixedAssets.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        foreach (var line in count.Lines.Where(l => l.IsFound == true && !string.IsNullOrWhiteSpace(l.ActualLocation)))
        {
            assets[line.FixedAssetId].Location = line.ActualLocation;
        }

        count.Status = AssetPhysicalCountStatus.Completed;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(RejectAssetPhysicalCountCommand request, CancellationToken cancellationToken)
    {
        var count = await OpenAsync(request.Id, cancellationToken);
        count.Status = AssetPhysicalCountStatus.Rejected;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AssetPhysicalCount> OpenAsync(long id, CancellationToken ct)
    {
        var count = await db.AssetPhysicalCounts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == id, ct)
                    ?? throw new NotFoundException(nameof(AssetPhysicalCount), id);
        if (count.Status is not (AssetPhysicalCountStatus.Draft or AssetPhysicalCountStatus.InProgress))
        {
            throw new BusinessRuleException("FA-COUNT-CLOSED", "الجرد ده اتقفل.");
        }

        return count;
    }
}

// ================================================================== settings (screen #13)

public sealed record AssetSettingsDto(
    bool AutoDepreciationEnabled, int DepreciationRunDay, bool RequireApprovalForDisposal, bool RequireApprovalForTransfer,
    decimal? MaintenanceApprovalThreshold, AssetPhysicalCountFrequency? PhysicalCountFrequency, bool DefaultFirstMonthProrated);

public sealed record GetAssetSettingsQuery : IRequest<AssetSettingsDto>;
public sealed record UpdateAssetSettingsCommand(AssetSettingsDto Settings) : IRequest;

public sealed class UpdateAssetSettingsCommandValidator : AbstractValidator<UpdateAssetSettingsCommand>
{
    public UpdateAssetSettingsCommandValidator()
    {
        RuleFor(x => x.Settings.DepreciationRunDay).InclusiveBetween(1, 31);
        RuleFor(x => x.Settings.MaintenanceApprovalThreshold).GreaterThanOrEqualTo(0).When(x => x.Settings.MaintenanceApprovalThreshold is not null);
    }
}

public sealed class AssetSettingsHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<GetAssetSettingsQuery, AssetSettingsDto>, IRequestHandler<UpdateAssetSettingsCommand>
{
    public async Task<AssetSettingsDto> Handle(GetAssetSettingsQuery request, CancellationToken cancellationToken)
    {
        var s = await FixedAssetRules.SettingsAsync(db, current.CompanyId, cancellationToken);
        return new AssetSettingsDto(
            s.AutoDepreciationEnabled, s.DepreciationRunDay, s.RequireApprovalForDisposal, s.RequireApprovalForTransfer,
            s.MaintenanceApprovalThreshold, s.PhysicalCountFrequency, s.DefaultFirstMonthProrated);
    }

    public async Task Handle(UpdateAssetSettingsCommand request, CancellationToken cancellationToken)
    {
        var s = await db.AssetSettingsRows.FirstOrDefaultAsync(x => x.CompanyId == current.CompanyId, cancellationToken);
        if (s is null)
        {
            s = new AssetSettings { CompanyId = current.CompanyId };
            db.AssetSettingsRows.Add(s);
        }

        var d = request.Settings;
        s.AutoDepreciationEnabled = d.AutoDepreciationEnabled;
        s.DepreciationRunDay = d.DepreciationRunDay;
        s.RequireApprovalForDisposal = d.RequireApprovalForDisposal;
        s.RequireApprovalForTransfer = d.RequireApprovalForTransfer;
        s.MaintenanceApprovalThreshold = d.MaintenanceApprovalThreshold;
        s.PhysicalCountFrequency = d.PhysicalCountFrequency;
        s.DefaultFirstMonthProrated = d.DefaultFirstMonthProrated;
        await db.SaveChangesAsync(cancellationToken);
    }
}
