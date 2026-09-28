using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.SalesQuotes.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesQuotes.Commands.CreateSalesQuote;

/// <summary>Creates a sales quote as Draft (screen #5).</summary>
public sealed record CreateSalesQuoteCommand : IRequest<long>
{
    public long? BranchId { get; init; }
    public required long CustomerId { get; init; }
    public required DateOnly QuoteDate { get; init; }
    public required DateOnly ValidUntil { get; init; }
    public required IReadOnlyList<SalesQuoteLineInput> Lines { get; init; }
}

public sealed class CreateSalesQuoteCommandValidator : AbstractValidator<CreateSalesQuoteCommand>
{
    public CreateSalesQuoteCommandValidator()
    {
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

public sealed class CreateSalesQuoteCommandHandler(
    IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateSalesQuoteCommand, long>
{
    public async Task<long> Handle(CreateSalesQuoteCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        var quoteNumber = await codeGenerator.ResolveCodeAsync("SALES_QUOTE", null, cancellationToken);
        var (lines, subtotal) = SalesQuoteLineBuilder.Build(request.Lines);

        var quote = new SalesQuote
        {
            CompanyId = currentCompanyContext.CompanyId,
            BranchId = request.BranchId,
            CustomerId = request.CustomerId,
            QuoteNumber = quoteNumber,
            QuoteDate = request.QuoteDate,
            ValidUntil = request.ValidUntil,
            Status = SalesQuoteStatus.Draft,
            Subtotal = subtotal
        };

        foreach (var line in lines)
        {
            quote.Lines.Add(line);
        }

        db.SalesQuotes.Add(quote);
        await db.SaveChangesAsync(cancellationToken);

        return quote.Id;
    }
}
