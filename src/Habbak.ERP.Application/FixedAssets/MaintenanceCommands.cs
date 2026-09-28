using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.FixedAssets;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.FixedAssets;

// ================================================================== fault reports (screen #9)

public sealed record MaintenanceIssueDto(
    long Id, string IssueNumber, long? BranchId, string? BranchNameAr, long? FixedAssetId, string? AssetNumber, string? AssetNameAr,
    string? DeviceName, DateTime ReportedAtUtc, long ReportedByUserId, string Description, IssueSeverity Severity,
    MaintenanceIssueStatus Status, string? Notes, long? OpenRequestId);

public sealed record GetMaintenanceIssuesQuery(MaintenanceIssueStatus? Status = null) : IRequest<IReadOnlyList<MaintenanceIssueDto>>;

public sealed class GetMaintenanceIssuesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetMaintenanceIssuesQuery, IReadOnlyList<MaintenanceIssueDto>>
{
    public async Task<IReadOnlyList<MaintenanceIssueDto>> Handle(GetMaintenanceIssuesQuery request, CancellationToken cancellationToken) =>
        await db.MaintenanceIssues.AsNoTracking()
            .Where(i => request.Status == null || i.Status == request.Status)
            .OrderByDescending(i => i.ReportedAtUtc)
            .Select(i => new MaintenanceIssueDto(
                i.Id, i.IssueNumber, i.BranchId, i.Branch != null ? i.Branch.NameAr : null, i.FixedAssetId,
                i.FixedAsset != null ? i.FixedAsset.AssetNumber : null, i.FixedAsset != null ? i.FixedAsset.NameAr : null, i.DeviceName,
                i.ReportedAtUtc, i.ReportedByUserId, i.Description, i.Severity, i.Status, i.Notes,
                db.MaintenanceRequests.Where(r => r.IssueId == i.Id && r.Status != MaintenanceRequestStatus.Rejected
                                                  && r.Status != MaintenanceRequestStatus.Cancelled)
                    .Select(r => (long?)r.Id).FirstOrDefault()))
            .ToListAsync(cancellationToken);
}

/// <summary>A fault reported on a registered asset, or on any other device by name.</summary>
public sealed record CreateMaintenanceIssueCommand(
    long? BranchId, long? FixedAssetId, string? DeviceName, string Description, IssueSeverity Severity, string? Notes) : IRequest<long>;

public sealed class CreateMaintenanceIssueCommandValidator : AbstractValidator<CreateMaintenanceIssueCommand>
{
    public CreateMaintenanceIssueCommandValidator()
    {
        RuleFor(x => x.Description).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Severity).IsInEnum();
        RuleFor(x => x.DeviceName).NotEmpty().When(x => x.FixedAssetId is null).WithMessage("حدد الأصل أو اكتب اسم الجهاز.");
        RuleFor(x => x.DeviceName).MaximumLength(200);
    }
}

public sealed record InspectMaintenanceIssueCommand(long Id) : IRequest;
public sealed record RejectMaintenanceIssueCommand(long Id) : IRequest;
public sealed record CancelMaintenanceIssueCommand(long Id) : IRequest;

public sealed class MaintenanceIssueCommandsHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codes)
    : IRequestHandler<CreateMaintenanceIssueCommand, long>, IRequestHandler<InspectMaintenanceIssueCommand>,
      IRequestHandler<RejectMaintenanceIssueCommand>, IRequestHandler<CancelMaintenanceIssueCommand>
{
    public async Task<long> Handle(CreateMaintenanceIssueCommand request, CancellationToken cancellationToken)
    {
        long? branchId = request.BranchId ?? current.BranchId;
        if (request.FixedAssetId is { } assetId)
        {
            var asset = await FixedAssetRules.FindAssetAsync(db, assetId, cancellationToken);
            if (asset.Status is FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff)
            {
                throw new BusinessRuleException("FA-ASSET-CLOSED", "الأصل مستبعد.");
            }

            branchId = asset.BranchId;
        }

        var issue = new MaintenanceIssue
        {
            CompanyId = current.CompanyId,
            BranchId = branchId,
            IssueNumber = await codes.ResolveCodeAsync(FixedAssetScreens.MaintenanceIssues, null, cancellationToken),
            FixedAssetId = request.FixedAssetId,
            DeviceName = request.FixedAssetId is null ? request.DeviceName?.Trim() : null,
            ReportedAtUtc = DateTime.UtcNow,
            ReportedByUserId = current.UserId,
            Description = request.Description.Trim(),
            Severity = request.Severity,
            Notes = request.Notes
        };
        db.MaintenanceIssues.Add(issue);
        await db.SaveChangesAsync(cancellationToken);
        return issue.Id;
    }

    public async Task Handle(InspectMaintenanceIssueCommand request, CancellationToken cancellationToken)
    {
        var issue = await OpenAsync(request.Id, cancellationToken);
        issue.Status = MaintenanceIssueStatus.UnderInspection;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(RejectMaintenanceIssueCommand request, CancellationToken cancellationToken)
    {
        var issue = await OpenAsync(request.Id, cancellationToken);
        issue.Status = MaintenanceIssueStatus.Rejected;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(CancelMaintenanceIssueCommand request, CancellationToken cancellationToken)
    {
        var issue = await OpenAsync(request.Id, cancellationToken);
        issue.Status = MaintenanceIssueStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Only a report not yet being repaired can be inspected, rejected or cancelled.</summary>
    private async Task<MaintenanceIssue> OpenAsync(long id, CancellationToken ct)
    {
        var issue = await db.MaintenanceIssues.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException(nameof(MaintenanceIssue), id);
        if (issue.Status is not (MaintenanceIssueStatus.Reported or MaintenanceIssueStatus.UnderInspection))
        {
            throw new BusinessRuleException("FA-ISSUE-CLOSED", "البلاغ ده اتقفل أو بيتصلّح.");
        }

        return issue;
    }
}

// ================================================================== maintenance requests (screen #10)

public sealed record MaintenanceSparePartDto(
    long Id, long? ItemId, string? ItemNameAr, string Description, decimal Quantity, decimal UnitCost, decimal TotalCost,
    long? WarehouseId, long? StockTransactionId, bool IsStocked);

public sealed record MaintenanceRequestDto(
    long Id, string RequestNumber, long? IssueId, string? IssueNumber, long FixedAssetId, string AssetNumber, string AssetNameAr,
    long MaintenanceCategoryId, string CategoryNameAr, long? MaintenanceScheduleId, DateOnly? DueDate,
    DateOnly RequestDate, DateOnly? ScheduledDate, DateOnly? CompletedDate, long? TechnicianId, string? TechnicianName,
    long? SupplierId, long? ExternalCreditAccountId, MaintenanceRequestStatus Status, DateTime? ApprovedAtUtc,
    decimal? EstimatedCost, decimal LaborCost, decimal SparePartsTotalCost, decimal ActualCost, string? Notes,
    long? JournalEntryId, long? SparePartsJournalEntryId, string RowVersion, IReadOnlyList<MaintenanceSparePartDto> SpareParts);

public sealed record MaintenanceRequestListItemDto(
    long Id, string RequestNumber, long FixedAssetId, string AssetNumber, string AssetNameAr, string CategoryNameAr,
    MaintenanceRequestStatus Status, DateOnly RequestDate, DateOnly? ScheduledDate, DateOnly? CompletedDate, string? TechnicianName,
    decimal? EstimatedCost, decimal ActualCost, bool FromSchedule);

public sealed record GetMaintenanceRequestsQuery(MaintenanceRequestStatus? Status = null, long? FixedAssetId = null)
    : IRequest<IReadOnlyList<MaintenanceRequestListItemDto>>;

public sealed record GetMaintenanceRequestQuery(long Id) : IRequest<MaintenanceRequestDto>;

public sealed class MaintenanceRequestQueriesHandler(IApplicationDbContext db)
    : IRequestHandler<GetMaintenanceRequestsQuery, IReadOnlyList<MaintenanceRequestListItemDto>>,
      IRequestHandler<GetMaintenanceRequestQuery, MaintenanceRequestDto>
{
    public async Task<IReadOnlyList<MaintenanceRequestListItemDto>> Handle(GetMaintenanceRequestsQuery request, CancellationToken cancellationToken) =>
        await db.MaintenanceRequests.AsNoTracking()
            .Where(r => (request.Status == null || r.Status == request.Status) && (request.FixedAssetId == null || r.FixedAssetId == request.FixedAssetId))
            .OrderByDescending(r => r.RequestDate).ThenByDescending(r => r.Id)
            .Select(r => new MaintenanceRequestListItemDto(
                r.Id, r.RequestNumber, r.FixedAssetId, r.FixedAsset!.AssetNumber, r.FixedAsset.NameAr, r.MaintenanceCategory!.NameAr, r.Status,
                r.RequestDate, r.ScheduledDate, r.CompletedDate, r.TechnicianName, r.EstimatedCost, r.ActualCost, r.MaintenanceScheduleId != null))
            .ToListAsync(cancellationToken);

    public async Task<MaintenanceRequestDto> Handle(GetMaintenanceRequestQuery request, CancellationToken cancellationToken)
    {
        var r = await db.MaintenanceRequests.AsNoTracking()
                    .Include(x => x.Issue).Include(x => x.FixedAsset).Include(x => x.MaintenanceCategory)
                    .Include(x => x.SpareParts).ThenInclude(p => p.Item)
                    .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(MaintenanceRequest), request.Id);
        return new MaintenanceRequestDto(
            r.Id, r.RequestNumber, r.IssueId, r.Issue?.IssueNumber, r.FixedAssetId, r.FixedAsset!.AssetNumber, r.FixedAsset.NameAr,
            r.MaintenanceCategoryId, r.MaintenanceCategory!.NameAr, r.MaintenanceScheduleId, r.DueDate, r.RequestDate, r.ScheduledDate,
            r.CompletedDate, r.TechnicianId, r.TechnicianName, r.SupplierId, r.ExternalCreditAccountId, r.Status, r.ApprovedAtUtc,
            r.EstimatedCost, r.LaborCost, r.SparePartsTotalCost, r.ActualCost, r.Notes, r.JournalEntryId, r.SparePartsJournalEntryId,
            Convert.ToBase64String(r.RowVersion),
            r.SpareParts.OrderBy(p => p.Id).Select(p => new MaintenanceSparePartDto(
                p.Id, p.ItemId, p.Item?.NameAr, p.Description, p.Quantity, p.UnitCost, p.TotalCost, p.WarehouseId, p.StockTransactionId, p.IsStocked))
                .ToList());
    }
}

/// <summary>
/// A spare part on the request. A stocked part names the item and the warehouse and never a price —
/// it is costed at the average when issued (rule 16); its quantity is in the item's base unit. A part
/// bought for the job has no item and its cost is typed in.
/// </summary>
public sealed record MaintenanceSparePartInput(long? ItemId, long? WarehouseId, string? Description, decimal Quantity, decimal? UnitCost);

public sealed record MaintenanceRequestInput(
    long FixedAssetId, long MaintenanceCategoryId, DateOnly RequestDate, DateOnly? ScheduledDate,
    long? TechnicianId, string? TechnicianName, long? SupplierId, long? ExternalCreditAccountId,
    decimal? EstimatedCost, decimal LaborCost, string? Notes, IReadOnlyList<MaintenanceSparePartInput>? SpareParts);

public sealed class MaintenanceRequestInputValidator : AbstractValidator<MaintenanceRequestInput>
{
    public MaintenanceRequestInputValidator()
    {
        RuleFor(x => x.FixedAssetId).GreaterThan(0);
        RuleFor(x => x.MaintenanceCategoryId).GreaterThan(0);
        RuleFor(x => x.RequestDate).NotEqual(default(DateOnly));
        RuleFor(x => x.LaborCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EstimatedCost).GreaterThanOrEqualTo(0).When(x => x.EstimatedCost is not null);
        RuleFor(x => x.TechnicianName).MaximumLength(200);
        RuleForEach(x => x.SpareParts).ChildRules(p =>
        {
            p.RuleFor(x => x.Quantity).GreaterThan(0);
            p.RuleFor(x => x.WarehouseId).NotNull().When(x => x.ItemId is not null).WithMessage("القطعة من المخزون محتاجة المخزن اللي هتتصرف منه.");
            p.RuleFor(x => x.Description).NotEmpty().When(x => x.ItemId is null).WithMessage("اكتب وصف القطعة المشتراة.");
            p.RuleFor(x => x.UnitCost).NotNull().GreaterThanOrEqualTo(0).When(x => x.ItemId is null).WithMessage("القطعة المشتراة محتاجة تكلفتها.");
        });
    }
}

public sealed record CreateMaintenanceRequestCommand(long? IssueId, MaintenanceRequestInput Data) : IRequest<long>;
public sealed record UpdateMaintenanceRequestCommand(long Id, string RowVersion, MaintenanceRequestInput Data) : IRequest;
public sealed record ApproveMaintenanceRequestCommand(long Id) : IRequest;
public sealed record StartMaintenanceRequestCommand(long Id) : IRequest;
public sealed record CompleteMaintenanceRequestCommand(long Id, DateOnly CompletedDate) : IRequest;
public sealed record RejectMaintenanceRequestCommand(long Id) : IRequest;
public sealed record CancelMaintenanceRequestCommand(long Id) : IRequest;

public sealed class CreateMaintenanceRequestCommandValidator : AbstractValidator<CreateMaintenanceRequestCommand>
{
    public CreateMaintenanceRequestCommandValidator() => RuleFor(x => x.Data).SetValidator(new MaintenanceRequestInputValidator());
}

public sealed class UpdateMaintenanceRequestCommandValidator : AbstractValidator<UpdateMaintenanceRequestCommand>
{
    public UpdateMaintenanceRequestCommandValidator() => RuleFor(x => x.Data).SetValidator(new MaintenanceRequestInputValidator());
}

public sealed class MaintenanceRequestCommandsHandler(
    IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codes, IStockMovementService stock, IPostingTemplateEngine posting)
    : IRequestHandler<CreateMaintenanceRequestCommand, long>, IRequestHandler<UpdateMaintenanceRequestCommand>,
      IRequestHandler<ApproveMaintenanceRequestCommand>, IRequestHandler<StartMaintenanceRequestCommand>,
      IRequestHandler<CompleteMaintenanceRequestCommand>, IRequestHandler<RejectMaintenanceRequestCommand>,
      IRequestHandler<CancelMaintenanceRequestCommand>
{
    public async Task<long> Handle(CreateMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        MaintenanceIssue? issue = null;
        if (request.IssueId is { } issueId)
        {
            issue = await db.MaintenanceIssues.FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken)
                    ?? throw new NotFoundException(nameof(MaintenanceIssue), issueId);
            if (issue.Status is not (MaintenanceIssueStatus.Reported or MaintenanceIssueStatus.UnderInspection))
            {
                throw new BusinessRuleException("FA-ISSUE-CLOSED", "البلاغ ده اتقفل أو عليه طلب صيانة شغّال.");
            }

            if (issue.FixedAssetId is { } issueAsset && issueAsset != request.Data.FixedAssetId)
            {
                throw new BusinessRuleException("FA-ISSUE-ASSET-MISMATCH", "الطلب لازم يكون على نفس أصل البلاغ.");
            }

            issue.FixedAssetId ??= request.Data.FixedAssetId;
            issue.Status = MaintenanceIssueStatus.UnderInspection;
        }

        var entity = new MaintenanceRequest
        {
            CompanyId = current.CompanyId,
            RequestNumber = await codes.ResolveCodeAsync(FixedAssetScreens.MaintenanceRequests, null, cancellationToken),
            IssueId = issue?.Id
        };
        await ApplyAsync(entity, request.Data, cancellationToken);
        db.MaintenanceRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task Handle(UpdateMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(request.Id, cancellationToken);
        if (entity.Status is not (MaintenanceRequestStatus.Draft or MaintenanceRequestStatus.Approved or MaintenanceRequestStatus.InProgress))
        {
            throw new BusinessRuleException("FA-MAINT-CLOSED", "الطلب ده اتقفل.");
        }

        if (entity.Status != MaintenanceRequestStatus.Draft && request.Data.FixedAssetId != entity.FixedAssetId)
        {
            throw new BusinessRuleException("FA-MAINT-ASSET-LOCKED", "الأصل مايتغيّرش بعد الاعتماد.");
        }

        db.Entry(entity).Property(nameof(MaintenanceRequest.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);
        await ApplyAsync(entity, request.Data, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(ApproveMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(request.Id, cancellationToken);
        if (entity.Status != MaintenanceRequestStatus.Draft)
        {
            throw new BusinessRuleException("FA-MAINT-NOT-DRAFT", "الاعتماد للطلب المسودة بس.");
        }

        entity.Status = MaintenanceRequestStatus.Approved;
        entity.ApprovedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Work starts: the asset goes into maintenance. Above the threshold it must have been approved (rule 15).</summary>
    public async Task Handle(StartMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(request.Id, cancellationToken);
        if (entity.Status is not (MaintenanceRequestStatus.Draft or MaintenanceRequestStatus.Approved))
        {
            throw new BusinessRuleException("FA-MAINT-NOT-STARTABLE", "الطلب ده مينفعش يبدأ.");
        }

        await EnsureApprovedAsync(entity, entity.EstimatedCost ?? 0m, cancellationToken);

        var asset = await FixedAssetRules.FindAssetAsync(db, entity.FixedAssetId, cancellationToken);
        if (asset.Status == FixedAssetStatus.Active)
        {
            asset.Status = FixedAssetStatus.InMaintenance;
        }

        if (entity.IssueId is { } issueId && await db.MaintenanceIssues.FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken) is { } issue)
        {
            issue.Status = MaintenanceIssueStatus.Repairing;
        }

        entity.Status = MaintenanceRequestStatus.InProgress;
        entity.ScheduledDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Finishes the job: issues the stocked parts at the average cost of this moment (rule 16), totals
    /// the cost, and posts it as two separate entries (rule 20) — the external cost (labor + parts
    /// bought) against the supplier/treasury, the stocked parts against inventory. Both carry the
    /// asset's cost center (rule 27). The asset is back in service and the schedule moves on.
    /// </summary>
    public async Task Handle(CompleteMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(request.Id, cancellationToken);
        if (entity.Status != MaintenanceRequestStatus.InProgress)
        {
            throw new BusinessRuleException("FA-MAINT-NOT-IN-PROGRESS", "الإكمال للطلب اللي الشغل فيه بدأ بس.");
        }

        var asset = await FixedAssetRules.FindAssetAsync(db, entity.FixedAssetId, cancellationToken);

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        foreach (var part in entity.SpareParts.Where(p => p.IsStocked))
        {
            var applied = await stock.ApplyMovementAsync(new StockMovementRequest
            {
                CompanyId = current.CompanyId,
                WarehouseId = part.WarehouseId!.Value,
                ItemId = part.ItemId!.Value,
                TransactionType = TransactionType.MaintenanceIssue,
                Quantity = part.Quantity,
                UnitCost = 0m,   // outbound: the service costs it at the average
                TransactionDate = request.CompletedDate,
                SourceDocumentType = "MaintenanceRequest",
                SourceDocumentId = entity.Id
            }, cancellationToken);
            part.UnitCost = applied.UnitCost;
            part.TotalCost = Math.Round(part.Quantity * applied.UnitCost, 2);
            part.StockTransaction = applied;
        }

        var stockParts = entity.SpareParts.Where(p => p.IsStocked).Sum(p => p.TotalCost);
        var boughtParts = entity.SpareParts.Where(p => !p.IsStocked).Sum(p => p.TotalCost);
        entity.SparePartsTotalCost = stockParts + boughtParts;
        entity.ActualCost = entity.LaborCost + entity.SparePartsTotalCost;
        await EnsureApprovedAsync(entity, entity.ActualCost, cancellationToken);

        var external = entity.LaborCost + boughtParts;
        var expenseAccountId = asset.Category!.MaintenanceExpenseAccountId;
        IReadOnlyDictionary<long, long>? costCenter = null;
        async Task<IReadOnlyDictionary<long, long>> CostCenter() => costCenter ??= await FixedAssetRules.CostCenterAsync(db, asset, cancellationToken);

        if (external > 0 && await posting.IsConfiguredAsync(current.CompanyId, PostingScreenCatalog.MaintenanceExternalCost, cancellationToken))
        {
            if (entity.ExternalCreditAccountId is null)
            {
                throw new BusinessRuleException("FA-MAINT-CREDIT-ACCOUNT", "حدد الطرف الدائن للتكلفة الخارجية (حساب المورد أو الخزينة).");
            }

            entity.JournalEntry = await posting.PostIfConfiguredAsync(new TemplatePostingRequest
            {
                CompanyId = current.CompanyId,
                BranchId = asset.BranchId,
                ScreenCode = PostingScreenCatalog.MaintenanceExternalCost,
                SourceModule = SourceModule.FixedAssets,
                SourceDocumentType = SourceDocumentType.MaintenanceRequest,
                SourceDocumentId = entity.Id,
                EntryDate = request.CompletedDate,
                Description = $"صيانة {entity.RequestNumber} — {asset.NameAr} (تكلفة خارجية)",
                IdempotencyKey = PostingKeys.For(current.CompanyId, "Maintenance.External", entity.Id),
                Dimensions = await CostCenter(),
                Context = PostingContext.Create(new Dictionary<string, object?>
                {
                    ["BranchId"] = asset.BranchId,
                    ["FixedAssetId"] = asset.Id,
                    ["SupplierId"] = entity.SupplierId,
                    ["ExternalCost"] = external,
                    ["LaborCost"] = entity.LaborCost,
                    ["BoughtPartsCost"] = boughtParts,
                    ["MaintenanceExpenseAccountId"] = expenseAccountId,
                    ["ExternalCreditAccountId"] = entity.ExternalCreditAccountId
                })
            }, cancellationToken);
        }

        if (stockParts > 0 && await posting.IsConfiguredAsync(current.CompanyId, PostingScreenCatalog.MaintenanceSpareParts, cancellationToken))
        {
            entity.SparePartsJournalEntry = await posting.PostIfConfiguredAsync(new TemplatePostingRequest
            {
                CompanyId = current.CompanyId,
                BranchId = asset.BranchId,
                ScreenCode = PostingScreenCatalog.MaintenanceSpareParts,
                SourceModule = SourceModule.FixedAssets,
                SourceDocumentType = SourceDocumentType.MaintenanceRequest,
                SourceDocumentId = entity.Id,
                EntryDate = request.CompletedDate,
                Description = $"صيانة {entity.RequestNumber} — {asset.NameAr} (قطع غيار من المخزون)",
                IdempotencyKey = PostingKeys.For(current.CompanyId, "Maintenance.SpareParts", entity.Id),
                Dimensions = await CostCenter(),
                Context = PostingContext.Create(new Dictionary<string, object?>
                {
                    ["BranchId"] = asset.BranchId,
                    ["FixedAssetId"] = asset.Id,
                    [PostingScreenCatalog.HasStockMovementField] = true,
                    ["StockPartsCost"] = stockParts,
                    ["MaintenanceExpenseAccountId"] = expenseAccountId
                })
            }, cancellationToken);
        }

        if (asset.Status == FixedAssetStatus.InMaintenance)
        {
            asset.Status = FixedAssetStatus.Active;
        }

        if (entity.IssueId is { } issueId && await db.MaintenanceIssues.FirstOrDefaultAsync(i => i.Id == issueId, cancellationToken) is { } issue)
        {
            issue.Status = MaintenanceIssueStatus.Repaired;
        }

        if (entity.MaintenanceScheduleId is { } scheduleId
            && await db.MaintenanceSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken) is { } schedule)
        {
            schedule.LastExecutedDate = request.CompletedDate;
            MaintenanceScheduleRules.MovePast(schedule, entity.DueDate ?? request.CompletedDate);
        }

        entity.Status = MaintenanceRequestStatus.Completed;
        entity.CompletedDate = request.CompletedDate;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task Handle(RejectMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(request.Id, cancellationToken);
        if (entity.Status is not (MaintenanceRequestStatus.Draft or MaintenanceRequestStatus.Approved))
        {
            throw new BusinessRuleException("FA-MAINT-NOT-REJECTABLE", "الرفض قبل ما الشغل يبدأ بس.");
        }

        await CloseAsync(entity, MaintenanceRequestStatus.Rejected, cancellationToken);
    }

    public async Task Handle(CancelMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = await FindAsync(request.Id, cancellationToken);
        if (entity.Status is MaintenanceRequestStatus.Completed or MaintenanceRequestStatus.Rejected or MaintenanceRequestStatus.Cancelled)
        {
            throw new BusinessRuleException("FA-MAINT-CLOSED", "الطلب ده اتقفل.");
        }

        await CloseAsync(entity, MaintenanceRequestStatus.Cancelled, cancellationToken);
    }

    /// <summary>
    /// Nothing was issued or posted yet (that happens at completion). The asset leaves maintenance, the
    /// report goes back to inspection, and a scheduled occurrence is skipped rather than raised again.
    /// </summary>
    private async Task CloseAsync(MaintenanceRequest entity, MaintenanceRequestStatus status, CancellationToken ct)
    {
        if (entity.Status == MaintenanceRequestStatus.InProgress)
        {
            var asset = await FixedAssetRules.FindAssetAsync(db, entity.FixedAssetId, ct);
            if (asset.Status == FixedAssetStatus.InMaintenance)
            {
                asset.Status = FixedAssetStatus.Active;
            }
        }

        if (entity.IssueId is { } issueId && await db.MaintenanceIssues.FirstOrDefaultAsync(i => i.Id == issueId, ct) is { } issue
                                           && issue.Status is MaintenanceIssueStatus.UnderInspection or MaintenanceIssueStatus.Repairing)
        {
            issue.Status = MaintenanceIssueStatus.UnderInspection;
        }

        if (entity.MaintenanceScheduleId is { } scheduleId && entity.DueDate is { } due
            && await db.MaintenanceSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, ct) is { } schedule)
        {
            MaintenanceScheduleRules.MovePast(schedule, due);
        }

        entity.Status = status;
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureApprovedAsync(MaintenanceRequest entity, decimal cost, CancellationToken ct)
    {
        var settings = await FixedAssetRules.SettingsAsync(db, current.CompanyId, ct);
        if (settings.MaintenanceApprovalThreshold is { } threshold && cost > threshold && entity.ApprovedAtUtc is null)
        {
            throw new BusinessRuleException(
                "FA-MAINT-APPROVAL-REQUIRED", $"تكلفة الطلب ({cost:N2}) أعلى من حد الاعتماد ({threshold:N2}) — لازم يتعمد الأول.");
        }
    }

    private async Task<MaintenanceRequest> FindAsync(long id, CancellationToken ct) =>
        await db.MaintenanceRequests.Include(r => r.SpareParts).FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException(nameof(MaintenanceRequest), id);

    private async Task ApplyAsync(MaintenanceRequest entity, MaintenanceRequestInput d, CancellationToken ct)
    {
        if (entity.FixedAssetId != d.FixedAssetId)
        {
            var asset = await FixedAssetRules.FindAssetAsync(db, d.FixedAssetId, ct);
            if (asset.Status is FixedAssetStatus.Draft or FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff)
            {
                throw new BusinessRuleException("FA-MAINT-ASSET-NOT-ACTIVE", "الصيانة للأصل المعتمد اللي لسه في الخدمة.");
            }
        }

        var category = await db.MaintenanceCategories.FirstOrDefaultAsync(c => c.Id == d.MaintenanceCategoryId, ct)
                       ?? throw new NotFoundException(nameof(MaintenanceCategory), d.MaintenanceCategoryId);

        entity.FixedAssetId = d.FixedAssetId;
        entity.MaintenanceCategoryId = category.Id;
        entity.RequestDate = d.RequestDate;
        entity.ScheduledDate = d.ScheduledDate;
        entity.TechnicianId = d.TechnicianId;
        entity.TechnicianName = d.TechnicianName?.Trim();
        entity.SupplierId = d.SupplierId;
        entity.ExternalCreditAccountId = d.ExternalCreditAccountId;
        entity.EstimatedCost = d.EstimatedCost;
        entity.LaborCost = d.LaborCost;
        entity.Notes = d.Notes;

        // Parts are replaced wholesale — nothing is issued before completion, so there is nothing to undo.
        foreach (var old in entity.SpareParts.ToList())
        {
            entity.SpareParts.Remove(old);
            db.MaintenanceSpareParts.Remove(old);
        }

        var itemIds = (d.SpareParts ?? []).Where(p => p.ItemId is not null).Select(p => p.ItemId!.Value).Distinct().ToList();
        var items = await db.Items.Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        foreach (var p in d.SpareParts ?? [])
        {
            if (p.ItemId is { } itemId)
            {
                var item = items.GetValueOrDefault(itemId) ?? throw new NotFoundException(nameof(Item), itemId);
                entity.SpareParts.Add(new MaintenanceSparePart
                {
                    ItemId = itemId, WarehouseId = p.WarehouseId, Description = string.IsNullOrWhiteSpace(p.Description) ? item.NameAr : p.Description.Trim(),
                    Quantity = p.Quantity, UnitCost = 0m, TotalCost = 0m   // costed when issued (rule 16)
                });
            }
            else
            {
                var unitCost = p.UnitCost ?? 0m;
                entity.SpareParts.Add(new MaintenanceSparePart
                {
                    Description = p.Description!.Trim(), Quantity = p.Quantity, UnitCost = unitCost, TotalCost = Math.Round(p.Quantity * unitCost, 2)
                });
            }
        }

        entity.SparePartsTotalCost = entity.SpareParts.Sum(p => p.TotalCost);
        entity.ActualCost = entity.LaborCost + entity.SparePartsTotalCost;
    }
}

// ================================================================== preventive schedules (screen #11)

internal static class MaintenanceScheduleRules
{
    /// <summary>The schedule's next due date after <paramref name="handled"/> — never moved backwards.</summary>
    public static void MovePast(MaintenanceSchedule schedule, DateOnly handled)
    {
        var next = MaintenanceSchedule.Advance(handled, schedule.Frequency);
        if (next > schedule.NextDueDate)
        {
            schedule.NextDueDate = next;
        }
    }

    /// <summary>The job's key for one occurrence (rule 29): (schedule, due date).</summary>
    public static Guid Key(long scheduleId, DateOnly dueDate)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"MaintenanceSchedule:{scheduleId}:{dueDate:yyyy-MM-dd}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}

public sealed record MaintenanceScheduleDto(
    long Id, long FixedAssetId, string AssetNumber, string AssetNameAr, long MaintenanceCategoryId, string CategoryNameAr,
    MaintenanceFrequency Frequency, DateOnly? LastExecutedDate, DateOnly NextDueDate, long? TechnicianId, string? TechnicianName,
    bool IsActive, string? Notes);

public sealed record GetMaintenanceSchedulesQuery : IRequest<IReadOnlyList<MaintenanceScheduleDto>>;

public sealed class GetMaintenanceSchedulesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetMaintenanceSchedulesQuery, IReadOnlyList<MaintenanceScheduleDto>>
{
    public async Task<IReadOnlyList<MaintenanceScheduleDto>> Handle(GetMaintenanceSchedulesQuery request, CancellationToken cancellationToken) =>
        await db.MaintenanceSchedules.AsNoTracking()
            .OrderBy(s => s.NextDueDate)
            .Select(s => new MaintenanceScheduleDto(
                s.Id, s.FixedAssetId, s.FixedAsset!.AssetNumber, s.FixedAsset.NameAr, s.MaintenanceCategoryId, s.MaintenanceCategory!.NameAr,
                s.Frequency, s.LastExecutedDate, s.NextDueDate, s.TechnicianId, s.TechnicianName, s.IsActive, s.Notes))
            .ToListAsync(cancellationToken);
}

public sealed record SaveMaintenanceScheduleCommand(
    long? Id, long FixedAssetId, long MaintenanceCategoryId, MaintenanceFrequency Frequency, DateOnly NextDueDate,
    long? TechnicianId, string? TechnicianName, bool IsActive, string? Notes) : IRequest<long>;

public sealed class SaveMaintenanceScheduleCommandValidator : AbstractValidator<SaveMaintenanceScheduleCommand>
{
    public SaveMaintenanceScheduleCommandValidator()
    {
        RuleFor(x => x.FixedAssetId).GreaterThan(0);
        RuleFor(x => x.MaintenanceCategoryId).GreaterThan(0);
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.NextDueDate).NotEqual(default(DateOnly));
        RuleFor(x => x.TechnicianName).MaximumLength(200);
    }
}

public sealed record DeleteMaintenanceScheduleCommand(long Id) : IRequest;

public sealed class MaintenanceScheduleCommandsHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<SaveMaintenanceScheduleCommand, long>, IRequestHandler<DeleteMaintenanceScheduleCommand>
{
    public async Task<long> Handle(SaveMaintenanceScheduleCommand request, CancellationToken cancellationToken)
    {
        var asset = await FixedAssetRules.FindAssetAsync(db, request.FixedAssetId, cancellationToken);
        if (asset.Status is FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff)
        {
            throw new BusinessRuleException("FA-ASSET-CLOSED", "الأصل مستبعد.");
        }

        if (!await db.MaintenanceCategories.AnyAsync(c => c.Id == request.MaintenanceCategoryId, cancellationToken))
        {
            throw new NotFoundException(nameof(MaintenanceCategory), request.MaintenanceCategoryId);
        }

        MaintenanceSchedule schedule;
        if (request.Id is { } id)
        {
            schedule = await db.MaintenanceSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
                       ?? throw new NotFoundException(nameof(MaintenanceSchedule), id);
        }
        else
        {
            schedule = new MaintenanceSchedule { CompanyId = current.CompanyId };
            db.MaintenanceSchedules.Add(schedule);
        }

        schedule.FixedAssetId = asset.Id;
        schedule.MaintenanceCategoryId = request.MaintenanceCategoryId;
        schedule.Frequency = request.Frequency;
        schedule.NextDueDate = request.NextDueDate;
        schedule.TechnicianId = request.TechnicianId;
        schedule.TechnicianName = request.TechnicianName?.Trim();
        schedule.IsActive = request.IsActive;
        schedule.Notes = request.Notes;
        await db.SaveChangesAsync(cancellationToken);
        return schedule.Id;
    }

    public async Task Handle(DeleteMaintenanceScheduleCommand request, CancellationToken cancellationToken)
    {
        var schedule = await db.MaintenanceSchedules.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
                       ?? throw new NotFoundException(nameof(MaintenanceSchedule), request.Id);
        db.MaintenanceSchedules.Remove(schedule);
        await db.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// What the daily job asks for each company (section 3.2): a Draft request for every active schedule
/// that has fallen due. The request's key is derived from (schedule, due date), so a second run — the
/// same day or any day before the occurrence is handled — finds it and raises nothing (rule 29).
/// Returns how many requests were raised.
/// </summary>
public sealed record GenerateDueMaintenanceRequestsCommand(DateOnly Today) : IRequest<int>;

public sealed class GenerateDueMaintenanceRequestsCommandHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codes)
    : IRequestHandler<GenerateDueMaintenanceRequestsCommand, int>
{
    public async Task<int> Handle(GenerateDueMaintenanceRequestsCommand request, CancellationToken cancellationToken)
    {
        var due = await db.MaintenanceSchedules
            .Where(s => s.IsActive && s.NextDueDate <= request.Today
                        && (s.FixedAsset!.Status == FixedAssetStatus.Active || s.FixedAsset.Status == FixedAssetStatus.InMaintenance
                            || s.FixedAsset.Status == FixedAssetStatus.Transferred))
            .ToListAsync(cancellationToken);

        var raised = 0;
        foreach (var schedule in due)
        {
            var key = MaintenanceScheduleRules.Key(schedule.Id, schedule.NextDueDate);
            if (await db.MaintenanceRequests.AnyAsync(r => r.IdempotencyKey == key, cancellationToken))
            {
                continue;
            }

            db.MaintenanceRequests.Add(new MaintenanceRequest
            {
                CompanyId = current.CompanyId,
                RequestNumber = await codes.ResolveCodeAsync(FixedAssetScreens.MaintenanceRequests, null, cancellationToken),
                FixedAssetId = schedule.FixedAssetId,
                MaintenanceCategoryId = schedule.MaintenanceCategoryId,
                MaintenanceScheduleId = schedule.Id,
                DueDate = schedule.NextDueDate,
                IdempotencyKey = key,
                RequestDate = request.Today,
                ScheduledDate = schedule.NextDueDate,
                TechnicianId = schedule.TechnicianId,
                TechnicianName = schedule.TechnicianName,
                Notes = schedule.Notes
            });
            await db.SaveChangesAsync(cancellationToken);
            raised++;
        }

        return raised;
    }
}

// ================================================================== the board (screen #12)

public enum MaintenanceBoardColumn
{
    Reported = 1,
    Planned = 2,
    InProgress = 3,
    Done = 4
}

/// <summary>A card on the Kanban board: an open fault report, or a request.</summary>
public sealed record MaintenanceBoardCardDto(
    string Kind, long Id, string Number, MaintenanceBoardColumn Column, string Title, string? AssetNumber, string? AssetNameAr,
    IssueSeverity? Severity, string? TechnicianName, DateOnly? Date, decimal? Cost, int Status, bool NeedsApproval, bool FromSchedule);

/// <summary>Open reports, planned and running requests, and what was finished in the last <paramref name="DoneDays"/> days.</summary>
public sealed record GetMaintenanceBoardQuery(DateOnly Today, int DoneDays = 14) : IRequest<IReadOnlyList<MaintenanceBoardCardDto>>;

public sealed class GetMaintenanceBoardQueryHandler(IApplicationDbContext db, ICurrentCompanyContext current)
    : IRequestHandler<GetMaintenanceBoardQuery, IReadOnlyList<MaintenanceBoardCardDto>>
{
    public async Task<IReadOnlyList<MaintenanceBoardCardDto>> Handle(GetMaintenanceBoardQuery request, CancellationToken cancellationToken)
    {
        var settings = await FixedAssetRules.SettingsAsync(db, current.CompanyId, cancellationToken);
        var threshold = settings.MaintenanceApprovalThreshold;

        var issues = await db.MaintenanceIssues.AsNoTracking()
            .Where(i => (i.Status == MaintenanceIssueStatus.Reported || i.Status == MaintenanceIssueStatus.UnderInspection)
                        && !db.MaintenanceRequests.Any(r => r.IssueId == i.Id && r.Status != MaintenanceRequestStatus.Rejected
                                                            && r.Status != MaintenanceRequestStatus.Cancelled))
            .Select(i => new MaintenanceBoardCardDto(
                "Issue", i.Id, i.IssueNumber, MaintenanceBoardColumn.Reported, i.Description,
                i.FixedAsset != null ? i.FixedAsset.AssetNumber : null, i.FixedAsset != null ? i.FixedAsset.NameAr : i.DeviceName,
                i.Severity, null, DateOnly.FromDateTime(i.ReportedAtUtc), null, (int)i.Status, false, false))
            .ToListAsync(cancellationToken);

        var doneSince = request.Today.AddDays(-request.DoneDays);
        var requests = await db.MaintenanceRequests.AsNoTracking()
            .Where(r => r.Status == MaintenanceRequestStatus.Draft || r.Status == MaintenanceRequestStatus.Approved
                        || r.Status == MaintenanceRequestStatus.InProgress
                        || (r.Status == MaintenanceRequestStatus.Completed && r.CompletedDate >= doneSince))
            .Select(r => new
            {
                r.Id, r.RequestNumber, r.Status, Category = r.MaintenanceCategory!.NameAr, r.FixedAsset!.AssetNumber, AssetName = r.FixedAsset.NameAr,
                Severity = r.Issue != null ? (IssueSeverity?)r.Issue.Severity : null, r.TechnicianName, r.ScheduledDate, r.RequestDate,
                r.CompletedDate, r.EstimatedCost, r.ActualCost, r.ApprovedAtUtc, FromSchedule = r.MaintenanceScheduleId != null
            })
            .ToListAsync(cancellationToken);

        return issues
            .Concat(requests.Select(r => new MaintenanceBoardCardDto(
                "Request", r.Id, r.RequestNumber,
                r.Status switch
                {
                    MaintenanceRequestStatus.InProgress => MaintenanceBoardColumn.InProgress,
                    MaintenanceRequestStatus.Completed => MaintenanceBoardColumn.Done,
                    _ => MaintenanceBoardColumn.Planned
                },
                r.Category, r.AssetNumber, r.AssetName, r.Severity, r.TechnicianName,
                r.Status == MaintenanceRequestStatus.Completed ? r.CompletedDate : r.ScheduledDate ?? r.RequestDate,
                r.Status == MaintenanceRequestStatus.Completed ? r.ActualCost : r.EstimatedCost, (int)r.Status,
                threshold is { } t && (r.EstimatedCost ?? 0m) > t && r.ApprovedAtUtc is null && r.Status == MaintenanceRequestStatus.Draft,
                r.FromSchedule)))
            .OrderBy(c => c.Column)
            .ThenByDescending(c => c.Severity ?? IssueSeverity.Low)
            .ThenBy(c => c.Date)
            .ToList();
    }
}
