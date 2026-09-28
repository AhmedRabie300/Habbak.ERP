using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Organization.Cities.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Cities.Queries.GetCitiesList;

/// <summary>Not a paginated GetList screen: the city catalog is always small (Currency precedent).</summary>
public sealed record GetCitiesListQuery : IRequest<IReadOnlyList<CityDto>>;

public sealed class GetCitiesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCitiesListQuery, IReadOnlyList<CityDto>>
{
    public async Task<IReadOnlyList<CityDto>> Handle(GetCitiesListQuery request, CancellationToken cancellationToken)
    {
        return await db.Cities
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CityDto { Id = c.Id, Code = c.Code, NameAr = c.NameAr, NameEn = c.NameEn, IsActive = c.IsActive, CountryId = c.CountryId })
            .ToListAsync(cancellationToken);
    }
}
