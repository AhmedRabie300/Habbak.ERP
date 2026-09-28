using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.InsuranceOffices.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.InsuranceOffices.Queries.GetInsuranceOfficesList;

/// <summary>Not a paginated GetList screen: the insurance-office catalog is always small (Currency precedent).</summary>
public sealed record GetInsuranceOfficesListQuery : IRequest<IReadOnlyList<InsuranceOfficeDto>>;

public sealed class GetInsuranceOfficesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetInsuranceOfficesListQuery, IReadOnlyList<InsuranceOfficeDto>>
{
    public async Task<IReadOnlyList<InsuranceOfficeDto>> Handle(GetInsuranceOfficesListQuery request, CancellationToken cancellationToken)
    {
        return await db.InsuranceOffices
            .AsNoTracking()
            .OrderBy(o => o.Code)
            .Select(o => new InsuranceOfficeDto { Id = o.Id, Code = o.Code, NameAr = o.NameAr, NameEn = o.NameEn, IsActive = o.IsActive, OfficialCode = o.OfficialCode, Address = o.Address })
            .ToListAsync(cancellationToken);
    }
}
