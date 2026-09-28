using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Items.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Items.Commands.UpdateItem;

/// <summary>Update replaces the whole set of UnitConversions/WarehouseSettings for the item in one
/// call (diff-and-replace), matching the Item edit screen's single Save button. IsActive is not a
/// caller-supplied field: it is derived from Status (Active -> true, everything else -> false).</summary>
public sealed record UpdateItemCommand(
    long Id,
    string NameAr,
    string NameEn,
    long? ItemGroupId,
    long? POSCategoryId,
    ItemType ItemType,
    string? Barcode,
    long BaseUnitOfMeasureId,
    long? PurchaseUnitOfMeasureId,
    long? SellUnitOfMeasureId,
    SaleMethod SaleMethod,
    CostMethod CostMethod,
    decimal? DefaultPrice,
    bool IsStocked,
    bool IsTracked,
    bool TrackSerial,
    int? ShelfLifeDays,
    decimal? StandardCost,
    bool IsPurchasable,
    bool IsSellable,
    bool IsManufacturable,
    bool AllowSubstitutes,
    ItemStatus Status,
    IReadOnlyList<ItemUnitConversionInput> UnitConversions,
    IReadOnlyList<ItemWarehouseSettingsInput> WarehouseSettings,
    IReadOnlyList<BranchItemLimitInput> BranchItemLimits,
    string? TaxCode = null) : IRequest;

public sealed class UpdateItemCommandValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Barcode).MaximumLength(100);
        RuleFor(x => x.TaxCode).MaximumLength(50);
        RuleFor(x => x.BaseUnitOfMeasureId).GreaterThan(0);
        RuleFor(x => x.ShelfLifeDays).GreaterThan(0).When(x => x.ShelfLifeDays.HasValue);
        RuleFor(x => x.StandardCost).GreaterThanOrEqualTo(0).When(x => x.StandardCost.HasValue);
        RuleFor(x => x.DefaultPrice).GreaterThanOrEqualTo(0).When(x => x.DefaultPrice.HasValue);

        RuleForEach(x => x.UnitConversions).ChildRules(rules =>
        {
            rules.RuleFor(x => x.AlternateUnitOfMeasureId).GreaterThan(0);
            rules.RuleFor(x => x.ConversionFactor).GreaterThan(0);
        });
        RuleFor(x => x.UnitConversions)
            .Must(list => list.Select(x => x.AlternateUnitOfMeasureId).Distinct().Count() == list.Count)
            .WithMessage("لا يمكن تكرار نفس وحدة القياس البديلة أكثر من مرة.");

        RuleForEach(x => x.WarehouseSettings).ChildRules(rules =>
        {
            rules.RuleFor(x => x.WarehouseId).GreaterThan(0);
            rules.RuleFor(x => x.MinStockLevel).GreaterThanOrEqualTo(0).When(x => x.MinStockLevel.HasValue);
            rules.RuleFor(x => x.MaxStockLevel).GreaterThanOrEqualTo(0).When(x => x.MaxStockLevel.HasValue);
            rules.RuleFor(x => x.ReorderPoint).GreaterThanOrEqualTo(0).When(x => x.ReorderPoint.HasValue);
        });
        RuleFor(x => x.WarehouseSettings)
            .Must(list => list.Select(x => x.WarehouseId).Distinct().Count() == list.Count)
            .WithMessage("لا يمكن تكرار نفس المخزن أكثر من مرة.");

        RuleForEach(x => x.BranchItemLimits).ChildRules(rules =>
        {
            rules.RuleFor(x => x.BranchId).GreaterThan(0);
            rules.RuleFor(x => x.MinRequestQuantity).GreaterThanOrEqualTo(0).When(x => x.MinRequestQuantity.HasValue);
            rules.RuleFor(x => x.MaxRequestQuantity).GreaterThan(0);
            rules.RuleFor(x => x)
                .Must(x => x.MinRequestQuantity is null || x.MinRequestQuantity <= x.MaxRequestQuantity)
                .WithMessage("الحد الأدنى لا يمكن أن يتجاوز الحد الأقصى.")
                .WithName("MinRequestQuantity");
        });
        RuleFor(x => x.BranchItemLimits)
            .Must(list => list.Select(x => x.BranchId).Distinct().Count() == list.Count)
            .WithMessage("لا يمكن تكرار نفس الفرع أكثر من مرة.");
    }
}

public sealed class UpdateItemCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateItemCommand>
{
    public async Task Handle(UpdateItemCommand request, CancellationToken cancellationToken)
    {
        var item = await db.Items
            .Include(i => i.UnitConversions)
            .Include(i => i.WarehouseSettings)
            .Include(i => i.BranchItemLimits)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Item), request.Id);

        var baseUnitExists = await db.UnitsOfMeasure.AnyAsync(u => u.Id == request.BaseUnitOfMeasureId, cancellationToken);
        if (!baseUnitExists)
        {
            throw new NotFoundException(nameof(UnitOfMeasure), request.BaseUnitOfMeasureId);
        }

        if (request.ItemGroupId is { } itemGroupId && !await db.ItemGroups.AnyAsync(g => g.Id == itemGroupId, cancellationToken))
        {
            throw new NotFoundException(nameof(ItemGroup), itemGroupId);
        }

        if (request.POSCategoryId is { } posCategoryId && !await db.POSCategories.AnyAsync(c => c.Id == posCategoryId, cancellationToken))
        {
            throw new NotFoundException(nameof(POSCategory), posCategoryId);
        }

        if (request.PurchaseUnitOfMeasureId is { } purchaseUnitId && !await db.UnitsOfMeasure.AnyAsync(u => u.Id == purchaseUnitId, cancellationToken))
        {
            throw new NotFoundException(nameof(UnitOfMeasure), purchaseUnitId);
        }

        if (request.SellUnitOfMeasureId is { } sellUnitId && !await db.UnitsOfMeasure.AnyAsync(u => u.Id == sellUnitId, cancellationToken))
        {
            throw new NotFoundException(nameof(UnitOfMeasure), sellUnitId);
        }

        if (request.UnitConversions.Any(c => c.AlternateUnitOfMeasureId == request.BaseUnitOfMeasureId))
        {
            throw new BusinessRuleException("INV-ITEM-CONVERSION-SAME-AS-BASE", "لا يمكن إضافة تحويل لنفس الوحدة الأساسية للصنف.");
        }

        var alternateUnitIds = request.UnitConversions.Select(c => c.AlternateUnitOfMeasureId).ToList();
        var existingAlternateUnitCount = await db.UnitsOfMeasure.CountAsync(u => alternateUnitIds.Contains(u.Id), cancellationToken);
        if (existingAlternateUnitCount != alternateUnitIds.Distinct().Count())
        {
            throw new BusinessRuleException("INV-ITEM-CONVERSION-UNIT-NOT-FOUND", "إحدى وحدات القياس البديلة غير موجودة.");
        }

        var warehouseIds = request.WarehouseSettings.Select(w => w.WarehouseId).ToList();
        var existingWarehouseCount = await db.Warehouses.CountAsync(w => warehouseIds.Contains(w.Id), cancellationToken);
        if (existingWarehouseCount != warehouseIds.Distinct().Count())
        {
            throw new BusinessRuleException("INV-ITEM-WAREHOUSE-NOT-FOUND", "أحد المخازن المحددة غير موجود.");
        }

        item.NameAr = request.NameAr;
        item.NameEn = request.NameEn;
        item.ItemGroupId = request.ItemGroupId;
        item.POSCategoryId = request.POSCategoryId;
        item.ItemType = request.ItemType;
        item.Barcode = request.Barcode;
        item.TaxCode = string.IsNullOrWhiteSpace(request.TaxCode) ? null : request.TaxCode.Trim();
        item.BaseUnitOfMeasureId = request.BaseUnitOfMeasureId;
        item.PurchaseUnitOfMeasureId = request.PurchaseUnitOfMeasureId;
        item.SellUnitOfMeasureId = request.SellUnitOfMeasureId;
        item.SaleMethod = request.SaleMethod;
        item.CostMethod = request.CostMethod;
        item.DefaultPrice = request.DefaultPrice;
        item.IsStocked = request.IsStocked;
        item.IsTracked = request.IsTracked;
        item.TrackSerial = request.TrackSerial;
        item.ShelfLifeDays = request.ShelfLifeDays;
        item.StandardCost = request.StandardCost;
        item.IsPurchasable = request.IsPurchasable;
        item.IsSellable = request.IsSellable;
        item.IsManufacturable = request.IsManufacturable;
        item.AllowSubstitutes = request.AllowSubstitutes;
        item.Status = request.Status;
        item.IsActive = request.Status == ItemStatus.Active;

        item.UnitConversions.Clear();
        foreach (var conversion in request.UnitConversions)
        {
            item.UnitConversions.Add(new ItemUnitConversion
            {
                ItemId = item.Id,
                AlternateUnitOfMeasureId = conversion.AlternateUnitOfMeasureId,
                ConversionFactor = conversion.ConversionFactor
            });
        }

        item.WarehouseSettings.Clear();
        foreach (var settings in request.WarehouseSettings)
        {
            item.WarehouseSettings.Add(new ItemWarehouseSettings
            {
                ItemId = item.Id,
                WarehouseId = settings.WarehouseId,
                MinStockLevel = settings.MinStockLevel,
                MaxStockLevel = settings.MaxStockLevel,
                ReorderPoint = settings.ReorderPoint
            });
        }

        item.BranchItemLimits.Clear();
        foreach (var limit in request.BranchItemLimits)
        {
            item.BranchItemLimits.Add(new BranchItemLimit
            {
                ItemId = item.Id,
                BranchId = limit.BranchId,
                MinRequestQuantity = limit.MinRequestQuantity,
                MaxRequestQuantity = limit.MaxRequestQuantity
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
