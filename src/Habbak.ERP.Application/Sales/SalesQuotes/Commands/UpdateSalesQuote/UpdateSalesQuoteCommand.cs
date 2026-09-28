using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesQuotes.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesQuotes.Commands.UpdateSalesQuote;

/// <summary>Edits a Draft sales quote — only a Draft can change.</summary>
public sealed record UpdateSalesQuoteCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required DateOnly QuoteDate { get; init; }
    public required DateOnly ValidUntil { get; init; }
    public required IReadOnlyList<SalesQuoteLineInput> Lines { get; init; }
}

public sealed class UpdateSalesQuoteCommandValidator : AbstractValidator<UpdateSalesQuoteCommand>
{
    public UpdateSalesQuoteCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.QuoteDate).NotEqual(default(DateOnly));
        RuleFor(x => x.ValidUntil).NotEqual(default(DateOnly));
        RuleFor(x => x.Lines).NotEmpty().WithMessage("عرض السعر يحتاج بند واحد على الأقل.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class UpdateSalesQuoteCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateSalesQuoteCommand>
{
    public async Task Handle(UpdateSalesQuoteCommand request, CancellationToken cancellationToken)
    {
        var quote = await db.SalesQuotes
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesQuote), request.Id);

        if (quote.Status != SalesQuoteStatus.Draft)
        {
            throw new BusinessRuleException("SALES-QUOTE-NOT-DRAFT", "لا يمكن تعديل عرض السعر إلا وهو في حالة مسودة.");
        }

        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        db.Entry(quote).Property(nameof(SalesQuote.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        quote.BranchId = request.BranchId;
        quote.CustomerId = request.CustomerId;
        quote.QuoteDate = request.QuoteDate;
        quote.ValidUntil = request.ValidUntil;

        db.SalesQuoteLines.RemoveRange(quote.Lines);
        quote.Lines.Clear();
        await db.SaveChangesAsync(cancellationToken);

        var (lines, subtotal) = SalesQuoteLineBuilder.Build(request.Lines);
        foreach (var line in lines)
        {
            quote.Lines.Add(line);
        }

        quote.Subtotal = subtotal;

        await db.SaveChangesAsync(cancellationToken);
    }
}
