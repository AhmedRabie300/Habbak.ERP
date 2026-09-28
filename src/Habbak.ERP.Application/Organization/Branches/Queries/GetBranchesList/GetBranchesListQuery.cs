using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Organization.Branches.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Organization.Branches.Queries.GetBranchesList;

/// <summary>Not a paginated GetList screen: a company's branch count is always small.</summary>
public sealed record GetBranchesListQuery : IRequest<IReadOnlyList<BranchDto>>;

public sealed class GetBranchesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBranchesListQuery, IReadOnlyList<BranchDto>>
{
    public async Task<IReadOnlyList<BranchDto>> Handle(GetBranchesListQuery request, CancellationToken cancellationToken)
    {
        return await db.Branches
            .AsNoTracking()
            .OrderBy(b => b.Code)
            .Select(b => new BranchDto { Id = b.Id, Code = b.Code, NameAr = b.NameAr, NameEn = b.NameEn, IsActive = b.IsActive })
            .ToListAsync(cancellationToken);
    }
}
