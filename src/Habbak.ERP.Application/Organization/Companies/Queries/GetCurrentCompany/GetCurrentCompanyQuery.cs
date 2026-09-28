using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Organization.Companies.Dtos;
using Habbak.ERP.Domain.Organization;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Companies.Queries.GetCurrentCompany;

/// <summary>Resolves the logged-in session's own company record — used to show a read-only
/// "Company" field on screens like Branches (00-System-Wide-Corrections... session decision:
/// Branches stays filtered to the current company; the form only clarifies which one).</summary>
public sealed record GetCurrentCompanyQuery : IRequest<CompanyDto>;

public sealed class GetCurrentCompanyQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<GetCurrentCompanyQuery, CompanyDto>
{
    public async Task<CompanyDto> Handle(GetCurrentCompanyQuery request, CancellationToken cancellationToken)
    {
        return await db.Companies
            .AsNoTracking()
            .Where(c => c.Id == currentCompanyContext.CompanyId)
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
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(Company), currentCompanyContext.CompanyId);
    }
}
