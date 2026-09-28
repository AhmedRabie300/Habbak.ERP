using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using MediatR;

namespace Habbak.ERP.Application.Organization.Currencies.Commands.UpdateCurrency;

/// <summary>IsDefault true makes it the default (the previous one loses the flag); false is refused on the default itself.</summary>
public sealed record UpdateCurrencyCommand(long Id, string NameAr, string NameEn, bool IsActive, bool? IsDefault = null) : IRequest;

public sealed class UpdateCurrencyCommandValidator : AbstractValidator<UpdateCurrencyCommand>
{
    public UpdateCurrencyCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
    }
}

public sealed class UpdateCurrencyCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCurrencyCommand>
{
    public async Task Handle(UpdateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var currency = await db.Currencies.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Currency), request.Id);

        DefaultCurrencyRules.EnsureNotRemovingDefault(currency, keepDefault: request.IsDefault ?? true, keepActive: request.IsActive);

        currency.NameAr = request.NameAr;
        currency.NameEn = request.NameEn;
        currency.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        if (request.IsDefault == true && !currency.IsDefault)
        {
            await DefaultCurrencyRules.MakeDefaultAsync(db, currency, cancellationToken);
        }
    }
}
