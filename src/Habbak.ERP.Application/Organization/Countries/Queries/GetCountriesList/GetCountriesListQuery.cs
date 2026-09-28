using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Organization.Countries.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Countries.Queries.GetCountriesList;

/// <summary>Not a paginated GetList screen: the country catalog is always small (Currency precedent).</summary>
public sealed record GetCountriesListQuery : IRequest<IReadOnlyList<CountryDto>>;

public sealed class GetCountriesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCountriesListQuery, IReadOnlyList<CountryDto>>
{
    public async Task<IReadOnlyList<CountryDto>> Handle(GetCountriesListQuery request, CancellationToken cancellationToken)
    {
        return await db.Countries
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CountryDto { Id = c.Id, Code = c.Code, NameAr = c.NameAr, NameEn = c.NameEn, IsActive = c.IsActive, IsoCode = c.IsoCode })
            .ToListAsync(cancellationToken);
    }
}
