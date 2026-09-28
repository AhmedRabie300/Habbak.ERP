using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Organization.Countries.Dtos;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Countries.Queries.GetCountryById;

public sealed record GetCountryByIdQuery(long Id) : IRequest<CountryDto>;

public sealed class GetCountryByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCountryByIdQuery, CountryDto>
{
    public async Task<CountryDto> Handle(GetCountryByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.Countries
            .AsNoTracking()
            .Where(c => c.Id == request.Id)
            .Select(c => new CountryDto { Id = c.Id, Code = c.Code, NameAr = c.NameAr, NameEn = c.NameEn, IsActive = c.IsActive, IsoCode = c.IsoCode })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Country), request.Id);
    }
}
