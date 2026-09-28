using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Companies.Commands.UpdateCompany;

public sealed record UpdateCompanyCommand(
    long Id,
    string NameAr,
    string NameEn,
    string? CommercialRegister,
    string? TaxCard,
    long BaseCurrencyId,
    bool IsActive) : IRequest;

public sealed class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CommercialRegister).MaximumLength(100);
        RuleFor(x => x.TaxCard).MaximumLength(100);
        RuleFor(x => x.BaseCurrencyId).GreaterThan(0);
    }
}

public sealed class UpdateCompanyCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCompanyCommand>
{
    public async Task Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        var company = await db.Companies.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Company), request.Id);

        var currencyExists = await db.Currencies.AnyAsync(c => c.Id == request.BaseCurrencyId, cancellationToken);
        if (!currencyExists)
        {
            throw new NotFoundException(nameof(Currency), request.BaseCurrencyId);
        }

        company.NameAr = request.NameAr;
        company.NameEn = request.NameEn;
        company.CommercialRegister = request.CommercialRegister;
        company.TaxCard = request.TaxCard;
        company.BaseCurrencyId = request.BaseCurrencyId;
        company.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
