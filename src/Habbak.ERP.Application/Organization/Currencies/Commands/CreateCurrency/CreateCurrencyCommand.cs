using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Currencies.Commands.CreateCurrency;

/// <summary>Creates a currency — system-wide, not company-scoped (Currency.cs doc).</summary>
/// <summary>The first currency, or one created with IsDefault, becomes the default.</summary>
public sealed record CreateCurrencyCommand(string Code, string NameAr, string NameEn, bool IsDefault = false) : IRequest<long>;

public sealed class CreateCurrencyCommandValidator : AbstractValidator<CreateCurrencyCommand>
{
    public CreateCurrencyCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(3);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class CreateCurrencyCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateCurrencyCommand, long>
{
    public async Task<long> Handle(CreateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.ToUpperInvariant();

        var codeExists = await db.Currencies.AnyAsync(c => c.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("ORG-CURRENCY-CODE-EXISTS", "توجد عملة أخرى بنفس الكود بالفعل.");
        }

        var currency = new Currency
        {
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            IsActive = true
        };

        db.Currencies.Add(currency);
        await db.SaveChangesAsync(cancellationToken);

        if (request.IsDefault || !await db.Currencies.AnyAsync(c => c.IsDefault, cancellationToken))
        {
            await DefaultCurrencyRules.MakeDefaultAsync(db, currency, cancellationToken);
        }

        return currency.Id;
    }
}
