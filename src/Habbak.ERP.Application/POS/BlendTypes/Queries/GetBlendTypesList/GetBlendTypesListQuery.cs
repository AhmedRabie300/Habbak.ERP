using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.BlendTypes.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.BlendTypes.Queries.GetBlendTypesList;

/// <summary>قائمة صغيرة زي POS Categories — مفيش داعي لصفحات.</summary>
public sealed record GetBlendTypesListQuery : IRequest<IReadOnlyList<BlendTypeDto>>;

public sealed class GetBlendTypesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBlendTypesListQuery, IReadOnlyList<BlendTypeDto>>
{
    public async Task<IReadOnlyList<BlendTypeDto>> Handle(GetBlendTypesListQuery request, CancellationToken cancellationToken)
    {
        return await db.BlendTypes
            .AsNoTracking()
            .OrderBy(b => b.NameAr)
            .Select(b => new BlendTypeDto
            {
                Id = b.Id,
                Code = b.Code,
                NameAr = b.NameAr,
                NameEn = b.NameEn,
                PricePerGram = b.PricePerGram,
                IsActive = b.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
