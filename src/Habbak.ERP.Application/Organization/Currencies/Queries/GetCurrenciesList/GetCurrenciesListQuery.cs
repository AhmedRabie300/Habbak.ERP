using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Organization.Currencies.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Currencies.Queries.GetCurrenciesList;

/// <summary>Not a paginated GetList screen: a system's currency catalog is always small.</summary>
public sealed record GetCurrenciesListQuery : IRequest<IReadOnlyList<CurrencyDto>>;

public sealed class GetCurrenciesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCurrenciesListQuery, IReadOnlyList<CurrencyDto>>
{
    public async Task<IReadOnlyList<CurrencyDto>> Handle(GetCurrenciesListQuery request, CancellationToken cancellationToken)
    {
        return await db.Currencies
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CurrencyDto { Id = c.Id, Code = c.Code, NameAr = c.NameAr, NameEn = c.NameEn, IsActive = c.IsActive, IsDefault = c.IsDefault })
            .ToListAsync(cancellationToken);
    }
}
