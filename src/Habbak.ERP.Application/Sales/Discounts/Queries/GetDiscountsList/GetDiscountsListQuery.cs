using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.Discounts.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.Discounts.Queries.GetDiscountsList;

public sealed record GetDiscountsListQuery : IRequest<IReadOnlyList<DiscountListItemDto>>;

public sealed class GetDiscountsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDiscountsListQuery, IReadOnlyList<DiscountListItemDto>>
{
    public async Task<IReadOnlyList<DiscountListItemDto>> Handle(GetDiscountsListQuery request, CancellationToken cancellationToken)
    {
        return await db.Discounts
            .AsNoTracking()
            .OrderBy(d => d.ApplicationPriority)
            .Select(d => new DiscountListItemDto
            {
                Id = d.Id,
                Code = d.Code,
                NameAr = d.NameAr,
                NameEn = d.NameEn,
                DiscountType = d.DiscountType.ToString(),
                Value = d.Value,
                ApplicationPriority = d.ApplicationPriority,
                IsStackable = d.IsStackable,
                IsHappyHour = d.IsHappyHour,
                IsActive = d.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
