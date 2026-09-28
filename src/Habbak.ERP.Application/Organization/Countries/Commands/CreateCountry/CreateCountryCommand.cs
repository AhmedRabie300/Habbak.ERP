using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Countries.Commands.CreateCountry;

/// <summary>Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4. System-wide (Country.cs doc) — no CompanyId, unique on Code alone, same as Currency.</summary>
public sealed record CreateCountryCommand(string? Code, string NameAr, string NameEn, string? IsoCode) : IRequest<long>;

public sealed class CreateCountryCommandValidator : AbstractValidator<CreateCountryCommand>
{
    public CreateCountryCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IsoCode).MaximumLength(10);
    }
}

public sealed class CreateCountryCommandHandler(IApplicationDbContext db, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateCountryCommand, long>
{
    public async Task<long> Handle(CreateCountryCommand request, CancellationToken cancellationToken)
    {
        var code = await codeGenerator.ResolveCodeAsync("SETTINGS_COUNTRIES", request.Code, cancellationToken);

        var codeExists = await db.Countries.AnyAsync(c => c.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("ORG-COUNTRY-CODE-EXISTS", "توجد دولة أخرى بنفس الكود بالفعل.");
        }

        var country = new Country
        {
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            IsoCode = request.IsoCode,
            IsActive = true
        };

        db.Countries.Add(country);
        await db.SaveChangesAsync(cancellationToken);

        return country.Id;
    }
}
