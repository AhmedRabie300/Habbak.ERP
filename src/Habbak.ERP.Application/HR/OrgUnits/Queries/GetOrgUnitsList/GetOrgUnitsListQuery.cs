using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.OrgUnits.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.OrgUnits.Queries.GetOrgUnitsList;

/// <summary>Not a paginated GetList screen: a company's org-structure catalog is always small.</summary>
public sealed record GetOrgUnitsListQuery : IRequest<IReadOnlyList<OrgUnitDto>>;

public sealed class GetOrgUnitsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetOrgUnitsListQuery, IReadOnlyList<OrgUnitDto>>
{
    public async Task<IReadOnlyList<OrgUnitDto>> Handle(GetOrgUnitsListQuery request, CancellationToken cancellationToken)
    {
        return await db.OrgUnits
            .AsNoTracking()
            .OrderBy(u => u.Code)
            .Select(u => new OrgUnitDto { Id = u.Id, Code = u.Code, NameAr = u.NameAr, NameEn = u.NameEn, IsActive = u.IsActive, ParentId = u.ParentId, BranchId = u.BranchId, ManagerEmployeeId = u.ManagerEmployeeId, CostCenterDimensionValueId = u.CostCenterDimensionValueId })
            .ToListAsync(cancellationToken);
    }
}
