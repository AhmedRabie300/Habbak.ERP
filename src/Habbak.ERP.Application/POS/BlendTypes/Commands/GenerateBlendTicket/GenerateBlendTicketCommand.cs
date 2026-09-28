using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.BlendTypes.Commands.GenerateBlendTicket;

public sealed record BlendCompositionInput(long BlendTypeId, decimal WeightGrams);

/// <summary>مراجعة 2026-09-13، بند 2.1 — شاشة استشاري التصنيع: العميل يختار أكتر من نوع بن ووزن كل
/// نوع بالجرام، والنظام بيصدر تذكرة QR ذاتية الاحتواء (00-Project-Overview.md قسم 14.3) بنفس آلية
/// GenerateQRTicketCommand بالظبط (نفس QRTicket/QRTicketLine، مش مسار كود منفصل) — الفرق الوحيد إن
/// المُدخَلات هنا BlendTypeId+وزن بدل ItemId+سعر مباشر، وبيتحوّلوا هنا لسطر تذكرة عادي: Quantity =
/// الوزن بالجرام، UnitPrice = BlendType.PricePerGram وقت الإصدار (مش وقت الإنشاء الأصلي للخلطة —
/// لو السعر اتغيّر بعدين، التذاكر القديمة تفضل بأسعارها الأصلية زي أي مستند مُرحَّل).</summary>
public sealed record GenerateBlendTicketCommand(IReadOnlyList<BlendCompositionInput> Compositions) : IRequest<GenerateBlendTicketResult>;

public sealed record GenerateBlendTicketResult(long Id, Guid IdempotencyKey, decimal TotalWeightGrams, decimal TotalPrice);

public sealed class GenerateBlendTicketCommandValidator : AbstractValidator<GenerateBlendTicketCommand>
{
    public GenerateBlendTicketCommandValidator()
    {
        RuleFor(x => x.Compositions).NotEmpty().WithMessage("لا يمكن إصدار تذكرة خلطة بدون أنواع بن.");

        RuleForEach(x => x.Compositions).ChildRules(item =>
        {
            item.RuleFor(i => i.BlendTypeId).GreaterThan(0);
            item.RuleFor(i => i.WeightGrams).GreaterThan(0);
        });
    }
}

public sealed class GenerateBlendTicketCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompany)
    : IRequestHandler<GenerateBlendTicketCommand, GenerateBlendTicketResult>
{
    public async Task<GenerateBlendTicketResult> Handle(GenerateBlendTicketCommand request, CancellationToken cancellationToken)
    {
        var blendTypeIds = request.Compositions.Select(c => c.BlendTypeId).Distinct().ToList();
        var blendTypes = await db.BlendTypes
            .Where(b => blendTypeIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, cancellationToken);

        foreach (var blendTypeId in blendTypeIds)
        {
            if (!blendTypes.ContainsKey(blendTypeId))
            {
                throw new NotFoundException(nameof(BlendType), blendTypeId);
            }
        }

        var ticket = new QRTicket
        {
            CompanyId = currentCompany.CompanyId,
            IdempotencyKey = Guid.NewGuid(),
            Status = QRTicketStatus.Active
        };

        var lineNumber = 1;
        decimal totalWeight = 0;
        decimal totalPrice = 0;

        foreach (var composition in request.Compositions)
        {
            var blendType = blendTypes[composition.BlendTypeId];

            ticket.Lines.Add(new QRTicketLine
            {
                LineNumber = lineNumber++,
                ItemId = blendType.ItemId,
                Quantity = composition.WeightGrams,
                UnitPrice = blendType.PricePerGram
            });

            totalWeight += composition.WeightGrams;
            totalPrice += composition.WeightGrams * blendType.PricePerGram;
        }

        db.QRTickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        return new GenerateBlendTicketResult(ticket.Id, ticket.IdempotencyKey, totalWeight, Math.Round(totalPrice, 2));
    }
}
