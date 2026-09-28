using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.Discounts.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.Discounts.Queries.GetDiscountById;

public sealed record GetDiscountByIdQuery(long Id) : IRequest<DiscountDetailDto>;

public sealed class GetDiscountByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetDiscountByIdQuery, DiscountDetailDto>
{
    public async Task<DiscountDetailDto> Handle(GetDiscountByIdQuery request, CancellationToken cancellationToken)
    {
        var discount = await db.Discounts.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Discount), request.Id);

        return new DiscountDetailDto
        {
            Id = discount.Id,
            Code = discount.Code,
            NameAr = discount.NameAr,
            NameEn = discount.NameEn,
            DiscountType = discount.DiscountType.ToString(),
            Value = discount.Value,
            ApplicationPriority = discount.ApplicationPriority,
            IsStackable = discount.IsStackable,
            MinInvoiceAmount = discount.MinInvoiceAmount,
            MinQuantity = discount.MinQuantity,
            IsHappyHour = discount.IsHappyHour,
            HappyHourFromTime = discount.HappyHourFromTime,
            HappyHourToTime = discount.HappyHourToTime,
            EffectiveFromDate = discount.EffectiveFromDate,
            EffectiveToDate = discount.EffectiveToDate,
            IsActive = discount.IsActive
        };
    }
}
