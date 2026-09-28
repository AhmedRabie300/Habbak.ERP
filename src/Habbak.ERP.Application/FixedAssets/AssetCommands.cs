using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.FixedAssets;
using Habbak.ERP.Shared.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.FixedAssets;

public sealed record FixedAssetListItemDto(
    long Id, string AssetNumber, string NameAr, string NameEn, long CategoryId, string CategoryNameAr, long? BranchId, string? BranchNameAr,
    FixedAssetStatus Status, DateOnly AcquisitionDate, decimal BaseCurrencyAmount, decimal AccumulatedDepreciation, decimal NetBookValue,
    string? Location);

public sealed record DepreciationPeriodDto(
    long Id, int PeriodNumber, DateOnly PeriodStart, DateOnly PeriodEnd, decimal Amount, decimal AccumulatedAfter, decimal BookValueAfter,
    DepreciationScheduleStatus Status, long? DepreciationRunId, string? RunNumber, long? JournalEntryId);

public sealed record FixedAssetDto(
    long Id, string AssetNumber, string NameAr, string NameEn, long? BranchId, long CategoryId, string CategoryNameAr,
    string? SerialNumber, string? Barcode, string? Location,
    DateOnly AcquisitionDate, decimal AcquisitionCost, string CurrencyCode, decimal ExchangeRate, decimal BaseCurrencyAmount,
    long? SupplierId, long? PurchaseInvoiceId, long? FundingAccountId, long? AcquisitionJournalEntryId,
    int? UsefulLifeYears, decimal SalvageValue, DepreciationMethod DepreciationMethod, decimal? DepreciationRate,
    DateOnly DepreciationStartDate, bool FirstMonthProrated, decimal AccumulatedDepreciation, decimal NetBookValue,
    FixedAssetStatus Status, DateOnly? DisposalDate, string? DisposalReason, decimal? DisposalProceeds, long? DisposalJournalEntryId,
    long? CustodyOfficerId, string? CustodyOfficerName, long? CostCenterValueId, string? Notes, string RowVersion,
    IReadOnlyList<DepreciationPeriodDto> Schedule);

/// <summary>What the asset form sends. The financial fields only count while the asset is a Draft (rule 9).</summary>
public sealed record FixedAssetInput(
    string NameAr, string NameEn, long? BranchId, long CategoryId, string? SerialNumber, string? Barcode, string? Location,
    DateOnly AcquisitionDate, decimal AcquisitionCost, string? CurrencyCode, decimal ExchangeRate,
    long? SupplierId, long? PurchaseInvoiceId, long? FundingAccountId,
    int? UsefulLifeYears, decimal? SalvageValue, DepreciationMethod? DepreciationMethod, decimal? DepreciationRate,
    DateOnly? DepreciationStartDate, bool? FirstMonthProrated,
    long? CustodyOfficerId, long? CostCenterValueId, string? Notes);

// ------------------------------------------------------------------ queries

public sealed record GetFixedAssetsQuery(FixedAssetStatus? Status = null, long? CategoryId = null) : IRequest<IReadOnlyList<FixedAssetListItemDto>>;

public sealed class GetFixedAssetsQueryHandler(IApplicationDbContext db) : IRequestHandler<GetFixedAssetsQuery, IReadOnlyList<FixedAssetListItemDto>>
{
    public async Task<IReadOnlyList<FixedAssetListItemDto>> Handle(GetFixedAssetsQuery request, CancellationToken cancellationToken) =>
        await db.FixedAssets.AsNoTracking()
            .Where(a => (request.Status == null || a.Status == request.Status) && (request.CategoryId == null || a.CategoryId == request.CategoryId))
            .OrderBy(a => a.AssetNumber)
            .Select(a => new FixedAssetListItemDto(
                a.Id, a.AssetNumber, a.NameAr, a.NameEn, a.CategoryId, a.Category!.NameAr, a.BranchId, a.Branch != null ? a.Branch.NameAr : null,
                a.Status, a.AcquisitionDate, a.BaseCurrencyAmount, a.AccumulatedDepreciation, a.BaseCurrencyAmount - a.AccumulatedDepreciation, a.Location))
            .ToListAsync(cancellationToken);
}

public sealed record GetFixedAssetQuery(long Id) : IRequest<FixedAssetDto>;

public sealed class GetFixedAssetQueryHandler(IApplicationDbContext db) : IRequestHandler<GetFixedAssetQuery, FixedAssetDto>
{
    public async Task<FixedAssetDto> Handle(GetFixedAssetQuery request, CancellationToken cancellationToken)
    {
        var a = await db.FixedAssets.AsNoTracking()
                    .Include(x => x.Category)
                    .Include(x => x.Schedule).ThenInclude(s => s.DepreciationRun)
                    .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(FixedAsset), request.Id);

        return new FixedAssetDto(
            a.Id, a.AssetNumber, a.NameAr, a.NameEn, a.BranchId, a.CategoryId, a.Category!.NameAr, a.SerialNumber, a.Barcode, a.Location,
            a.AcquisitionDate, a.AcquisitionCost, a.CurrencyCode, a.ExchangeRate, a.BaseCurrencyAmount, a.SupplierId, a.PurchaseInvoiceId,
            a.FundingAccountId, a.AcquisitionJournalEntryId, a.UsefulLifeYears, a.SalvageValue, a.DepreciationMethod, a.DepreciationRate,
            a.DepreciationStartDate, a.FirstMonthProrated, a.AccumulatedDepreciation, a.NetBookValue, a.Status, a.DisposalDate,
            a.DisposalReason, a.DisposalProceeds, a.DisposalJournalEntryId, a.CustodyOfficerId, a.CustodyOfficerName, a.CostCenterValueId,
            a.Notes, Convert.ToBase64String(a.RowVersion),
            a.Schedule.OrderBy(s => s.PeriodNumber).Select(AssetMap.Period).ToList());
    }
}

/// <summary>Screen #3: the depreciation periods across assets, optionally for one asset or one month.</summary>
public sealed record GetDepreciationScheduleQuery(long? FixedAssetId, int? Year, int? Month, DepreciationScheduleStatus? Status)
    : IRequest<IReadOnlyList<DepreciationScheduleRowDto>>;

public sealed record DepreciationScheduleRowDto(long FixedAssetId, string AssetNumber, string AssetNameAr, DepreciationPeriodDto Period);

public sealed class GetDepreciationScheduleQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDepreciationScheduleQuery, IReadOnlyList<DepreciationScheduleRowDto>>
{
    public async Task<IReadOnlyList<DepreciationScheduleRowDto>> Handle(GetDepreciationScheduleQuery request, CancellationToken cancellationToken)
    {
        var rows = db.DepreciationSchedules.AsNoTracking().Include(s => s.FixedAsset).Include(s => s.DepreciationRun).AsQueryable();
        if (request.FixedAssetId is { } assetId) rows = rows.Where(s => s.FixedAssetId == assetId);
        if (request.Status is { } status) rows = rows.Where(s => s.Status == status);
        if (request.Year is { } year && request.Month is { } month)
        {
            var from = new DateOnly(year, month, 1);
            var to = from.AddMonths(1).AddDays(-1);
            rows = rows.Where(s => s.PeriodEnd >= from && s.PeriodEnd <= to);
        }

        return (await rows.OrderBy(s => s.FixedAsset!.AssetNumber).ThenBy(s => s.PeriodNumber).Take(5000).ToListAsync(cancellationToken))
            .Select(s => new DepreciationScheduleRowDto(s.FixedAssetId, s.FixedAsset!.AssetNumber, s.FixedAsset.NameAr, AssetMap.Period(s)))
            .ToList();
    }
}

internal static class AssetMap
{
    public static DepreciationPeriodDto Period(DepreciationSchedule s) => new(
        s.Id, s.PeriodNumber, s.PeriodStart, s.PeriodEnd, s.Amount, s.AccumulatedAfter, s.BookValueAfter, s.Status,
        s.DepreciationRunId, s.DepreciationRun?.RunNumber, s.JournalEntryId);
}

// ------------------------------------------------------------------ create / update / delete

public sealed record CreateFixedAssetCommand(string? AssetNumber, FixedAssetInput Data) : IRequest<long>;
public sealed record UpdateFixedAssetCommand(long Id, string RowVersion, FixedAssetInput Data) : IRequest;
public sealed record DeleteFixedAssetCommand(long Id) : IRequest;

public sealed class FixedAssetInputValidator : AbstractValidator<FixedAssetInput>
{
    public FixedAssetInputValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.AcquisitionDate).NotEqual(default(DateOnly));
        RuleFor(x => x.AcquisitionCost).GreaterThan(0).WithMessage("تكلفة الاقتناء لازم تكون أكبر من صفر.");   // rule 2
        RuleFor(x => x.ExchangeRate).GreaterThan(0);
        RuleFor(x => x.UsefulLifeYears).GreaterThan(0).When(x => x.UsefulLifeYears is not null);
        RuleFor(x => x.SalvageValue).GreaterThanOrEqualTo(0).When(x => x.SalvageValue is not null);
        RuleFor(x => x.DepreciationRate).GreaterThan(0).LessThanOrEqualTo(100).When(x => x.DepreciationRate is not null);
    }
}

public sealed class CreateFixedAssetCommandValidator : AbstractValidator<CreateFixedAssetCommand>
{
    public CreateFixedAssetCommandValidator() => RuleFor(x => x.Data).SetValidator(new FixedAssetInputValidator());
}

public sealed class UpdateFixedAssetCommandValidator : AbstractValidator<UpdateFixedAssetCommand>
{
    public UpdateFixedAssetCommandValidator() => RuleFor(x => x.Data).SetValidator(new FixedAssetInputValidator());
}

public sealed class FixedAssetCommandsHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codes)
    : IRequestHandler<CreateFixedAssetCommand, long>, IRequestHandler<UpdateFixedAssetCommand>, IRequestHandler<DeleteFixedAssetCommand>
{
    public async Task<long> Handle(CreateFixedAssetCommand request, CancellationToken cancellationToken)
    {
        var number = await codes.ResolveCodeAsync(FixedAssetScreens.Assets, request.AssetNumber, cancellationToken);
        if (await db.FixedAssets.AnyAsync(a => a.AssetNumber == number, cancellationToken))
        {
            throw new BusinessRuleException("FA-ASSET-NUMBER-EXISTS", "فيه أصل تاني بنفس الرقم.");   // rule 1
        }

        var settings = await FixedAssetRules.SettingsAsync(db, current.CompanyId, cancellationToken);
        var asset = new FixedAsset
        {
            CompanyId = current.CompanyId, AssetNumber = number, Status = FixedAssetStatus.Draft,
            FirstMonthProrated = request.Data.FirstMonthProrated ?? settings.DefaultFirstMonthProrated   // rule 31
        };
        await ApplyAsync(asset, request.Data, financial: true, cancellationToken);
        db.FixedAssets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);
        return asset.Id;
    }

    public async Task Handle(UpdateFixedAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await FixedAssetRules.FindAssetAsync(db, request.Id, cancellationToken);
        if (asset.Status is FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff)
        {
            throw new BusinessRuleException("FA-ASSET-CLOSED", "الأصل مستبعد — مفيش تعديل عليه.");
        }

        db.Entry(asset).Property(nameof(FixedAsset.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        // After activation the cost and the depreciation are fixed (rule 9); revaluation is not in this phase.
        await ApplyAsync(asset, request.Data, financial: asset.Status == FixedAssetStatus.Draft, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(DeleteFixedAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await FixedAssetRules.FindAssetAsync(db, request.Id, cancellationToken);
        if (asset.Status != FixedAssetStatus.Draft)
        {
            throw new BusinessRuleException("FA-ASSET-NOT-DRAFT", "الأصل بعد الاعتماد مابيتحذفش — بيتغيّر حالته بس (استبعاد).");   // rule 8
        }

        db.FixedAssets.Remove(asset);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyAsync(FixedAsset asset, FixedAssetInput d, bool financial, CancellationToken ct)
    {
        asset.NameAr = d.NameAr.Trim();
        asset.NameEn = d.NameEn.Trim();
        asset.SerialNumber = d.SerialNumber;
        asset.Barcode = d.Barcode;
        asset.Location = d.Location;
        asset.Notes = d.Notes;
        asset.CostCenterValueId = d.CostCenterValueId;

        if (d.CustodyOfficerId is { } officerId)
        {
            var officer = await db.CustodyOfficers.FirstOrDefaultAsync(o => o.Id == officerId, ct)
                          ?? throw new NotFoundException("CustodyOfficer", officerId);
            if (asset.CustodyOfficerId != officerId)
            {
                asset.CustodyOfficerName = officer.NameAr;
            }
        }
        else
        {
            asset.CustodyOfficerName = null;
        }

        asset.CustodyOfficerId = d.CustodyOfficerId;

        if (!financial)
        {
            return;
        }

        var category = await db.FixedAssetCategories.FirstOrDefaultAsync(c => c.Id == d.CategoryId, ct)
                       ?? throw new NotFoundException(nameof(FixedAssetCategory), d.CategoryId);
        if (!category.IsActive)
        {
            throw new BusinessRuleException("FA-CATEGORY-INACTIVE", "الفئة موقوفة.");
        }

        var baseCurrency = await db.Companies.Where(c => c.Id == current.CompanyId).Select(c => c.BaseCurrency.Code).FirstAsync(ct);
        var currency = string.IsNullOrWhiteSpace(d.CurrencyCode) ? baseCurrency : d.CurrencyCode.Trim().ToUpperInvariant();

        asset.BranchId = d.BranchId;
        asset.CategoryId = category.Id;
        asset.AcquisitionDate = d.AcquisitionDate;
        asset.AcquisitionCost = d.AcquisitionCost;
        asset.CurrencyCode = currency;
        asset.ExchangeRate = currency == baseCurrency ? 1m : d.ExchangeRate;
        asset.BaseCurrencyAmount = Math.Round(asset.AcquisitionCost * asset.ExchangeRate, 2);   // rule 30
        asset.SupplierId = d.SupplierId;
        asset.PurchaseInvoiceId = d.PurchaseInvoiceId;
        asset.FundingAccountId = d.FundingAccountId;

        // The category supplies whatever the form left out.
        asset.DepreciationMethod = d.DepreciationMethod ?? category.DepreciationMethod;
        asset.UsefulLifeYears = d.UsefulLifeYears ?? category.DefaultUsefulLifeYears;
        asset.DepreciationRate = asset.DepreciationMethod == DepreciationMethod.DecliningBalance
            ? d.DepreciationRate ?? category.DefaultDepreciationRate
            : null;
        asset.SalvageValue = d.SalvageValue
                             ?? Math.Round(asset.BaseCurrencyAmount * (category.DefaultSalvagePercentage ?? 0m) / 100m, 2);
        asset.DepreciationStartDate = d.DepreciationStartDate ?? d.AcquisitionDate;
        if (d.FirstMonthProrated is { } prorated)
        {
            asset.FirstMonthProrated = prorated;
        }

        if (asset.DepreciationMethod != DepreciationMethod.NoDepreciation && asset.UsefulLifeYears is not > 0)
        {
            throw new BusinessRuleException("FA-USEFUL-LIFE-REQUIRED", "العمر الإنتاجي لازم يكون أكبر من صفر (إلا لو الأصل مابيتهلكش).");   // rule 3
        }

        if (asset.DepreciationMethod == DepreciationMethod.DecliningBalance && asset.DepreciationRate is not > 0)
        {
            throw new BusinessRuleException("FA-RATE-REQUIRED", "طريقة القسط المتناقص محتاجة نسبة إهلاك أكبر من صفر.");   // rule 4
        }

        if (asset.SalvageValue >= asset.BaseCurrencyAmount)
        {
            throw new BusinessRuleException("FA-SALVAGE-TOO-HIGH", "القيمة المتبقية لازم تكون أقل من تكلفة الأصل.");   // rule 5
        }
    }
}

// ------------------------------------------------------------------ activation (acquisition entry + schedule)

/// <summary>
/// Capitalises a Draft asset: posts the acquisition (rule 10) through the engine, lays out its
/// depreciation schedule, and freezes the cost (rule 9).
/// </summary>
public sealed record ActivateFixedAssetCommand(long Id) : IRequest;

public sealed class ActivateFixedAssetCommandHandler(IApplicationDbContext db, IPostingTemplateEngine posting, ICurrentCompanyContext current)
    : IRequestHandler<ActivateFixedAssetCommand>
{
    public async Task Handle(ActivateFixedAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = await FixedAssetRules.FindAssetAsync(db, request.Id, cancellationToken);
        if (asset.Status != FixedAssetStatus.Draft)
        {
            throw new BusinessRuleException("FA-ASSET-NOT-DRAFT", "الأصل اتعمد قبل كده.");
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        foreach (var period in DepreciationCalculator.Build(
                     asset.BaseCurrencyAmount, asset.SalvageValue, asset.DepreciationMethod, asset.UsefulLifeYears, asset.DepreciationRate,
                     asset.DepreciationStartDate, asset.FirstMonthProrated))
        {
            asset.Schedule.Add(new DepreciationSchedule
            {
                PeriodNumber = period.Number, PeriodStart = period.Start, PeriodEnd = period.End, Amount = period.Amount,
                AccumulatedAfter = period.AccumulatedAfter, BookValueAfter = period.BookValueAfter
            });
        }

        asset.Status = FixedAssetStatus.Active;

        if (await posting.IsConfiguredAsync(current.CompanyId, FixedAssetScreens.Assets, cancellationToken))
        {
            if (asset.FundingAccountId is null)
            {
                throw new BusinessRuleException("FA-FUNDING-ACCOUNT-REQUIRED", "حدد حساب التمويل (المورد أو الخزينة) قبل اعتماد الأصل — القيد دائن عليه.");
            }

            asset.AcquisitionJournalEntry = await posting.PostIfConfiguredAsync(new TemplatePostingRequest
            {
                CompanyId = current.CompanyId,
                BranchId = asset.BranchId,
                ScreenCode = FixedAssetScreens.Assets,
                SourceModule = SourceModule.FixedAssets,
                SourceDocumentType = SourceDocumentType.FixedAssetAcquisition,
                SourceDocumentId = asset.Id,
                EntryDate = asset.AcquisitionDate,
                Description = $"اقتناء أصل {asset.AssetNumber} — {asset.NameAr}",
                IdempotencyKey = PostingKeys.For(current.CompanyId, "FixedAsset.Acquisition", asset.Id),
                Dimensions = await FixedAssetRules.CostCenterAsync(db, asset, cancellationToken),
                Context = PostingContext.Create(new Dictionary<string, object?>
                {
                    ["BranchId"] = asset.BranchId,
                    ["FixedAssetId"] = asset.Id,
                    ["SupplierId"] = asset.SupplierId,
                    ["AcquisitionAmount"] = asset.BaseCurrencyAmount,
                    ["AssetAccountId"] = asset.Category!.AssetAccountId,
                    ["FundingAccountId"] = asset.FundingAccountId
                })
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
