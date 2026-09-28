using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.QRTickets.Commands.RedeemQRTicket;

/// <summary>قاعدة 18 — مسح تذكرة QR ذاتية الاحتواء وإضافة بنودها لشيك نشط حاليًا. لو التذكرة
/// اتصرفت (Redeemed) بالفعل، يرجّع نجاح بدون أي تأثير إضافي (منعًا لتكرار الإضافة لو اتمسحت بالغلط
/// أكتر من مرة) بدل رفض الطلب — نفس مبدأ IdempotencyBehavior لكن مطبَّق يدويًا هنا لأن مفتاح
/// المطابقة (IdempotencyKey) خاص بكيان QRTicket نفسه مش بالـCommand ده.</summary>
public sealed record RedeemQRTicketCommand(long CheckId, Guid IdempotencyKey) : IRequest;

public sealed class RedeemQRTicketCommandValidator : AbstractValidator<RedeemQRTicketCommand>
{
    public RedeemQRTicketCommandValidator()
    {
        RuleFor(x => x.CheckId).GreaterThan(0);
        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}

public sealed class RedeemQRTicketCommandHandler(IApplicationDbContext db) : IRequestHandler<RedeemQRTicketCommand>
{
    public async Task Handle(RedeemQRTicketCommand request, CancellationToken cancellationToken)
    {
        var ticket = await db.QRTickets.Include(t => t.Lines).FirstOrDefaultAsync(t => t.IdempotencyKey == request.IdempotencyKey, cancellationToken)
            ?? throw new NotFoundException(nameof(QRTicket), request.IdempotencyKey);

        if (ticket.Status == QRTicketStatus.Redeemed)
        {
            return;
        }

        var check = await db.Checks.Include(c => c.Lines).FirstOrDefaultAsync(c => c.Id == request.CheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.CheckId);

        if (check.Status != CheckStatus.Open && check.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن إضافة تذكرة QR لشيك منتهٍ.");
        }

        var nextLineNumber = check.Lines.Count == 0 ? 1 : check.Lines.Max(l => l.LineNumber) + 1;

        foreach (var ticketLine in ticket.Lines.OrderBy(l => l.LineNumber))
        {
            check.Lines.Add(new CheckLine
            {
                LineNumber = nextLineNumber++,
                ItemId = ticketLine.ItemId,
                Quantity = ticketLine.Quantity,
                UnitPrice = ticketLine.UnitPrice
            });
        }

        ticket.Status = QRTicketStatus.Redeemed;
        ticket.RedeemedByCheckId = check.Id;
        ticket.RedeemedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
