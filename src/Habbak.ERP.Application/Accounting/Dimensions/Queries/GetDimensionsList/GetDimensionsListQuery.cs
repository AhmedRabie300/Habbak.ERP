using Habbak.ERP.Application.Accounting.Dimensions.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Dimensions.Queries.GetDimensionsList;

public sealed record GetDimensionsListQuery : IRequest<IReadOnlyList<DimensionDto>>;

public sealed class GetDimensionsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDimensionsListQuery, IReadOnlyList<DimensionDto>>
{
    public async Task<IReadOnlyList<DimensionDto>> Handle(GetDimensionsListQuery request, CancellationToken cancellationToken)
    {
        return await db.CostCenterDimensions
            .AsNoTracking()
            .OrderBy(d => d.Code)
            .Select(d => new DimensionDto { Id = d.Id, Code = d.Code, NameAr = d.NameAr, NameEn = d.NameEn, IsActive = d.IsActive, LinkedEntityType = d.LinkedEntityType })
            .ToListAsync(cancellationToken);
    }
}
