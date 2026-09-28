using Habbak.ERP.Application.Accounting.Dimensions.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Dimensions.Queries.GetDimensionById;

public sealed record GetDimensionByIdQuery(long Id) : IRequest<DimensionDto>;

public sealed class GetDimensionByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDimensionByIdQuery, DimensionDto>
{
    public async Task<DimensionDto> Handle(GetDimensionByIdQuery request, CancellationToken cancellationToken)
    {
        var dimension = await db.CostCenterDimensions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CostCenterDimension), request.Id);

        return new DimensionDto
        {
            Id = dimension.Id,
            Code = dimension.Code,
            NameAr = dimension.NameAr,
            NameEn = dimension.NameEn,
            IsActive = dimension.IsActive,
            LinkedEntityType = dimension.LinkedEntityType
        };
    }
}
