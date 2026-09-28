using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Organization;
using MediatR;

namespace Habbak.ERP.Application.Organization.Countries.Commands.UpdateCountry;

public sealed record UpdateCountryCommand(long Id, string NameAr, string NameEn, string? IsoCode, bool IsActive) : IRequest;

public sealed class UpdateCountryCommandValidator : AbstractValidator<UpdateCountryCommand>
{
    public UpdateCountryCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(200);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IsoCode).MaximumLength(10);
    }
}

public sealed class UpdateCountryCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateCountryCommand>
{
    public async Task Handle(UpdateCountryCommand request, CancellationToken cancellationToken)
    {
        var country = await db.Countries.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(Country), request.Id);

        country.NameAr = request.NameAr;
        country.NameEn = request.NameEn;
        country.IsoCode = request.IsoCode;
        country.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
    }
}
