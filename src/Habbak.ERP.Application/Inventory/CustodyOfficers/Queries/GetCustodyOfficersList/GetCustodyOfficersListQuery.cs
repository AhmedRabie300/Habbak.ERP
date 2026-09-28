using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.CustodyOfficers.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.CustodyOfficers.Queries.GetCustodyOfficersList;

/// <summary>Not a paginated GetList screen: a company's custody-officer count is always small.</summary>
public sealed record GetCustodyOfficersListQuery : IRequest<IReadOnlyList<CustodyOfficerDto>>;

public sealed class GetCustodyOfficersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCustodyOfficersListQuery, IReadOnlyList<CustodyOfficerDto>>
{
    public async Task<IReadOnlyList<CustodyOfficerDto>> Handle(GetCustodyOfficersListQuery request, CancellationToken cancellationToken)
    {
        return await db.CustodyOfficers
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CustodyOfficerDto
            {
                Id = c.Id,
                Code = c.Code,
                NameAr = c.NameAr,
                NameEn = c.NameEn,
                BranchId = c.BranchId,
                IsActive = c.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
