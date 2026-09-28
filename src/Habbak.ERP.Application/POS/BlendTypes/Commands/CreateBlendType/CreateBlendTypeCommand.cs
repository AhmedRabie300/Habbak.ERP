using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.BlendTypes.Commands.CreateBlendType;

/// <summary>مراجعة 2026-09-13، بند 2.1 — بينشئ صف BlendType + الصنف الخلفي (RawMaterial, غير مباع
/// من شاشة البيع مباشرة: IsSellable=false) اللي هيُستخدَم لاحقًا كـItemId في QRTicketLine/CheckLine
/// وقت إصدار/مسح تذكرة الخلطة، في نفس الـTransaction.</summary>
public sealed record CreateBlendTypeCommand(string? Code, string NameAr, string NameEn, decimal PricePerGram) : IRequest<long>;

public sealed class CreateBlendTypeCommandValidator : AbstractValidator<CreateBlendTypeCommand>
{
    public CreateBlendTypeCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PricePerGram).GreaterThan(0);
    }
}

public sealed class CreateBlendTypeCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateBlendTypeCommand, long>
{
    public async Task<long> Handle(CreateBlendTypeCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("POS_BLEND_TYPES", request.Code, cancellationToken);

        var codeExists = await db.BlendTypes
            .AnyAsync(b => b.CompanyId == currentCompanyContext.CompanyId && b.Code == code, cancellationToken);

        if (codeExists)
        {
            throw new BusinessRuleException("POS-BLEND-TYPE-CODE-EXISTS", "يوجد نوع خلطة آخر بنفس الكود بالفعل.");
        }

        var gramUnit = await db.UnitsOfMeasure.FirstOrDefaultAsync(u => u.Code == "GM", cancellationToken)
            ?? throw new BusinessRuleException("POS-BLEND-TYPE-NO-GRAM-UNIT", "وحدة القياس \"جرام\" (GM) غير موجودة في النظام.");

        var backingItemCode = await codeGenerator.ResolveCodeAsync("INVENTORY_ITEMS", $"BLD-{code}", cancellationToken);

        var item = new Item
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = backingItemCode,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            ItemType = ItemType.RawMaterial,
            BaseUnitOfMeasureId = gramUnit.Id,
            SaleMethod = SaleMethod.ByWeight,
            CostMethod = CostMethod.WeightedAverage,
            IsStocked = false,
            IsPurchasable = false,
            IsSellable = false,
            IsManufacturable = false,
            Status = ItemStatus.Active
        };

        db.Items.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        var blendType = new BlendType
        {
            CompanyId = currentCompanyContext.CompanyId,
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            PricePerGram = request.PricePerGram,
            ItemId = item.Id,
            IsActive = true
        };

        db.BlendTypes.Add(blendType);
        await db.SaveChangesAsync(cancellationToken);

        return blendType.Id;
    }
}
