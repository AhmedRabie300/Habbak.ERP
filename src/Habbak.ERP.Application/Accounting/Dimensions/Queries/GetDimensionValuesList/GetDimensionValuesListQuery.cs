using Habbak.ERP.Application.Accounting.Dimensions.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Dimensions.Queries.GetDimensionValuesList;

/// <summary>
/// For a Branch-linked dimension these rows are auto-synced mirrors of the Branches screen's own
/// records (CreateBranchCommand/UpdateBranchCommand) — never entered here directly — so this
/// query never needs to branch on LinkedEntityType itself.
/// </summary>
public sealed record GetDimensionValuesListQuery(long DimensionId) : IRequest<IReadOnlyList<DimensionValueDto>>;

public sealed class GetDimensionValuesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDimensionValuesListQuery, IReadOnlyList<DimensionValueDto>>
{
    public async Task<IReadOnlyList<DimensionValueDto>> Handle(GetDimensionValuesListQuery request, CancellationToken cancellationToken)
    {
        return await db.CostCenterDimensionValues
            .AsNoTracking()
            .Where(v => v.CostCenterDimensionId == request.DimensionId)
            .OrderBy(v => v.Code)
            .Select(v => new DimensionValueDto
            {
                Id = v.Id,
                CostCenterDimensionId = v.CostCenterDimensionId,
                Code = v.Code,
                NameAr = v.NameAr,
                NameEn = v.NameEn,
                ParentId = v.ParentId,
                Level = v.Level,
                IsActive = v.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
