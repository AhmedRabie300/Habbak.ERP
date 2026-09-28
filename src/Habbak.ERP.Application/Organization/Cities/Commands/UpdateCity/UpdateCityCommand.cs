using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Cities.Commands.UpdateCity;

public sealed record UpdateCityCommand(long Id, string NameAr, string NameEn, long CountryId, bool IsActive) : IRequest;

public sealed class UpdateCityCommandValidator : AbstractValidator<UpdateCityCommand>
{
    public UpdateCityCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CountryId).GreaterThan(0);
    }
}

public sealed class UpdateCityCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCityCommand>
{
    public async Task Handle(UpdateCityCommand request, CancellationToken cancellationToken)
    {
        var city = await db.Cities.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(City), request.Id);

        if (!await db.Countries.AnyAsync(c => c.Id == request.CountryId, cancellationToken))
        {
            throw new NotFoundException(nameof(Country), request.CountryId);
        }

        city.NameAr = request.NameAr;
        city.NameEn = request.NameEn;
        city.CountryId = request.CountryId;
        city.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
