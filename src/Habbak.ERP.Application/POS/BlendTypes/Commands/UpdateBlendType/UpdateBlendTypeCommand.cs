using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.BlendTypes.Commands.UpdateBlendType;

/// <summary>الاسم بيتحدَّث على الصنف الخلفي كمان عشان يفضل مطابق (نفس الاسم يظهر وقت مسح تذكرة
/// خلطة قديمة على فاتورة). السعر بيتحدَّث على BlendType بس — الأسعار التاريخية اللي اتسجّلت بالفعل
/// على تذاكر/فواتير قديمة (QRTicketLine.UnitPrice) ثابتة زي أي مستند مُرحَّل، ما بتتأثرش بتغيير
/// سعر الخلطة الحالي.</summary>
public sealed record UpdateBlendTypeCommand(long Id, string NameAr, string NameEn, decimal PricePerGram, bool IsActive) : IRequest;

public sealed class UpdateBlendTypeCommandValidator : AbstractValidator<UpdateBlendTypeCommand>
{
    public UpdateBlendTypeCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PricePerGram).GreaterThan(0);
    }
}

public sealed class UpdateBlendTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateBlendTypeCommand>
{
    public async Task Handle(UpdateBlendTypeCommand request, CancellationToken cancellationToken)
    {
        var blendType = await db.BlendTypes.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BlendType), request.Id);

        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == blendType.ItemId, cancellationToken);

        blendType.NameAr = request.NameAr;
        blendType.NameEn = request.NameEn;
        blendType.PricePerGram = request.PricePerGram;
        blendType.IsActive = request.IsActive;

        if (item is not null)
        {
            item.NameAr = request.NameAr;
            item.NameEn = request.NameEn;
            item.Status = request.IsActive ? Domain.Inventory.ItemStatus.Active : Domain.Inventory.ItemStatus.Inactive;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
