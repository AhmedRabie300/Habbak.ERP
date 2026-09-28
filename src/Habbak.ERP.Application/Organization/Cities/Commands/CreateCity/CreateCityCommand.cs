using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Cities.Commands.CreateCity;

/// <summary>Docs/Implementation/HR-Core-Plan.md §1.1, Batch B4. System-wide (City.cs doc) — no CompanyId, unique on Code alone.</summary>
public sealed record CreateCityCommand(string? Code, string NameAr, string NameEn, long CountryId) : IRequest<long>;

public sealed class CreateCityCommandValidator : AbstractValidator<CreateCityCommand>
{
    public CreateCityCommandValidator()
    {
        RuleFor(x => x.Code).MaximumLength(50);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CountryId).GreaterThan(0);
    }
}

public sealed class CreateCityCommandHandler(IApplicationDbContext db, ICodeGenerator codeGenerator)
    : IRequestHandler<CreateCityCommand, long>
{
    public async Task<long> Handle(CreateCityCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Countries.AnyAsync(c => c.Id == request.CountryId, cancellationToken))
        {
            throw new NotFoundException(nameof(Country), request.CountryId);
        }

        var code = await codeGenerator.ResolveCodeAsync("SETTINGS_CITIES", request.Code, cancellationToken);

        var codeExists = await db.Cities.AnyAsync(c => c.Code == code, cancellationToken);
        if (codeExists)
        {
            throw new BusinessRuleException("ORG-CITY-CODE-EXISTS", "توجد مدينة أخرى بنفس الكود بالفعل.");
        }

        var city = new City
        {
            Code = code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            CountryId = request.CountryId,
            IsActive = true
        };

        db.Cities.Add(city);
        await db.SaveChangesAsync(cancellationToken);

        return city.Id;
    }
}
