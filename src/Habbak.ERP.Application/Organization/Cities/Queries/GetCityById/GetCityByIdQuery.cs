using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Organization.Cities.Dtos;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Cities.Queries.GetCityById;

public sealed record GetCityByIdQuery(long Id) : IRequest<CityDto>;

public sealed class GetCityByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetCityByIdQuery, CityDto>
{
    public async Task<CityDto> Handle(GetCityByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.Cities
            .AsNoTracking()
            .Where(c => c.Id == request.Id)
            .Select(c => new CityDto { Id = c.Id, Code = c.Code, NameAr = c.NameAr, NameEn = c.NameEn, IsActive = c.IsActive, CountryId = c.CountryId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(City), request.Id);
    }
}
