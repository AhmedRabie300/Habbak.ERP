using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Settings;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Companies.Commands.CreateCompany;

public sealed record CreateCompanyCommand(
    string Code,
    string NameAr,
    string NameEn,
    string? CommercialRegister,
    string? TaxCard,
    long BaseCurrencyId) : IRequest<long>;

public sealed class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CommercialRegister).MaximumLength(100);
        RuleFor(x => x.TaxCard).MaximumLength(100);
        RuleFor(x => x.BaseCurrencyId).GreaterThan(0);
    }
}

public sealed class CreateCompanyCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentContext) : IRequestHandler<CreateCompanyCommand, long>
{
    public async Task<long> Handle(CreateCompanyCommand request, CancellationToken cancellationToken)
    {
        var codeExists = await db.Companies.AnyAsync(c => c.Code == request.Code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("ORG-COMPANY-CODE-EXISTS", "توجد شركة أخرى بنفس الكود بالفعل.");
        }

        var currencyExists = await db.Currencies.AnyAsync(c => c.Id == request.BaseCurrencyId, cancellationToken);
        if (!currencyExists)
        {
            throw new NotFoundException(nameof(Currency), request.BaseCurrencyId);
        }

        var company = new Company
        {
            Code = request.Code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            CommercialRegister = request.CommercialRegister,
            TaxCard = request.TaxCard,
            BaseCurrencyId = request.BaseCurrencyId,
            IsActive = true
        };

        // The company and its security defaults (system roles, SystemSettings, super-admin access)
        // land together or not at all; the defaults need the company id, hence two saves.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        db.Companies.Add(company);
        await db.SaveChangesAsync(cancellationToken);

        await CompanySecurityDefaults.AddAsync(db, company.Id, DateTime.UtcNow, currentContext.UserId, cancellationToken);
        FixedAssets.FixedAssetDefaults.Add(db, company.Id);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return company.Id;
    }
}
