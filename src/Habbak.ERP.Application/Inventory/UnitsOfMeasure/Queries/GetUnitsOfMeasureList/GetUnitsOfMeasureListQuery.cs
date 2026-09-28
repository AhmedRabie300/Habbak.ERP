using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.UnitsOfMeasure.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.UnitsOfMeasure.Queries.GetUnitsOfMeasureList;

/// <summary>Not a paginated GetList screen: a company's unit-of-measure count is always small.</summary>
public sealed record GetUnitsOfMeasureListQuery : IRequest<IReadOnlyList<UnitOfMeasureDto>>;

public sealed class GetUnitsOfMeasureListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetUnitsOfMeasureListQuery, IReadOnlyList<UnitOfMeasureDto>>
{
    public async Task<IReadOnlyList<UnitOfMeasureDto>> Handle(GetUnitsOfMeasureListQuery request, CancellationToken cancellationToken)
    {
        return await db.UnitsOfMeasure
            .AsNoTracking()
            .OrderBy(u => u.Code)
            .Select(u => new UnitOfMeasureDto { Id = u.Id, Code = u.Code, NameAr = u.NameAr, NameEn = u.NameEn, Category = u.Category.ToString(), IsActive = u.IsActive })
            .ToListAsync(cancellationToken);
    }
}
