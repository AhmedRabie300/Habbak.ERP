using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.Items.Commands.CreateItem;

public sealed record CreateItemCommand(
    string? Code,
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
    string? TaxCode = null) : IRequest<long>;

public sealed class CreateItemCommandValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Barcode).MaximumLength(100);
        RuleFor(x => x.TaxCode).MaximumLength(50);
        RuleFor(x => x.BaseUnitOfMeasureId).GreaterThan(0);
        RuleFor(x => x.ShelfLifeDays).GreaterThan(0).When(x => x.ShelfLifeDays.HasValue);
        RuleFor(x => x.StandardCost).GreaterThanOrEqualTo(0).When(x => x.StandardCost.HasValue);
        RuleFor(x => x.DefaultPrice).GreaterThanOrEqualTo(0).When(x => x.DefaultPrice.HasValue);
    }
}

public sealed class CreateItemCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateItemCommand, long>
{
    public async Task<long> Handle(CreateItemCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("INVENTORY_ITEMS", request.Code, cancellationToken);

        var codeExists = await db.Items
            .AnyAsync(i => i.CompanyId == currentCompanyContext.CompanyId && i.Code == code, cancellationToken);

        if (codeExists)
        {
            throw new BusinessRuleException("INV-ITEM-CODE-EXISTS", "يوجد صنف آخر بنفس الكود بالفعل.");
        }

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

        var item = new Item
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            ItemGroupId = request.ItemGroupId,
            POSCategoryId = request.POSCategoryId,
            ItemType = request.ItemType,
            Barcode = request.Barcode,
            TaxCode = string.IsNullOrWhiteSpace(request.TaxCode) ? null : request.TaxCode.Trim(),
            BaseUnitOfMeasureId = request.BaseUnitOfMeasureId,
            PurchaseUnitOfMeasureId = request.PurchaseUnitOfMeasureId,
            SellUnitOfMeasureId = request.SellUnitOfMeasureId,
            SaleMethod = request.SaleMethod,
            CostMethod = request.CostMethod,
            DefaultPrice = request.DefaultPrice,
            IsStocked = request.IsStocked,
            IsTracked = request.IsTracked,
            TrackSerial = request.TrackSerial,
            ShelfLifeDays = request.ShelfLifeDays,
            StandardCost = request.StandardCost,
            IsPurchasable = request.IsPurchasable,
            IsSellable = request.IsSellable,
            IsManufacturable = request.IsManufacturable,
            AllowSubstitutes = request.AllowSubstitutes,
            Status = request.Status,
            IsActive = request.Status == ItemStatus.Active
        };

        db.Items.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
