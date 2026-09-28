using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.HR.OrgUnits.Dtos;
using Habbak.ERP.Domain.HR;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.HR.OrgUnits.Queries.GetOrgUnitById;

public sealed record GetOrgUnitByIdQuery(long Id) : IRequest<OrgUnitDto>;

public sealed class GetOrgUnitByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetOrgUnitByIdQuery, OrgUnitDto>
{
    public async Task<OrgUnitDto> Handle(GetOrgUnitByIdQuery request, CancellationToken cancellationToken)
    {
        return await db.OrgUnits
            .AsNoTracking()
            .Where(u => u.Id == request.Id)
            .Select(u => new OrgUnitDto { Id = u.Id, Code = u.Code, NameAr = u.NameAr, NameEn = u.NameEn, IsActive = u.IsActive, ParentId = u.ParentId, BranchId = u.BranchId, ManagerEmployeeId = u.ManagerEmployeeId, CostCenterDimensionValueId = u.CostCenterDimensionValueId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(OrgUnit), request.Id);
    }
}
