using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.RequestsForQuotation.Commands.SetRFQSupplierQuote;

/// <summary>
/// Records (creates or updates) one supplier's quoted price for one RFQ line — entered by the
/// buyer on the supplier's behalf, since suppliers aren't system users here. Upserts by
/// (RFQSupplierId, RFQLineId): first call for a pair creates the quote and flips that
/// RFQSupplier.Status to Responded (+ ResponseDate); a later call for the same pair just updates
/// the numbers. Also auto-advances RFQ.Status Sent → UnderReview on the RFQ's first response,
/// standing in for a separate "start review" step nothing else in the module needs.
/// </summary>
public sealed record SetRFQSupplierQuoteCommand : IRequest<long>
{
    public required long RFQId { get; init; }
    public required long RFQSupplierId { get; init; }
    public required long RFQLineId { get; init; }
    public required decimal UnitPrice { get; init; }
    public decimal? DiscountPercentage { get; init; }
    public int? DeliveryDays { get; init; }
    public required DateOnly ValidUntil { get; init; }
    public string? Notes { get; init; }
}

public sealed class SetRFQSupplierQuoteCommandValidator : AbstractValidator<SetRFQSupplierQuoteCommand>
{
    public SetRFQSupplierQuoteCommandValidator()
    {
        RuleFor(x => x.RFQSupplierId).GreaterThan(0);
        RuleFor(x => x.RFQLineId).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidUntil).NotEqual(default(DateOnly));
    }
}

public sealed class SetRFQSupplierQuoteCommandHandler(IApplicationDbContext db) : IRequestHandler<SetRFQSupplierQuoteCommand, long>
{
    public async Task<long> Handle(SetRFQSupplierQuoteCommand request, CancellationToken cancellationToken)
    {
        var rfq = await db.RequestsForQuotation.FirstOrDefaultAsync(r => r.Id == request.RFQId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Purchasing.RequestForQuotation), request.RFQId);

        if (rfq.Status is not (RFQStatus.Sent or RFQStatus.UnderReview))
        {
            throw new BusinessRuleException("PUR-RFQ-NOT-OPEN", "لا يمكن تسجيل عروض الأسعار إلا بعد إرسال الطلب وقبل الترسية.");
        }

        var rfqSupplier = await db.RFQSuppliers.FirstOrDefaultAsync(s => s.Id == request.RFQSupplierId && s.RFQId == request.RFQId, cancellationToken)
            ?? throw new NotFoundException(nameof(RFQSupplier), request.RFQSupplierId);

        if (!await db.RFQLines.AnyAsync(l => l.Id == request.RFQLineId && l.RFQId == request.RFQId, cancellationToken))
        {
            throw new NotFoundException(nameof(RFQLine), request.RFQLineId);
        }

        var quote = await db.RFQSupplierQuotes
            .FirstOrDefaultAsync(q => q.RFQSupplierId == request.RFQSupplierId && q.RFQLineId == request.RFQLineId, cancellationToken);

        if (quote is null)
        {
            quote = new RFQSupplierQuote { RFQSupplierId = request.RFQSupplierId, RFQLineId = request.RFQLineId };
            db.RFQSupplierQuotes.Add(quote);
        }

        quote.UnitPrice = request.UnitPrice;
        quote.DiscountPercentage = request.DiscountPercentage;
        quote.DeliveryDays = request.DeliveryDays;
        quote.ValidUntil = request.ValidUntil;
        quote.Notes = request.Notes;

        rfqSupplier.Status = RFQSupplierStatus.Responded;
        rfqSupplier.ResponseDate = DateOnly.FromDateTime(DateTime.UtcNow);

        if (rfq.Status == RFQStatus.Sent)
        {
            rfq.Status = RFQStatus.UnderReview;
        }

        await db.SaveChangesAsync(cancellationToken);

        return quote.Id;
    }
}
