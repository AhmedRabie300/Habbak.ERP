using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.QRTickets.Commands.GenerateQRTicket;

public sealed record QRTicketItemInput(long ItemId, decimal Quantity, decimal UnitPrice);

/// <summary>قاعدة 18 / 00-Project-Overview.md قسم 14.3 — تُنشئ تذكرة ذاتية الاحتواء (الأصناف
/// وكمياتها وأسعارها مُسجَّلة هنا وقت الإصدار) وترجع IdempotencyKey بتاعتها، واللي في الاستخدام
/// الحقيقي هيتحوّل لـQR يحمل الـPayload كامل عشان جهاز القراءة يقدر يتحقق منه حتى لو Offline. مفيش
/// موديول "استشاري تصنيع" حقيقي لسه بيولّد التذاكر دي فعليًا — هذا الـCommand بيمثّل نقطة الإصدار
/// البديلة لحد ما الموديول ده يتبني.</summary>
public sealed record GenerateQRTicketCommand(IReadOnlyList<QRTicketItemInput> Items) : IRequest<GenerateQRTicketResult>;

public sealed record GenerateQRTicketResult(long Id, Guid IdempotencyKey);

public sealed class GenerateQRTicketCommandValidator : AbstractValidator<GenerateQRTicketCommand>
{
    public GenerateQRTicketCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("لا يمكن إصدار تذكرة بدون أصناف.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ItemId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class GenerateQRTicketCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompany)
    : IRequestHandler<GenerateQRTicketCommand, GenerateQRTicketResult>
{
    public async Task<GenerateQRTicketResult> Handle(GenerateQRTicketCommand request, CancellationToken cancellationToken)
    {
        foreach (var item in request.Items)
        {
            if (!await db.Items.AnyAsync(i => i.Id == item.ItemId, cancellationToken))
            {
                throw new NotFoundException("Item", item.ItemId);
            }
        }

        var ticket = new QRTicket
        {
            CompanyId = currentCompany.CompanyId,
            IdempotencyKey = Guid.NewGuid(),
            Status = QRTicketStatus.Active
        };

        var lineNumber = 1;
        foreach (var item in request.Items)
        {
            ticket.Lines.Add(new QRTicketLine
            {
                LineNumber = lineNumber++,
                ItemId = item.ItemId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            });
        }

        db.QRTickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        return new GenerateQRTicketResult(ticket.Id, ticket.IdempotencyKey);
    }
}
