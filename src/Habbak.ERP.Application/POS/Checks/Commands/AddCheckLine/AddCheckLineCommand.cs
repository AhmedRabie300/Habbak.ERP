using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Checks.Commands.AddCheckLine;

/// <summary>UnitPrice اختياري — لو اتسابت null بيتحل تلقائيًا من قائمة الأسعار السارية على الفرع
/// حسب OrderType الحالي للشيك (قاعدة 10)؛ لو اتبعت صراحة من الكاشير، بتتسجَّل كسعر مُعدَّل يدويًا
/// لو مختلفة عن السعر المحلول تلقائيًا (قرار جلسة الاستشارة رقم 7 — صلاحية تعديل السعر اليدوي، لسه
/// بدون إنفاذ فعلي لعدم وجود نظام صلاحيات).</summary>
public sealed record AddCheckLineCommand(long CheckId, long ItemId, decimal Quantity, decimal? UnitPrice, decimal DiscountAmount, string? Note) : IRequest<long>;

public sealed class AddCheckLineCommandValidator : AbstractValidator<AddCheckLineCommand>
{
    public AddCheckLineCommandValidator()
    {
        RuleFor(x => x.CheckId).GreaterThan(0);
        RuleFor(x => x.ItemId).GreaterThan(0);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).When(x => x.UnitPrice.HasValue);
    }
}

public sealed class AddCheckLineCommandHandler(IApplicationDbContext db) : IRequestHandler<AddCheckLineCommand, long>
{
    public async Task<long> Handle(AddCheckLineCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.CheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.CheckId);

        if (check.Status != CheckStatus.Open && check.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن إضافة بند لشيك منتهٍ.");
        }

        if (!await db.Items.AnyAsync(i => i.Id == request.ItemId, cancellationToken))
        {
            throw new NotFoundException("Item", request.ItemId);
        }

        var resolvedPrice = await PriceResolver.ResolveUnitPriceAsync(db, check.BranchId!.Value, request.ItemId, check.OrderType, cancellationToken);

        decimal unitPrice;
        bool isManuallyOverridden;

        if (request.UnitPrice is { } explicitPrice)
        {
            unitPrice = explicitPrice;
            isManuallyOverridden = resolvedPrice is null || resolvedPrice.Value != explicitPrice;
        }
        else if (resolvedPrice is { } price)
        {
            unitPrice = price;
            isManuallyOverridden = false;
        }
        else
        {
            throw new BusinessRuleException("POS-CHECK-ITEM-NO-PRICE", "لا يوجد سعر محدد لهذا الصنف — أدخل السعر يدويًا.");
        }

        var nextLineNumber = check.Lines.Count == 0 ? 1 : check.Lines.Max(l => l.LineNumber) + 1;

        var line = new CheckLine
        {
            CheckId = check.Id,
            LineNumber = nextLineNumber,
            ItemId = request.ItemId,
            Quantity = request.Quantity,
            UnitPrice = unitPrice,
            DiscountAmount = request.DiscountAmount,
            IsPriceManuallyOverridden = isManuallyOverridden,
            Note = request.Note
        };

        db.CheckLines.Add(line);
        await db.SaveChangesAsync(cancellationToken);

        return line.Id;
    }
}
