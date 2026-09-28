using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.FixedAssets;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.FixedAssets;

// ====================================================================== asset categories (screen #1)

public sealed record FixedAssetCategoryDto(
    long Id, string Code, string NameAr, string NameEn, DepreciationMethod DepreciationMethod, decimal? DefaultDepreciationRate,
    int? DefaultUsefulLifeYears, decimal? DefaultSalvagePercentage, long AssetAccountId, long AccumulatedDepreciationAccountId,
    long DepreciationExpenseAccountId, long? DisposalGainAccountId, long? DisposalLossAccountId, long MaintenanceExpenseAccountId,
    bool IsActive, string RowVersion);

public sealed record FixedAssetCategoryInput(
    string NameAr, string NameEn, DepreciationMethod DepreciationMethod, decimal? DefaultDepreciationRate, int? DefaultUsefulLifeYears,
    decimal? DefaultSalvagePercentage, long AssetAccountId, long AccumulatedDepreciationAccountId, long DepreciationExpenseAccountId,
    long? DisposalGainAccountId, long? DisposalLossAccountId, long MaintenanceExpenseAccountId, bool IsActive);

public sealed record GetFixedAssetCategoriesQuery : IRequest<IReadOnlyList<FixedAssetCategoryDto>>;

public sealed class GetFixedAssetCategoriesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetFixedAssetCategoriesQuery, IReadOnlyList<FixedAssetCategoryDto>>
{
    public async Task<IReadOnlyList<FixedAssetCategoryDto>> Handle(GetFixedAssetCategoriesQuery request, CancellationToken cancellationToken) =>
        (await db.FixedAssetCategories.AsNoTracking().OrderBy(c => c.Code).ToListAsync(cancellationToken)).Select(CategoryMap.ToDto).ToList();
}

public sealed record GetFixedAssetCategoryQuery(long Id) : IRequest<FixedAssetCategoryDto>;

public sealed class GetFixedAssetCategoryQueryHandler(IApplicationDbContext db) : IRequestHandler<GetFixedAssetCategoryQuery, FixedAssetCategoryDto>
{
    public async Task<FixedAssetCategoryDto> Handle(GetFixedAssetCategoryQuery request, CancellationToken cancellationToken) =>
        CategoryMap.ToDto(await db.FixedAssetCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
                          ?? throw new NotFoundException(nameof(FixedAssetCategory), request.Id));
}

public sealed record CreateFixedAssetCategoryCommand(string? Code, FixedAssetCategoryInput Data) : IRequest<long>;
public sealed record UpdateFixedAssetCategoryCommand(long Id, string RowVersion, FixedAssetCategoryInput Data) : IRequest;
public sealed record DeleteFixedAssetCategoryCommand(long Id) : IRequest;

public sealed class FixedAssetCategoryInputValidator : AbstractValidator<FixedAssetCategoryInput>
{
    public FixedAssetCategoryInputValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AssetAccountId).GreaterThan(0);
        RuleFor(x => x.AccumulatedDepreciationAccountId).GreaterThan(0);
        RuleFor(x => x.DepreciationExpenseAccountId).GreaterThan(0);
        RuleFor(x => x.MaintenanceExpenseAccountId).GreaterThan(0);
        RuleFor(x => x.DefaultUsefulLifeYears).GreaterThan(0).When(x => x.DefaultUsefulLifeYears is not null);
        RuleFor(x => x.DefaultSalvagePercentage).InclusiveBetween(0, 99.99m).When(x => x.DefaultSalvagePercentage is not null);
        // A declining balance cannot be computed without its rate (corrected spec, 2.1.1).
        RuleFor(x => x.DefaultDepreciationRate).NotNull().GreaterThan(0).LessThanOrEqualTo(100)
            .When(x => x.DepreciationMethod == DepreciationMethod.DecliningBalance)
            .WithMessage("طريقة القسط المتناقص محتاجة نسبة إهلاك أكبر من صفر.");
    }
}

public sealed class CreateFixedAssetCategoryCommandValidator : AbstractValidator<CreateFixedAssetCategoryCommand>
{
    public CreateFixedAssetCategoryCommandValidator() => RuleFor(x => x.Data).SetValidator(new FixedAssetCategoryInputValidator());
}

public sealed class UpdateFixedAssetCategoryCommandValidator : AbstractValidator<UpdateFixedAssetCategoryCommand>
{
    public UpdateFixedAssetCategoryCommandValidator() => RuleFor(x => x.Data).SetValidator(new FixedAssetCategoryInputValidator());
}

public sealed class FixedAssetCategoryCommandsHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codes)
    : IRequestHandler<CreateFixedAssetCategoryCommand, long>, IRequestHandler<UpdateFixedAssetCategoryCommand>, IRequestHandler<DeleteFixedAssetCategoryCommand>
{
    public async Task<long> Handle(CreateFixedAssetCategoryCommand request, CancellationToken cancellationToken)
    {
        var code = await codes.ResolveCodeAsync(FixedAssetScreens.Categories, request.Code, cancellationToken);
        if (await db.FixedAssetCategories.AnyAsync(c => c.Code == code, cancellationToken))
        {
            throw new BusinessRuleException("FA-CATEGORY-CODE-EXISTS", "فيه فئة أصول تانية بنفس الكود.");
        }

        await EnsureAccountsAsync(request.Data, cancellationToken);
        var category = new FixedAssetCategory { CompanyId = current.CompanyId, Code = code };
        Apply(category, request.Data);
        db.FixedAssetCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    public async Task Handle(UpdateFixedAssetCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await db.FixedAssetCategories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
                       ?? throw new NotFoundException(nameof(FixedAssetCategory), request.Id);
        await EnsureAccountsAsync(request.Data, cancellationToken);
        db.Entry(category).Property(nameof(FixedAssetCategory.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);
        Apply(category, request.Data);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task Handle(DeleteFixedAssetCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await db.FixedAssetCategories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
                       ?? throw new NotFoundException(nameof(FixedAssetCategory), request.Id);
        if (await db.FixedAssets.AnyAsync(a => a.CategoryId == category.Id, cancellationToken))
        {
            throw new BusinessRuleException("FA-CATEGORY-IN-USE", "الفئة عليها أصول — اوقفها بدل ما تحذفها.");
        }

        db.FixedAssetCategories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Apply(FixedAssetCategory c, FixedAssetCategoryInput d)
    {
        c.NameAr = d.NameAr.Trim();
        c.NameEn = d.NameEn.Trim();
        c.DepreciationMethod = d.DepreciationMethod;
        c.DefaultDepreciationRate = d.DepreciationMethod == DepreciationMethod.DecliningBalance ? d.DefaultDepreciationRate : null;
        c.DefaultUsefulLifeYears = d.DefaultUsefulLifeYears;
        c.DefaultSalvagePercentage = d.DefaultSalvagePercentage;
        c.AssetAccountId = d.AssetAccountId;
        c.AccumulatedDepreciationAccountId = d.AccumulatedDepreciationAccountId;
        c.DepreciationExpenseAccountId = d.DepreciationExpenseAccountId;
        c.DisposalGainAccountId = d.DisposalGainAccountId;
        c.DisposalLossAccountId = d.DisposalLossAccountId;
        c.MaintenanceExpenseAccountId = d.MaintenanceExpenseAccountId;
        c.IsActive = d.IsActive;
    }

    /// <summary>Every account must exist in the company and accept postings.</summary>
    private async Task EnsureAccountsAsync(FixedAssetCategoryInput d, CancellationToken ct)
    {
        long?[] ids = [d.AssetAccountId, d.AccumulatedDepreciationAccountId, d.DepreciationExpenseAccountId, d.DisposalGainAccountId, d.DisposalLossAccountId, d.MaintenanceExpenseAccountId];
        var wanted = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        var postable = await db.Accounts.Where(a => wanted.Contains(a.Id) && a.IsPostable).Select(a => a.Id).ToListAsync(ct);
        if (postable.Count != wanted.Count)
        {
            throw new BusinessRuleException("FA-CATEGORY-ACCOUNT-INVALID", "حسابات الفئة لازم تكون موجودة وتقبل الترحيل (حسابات فرعية).");
        }
    }
}

internal static class CategoryMap
{
    public static FixedAssetCategoryDto ToDto(FixedAssetCategory c) => new(
        c.Id, c.Code, c.NameAr, c.NameEn, c.DepreciationMethod, c.DefaultDepreciationRate, c.DefaultUsefulLifeYears, c.DefaultSalvagePercentage,
        c.AssetAccountId, c.AccumulatedDepreciationAccountId, c.DepreciationExpenseAccountId, c.DisposalGainAccountId, c.DisposalLossAccountId,
        c.MaintenanceExpenseAccountId, c.IsActive, Convert.ToBase64String(c.RowVersion));
}

// ====================================================================== maintenance categories (screen #8)

public sealed record MaintenanceCategoryDto(long Id, string Code, string NameAr, string NameEn, MaintenanceType MaintenanceType, bool IsActive);

public sealed record GetMaintenanceCategoriesQuery : IRequest<IReadOnlyList<MaintenanceCategoryDto>>;

public sealed class GetMaintenanceCategoriesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetMaintenanceCategoriesQuery, IReadOnlyList<MaintenanceCategoryDto>>
{
    public async Task<IReadOnlyList<MaintenanceCategoryDto>> Handle(GetMaintenanceCategoriesQuery request, CancellationToken cancellationToken) =>
        await db.MaintenanceCategories.AsNoTracking().OrderBy(c => c.Code)
            .Select(c => new MaintenanceCategoryDto(c.Id, c.Code, c.NameAr, c.NameEn, c.MaintenanceType, c.IsActive))
            .ToListAsync(cancellationToken);
}

public sealed record SaveMaintenanceCategoryCommand(long? Id, string? Code, string NameAr, string NameEn, MaintenanceType MaintenanceType, bool IsActive) : IRequest<long>;
public sealed record DeleteMaintenanceCategoryCommand(long Id) : IRequest;

public sealed class SaveMaintenanceCategoryCommandValidator : AbstractValidator<SaveMaintenanceCategoryCommand>
{
    public SaveMaintenanceCategoryCommandValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MaintenanceType).IsInEnum();
    }
}

public sealed class MaintenanceCategoryCommandsHandler(IApplicationDbContext db, ICurrentCompanyContext current, ICodeGenerator codes)
    : IRequestHandler<SaveMaintenanceCategoryCommand, long>, IRequestHandler<DeleteMaintenanceCategoryCommand>
{
    public async Task<long> Handle(SaveMaintenanceCategoryCommand request, CancellationToken cancellationToken)
    {
        MaintenanceCategory category;
        if (request.Id is { } id)
        {
            category = await db.MaintenanceCategories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                       ?? throw new NotFoundException(nameof(MaintenanceCategory), id);
        }
        else
        {
            var code = await codes.ResolveCodeAsync(FixedAssetScreens.MaintenanceCategories, request.Code, cancellationToken);
            if (await db.MaintenanceCategories.AnyAsync(c => c.Code == code, cancellationToken))
            {
                throw new BusinessRuleException("MAINT-CATEGORY-CODE-EXISTS", "فيه فئة صيانة تانية بنفس الكود.");
            }

            category = new MaintenanceCategory { CompanyId = current.CompanyId, Code = code };
            db.MaintenanceCategories.Add(category);
        }

        category.NameAr = request.NameAr.Trim();
        category.NameEn = request.NameEn.Trim();
        category.MaintenanceType = request.MaintenanceType;
        category.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    public async Task Handle(DeleteMaintenanceCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await db.MaintenanceCategories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
                       ?? throw new NotFoundException(nameof(MaintenanceCategory), request.Id);
        if (await db.MaintenanceRequests.AnyAsync(r => r.MaintenanceCategoryId == category.Id, cancellationToken)
            || await db.MaintenanceSchedules.AnyAsync(s => s.MaintenanceCategoryId == category.Id, cancellationToken))
        {
            throw new BusinessRuleException("MAINT-CATEGORY-IN-USE", "الفئة مستخدمة — اوقفها بدل ما تحذفها.");
        }

        db.MaintenanceCategories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }
}
