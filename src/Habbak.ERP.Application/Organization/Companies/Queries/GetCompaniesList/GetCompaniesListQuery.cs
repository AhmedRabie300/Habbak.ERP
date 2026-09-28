using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Organization.Companies.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Companies.Queries.GetCompaniesList;

/// <summary>Not a paginated GetList screen: the tenant list is always small.</summary>
public sealed record GetCompaniesListQuery : IRequest<IReadOnlyList<CompanyDto>>;

public sealed class GetCompaniesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCompaniesListQuery, IReadOnlyList<CompanyDto>>
{
    public async Task<IReadOnlyList<CompanyDto>> Handle(GetCompaniesListQuery request, CancellationToken cancellationToken)
    {
        return await db.Companies
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CompanyDto
            {
                Id = c.Id,
                Code = c.Code,
                NameAr = c.NameAr,
                NameEn = c.NameEn,
                CommercialRegister = c.CommercialRegister,
                TaxCard = c.TaxCard,
                BaseCurrencyId = c.BaseCurrencyId,
                BaseCurrencyCode = c.BaseCurrency.Code,
                IsActive = c.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
