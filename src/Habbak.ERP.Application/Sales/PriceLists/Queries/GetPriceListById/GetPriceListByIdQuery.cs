using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Sales.PriceLists.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.PriceLists.Queries.GetPriceListById;

public sealed record GetPriceListByIdQuery(long Id) : IRequest<PriceListDetailDto>;

public sealed class GetPriceListByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPriceListByIdQuery, PriceListDetailDto>
{
    public async Task<PriceListDetailDto> Handle(GetPriceListByIdQuery request, CancellationToken cancellationToken)
    {
        var priceList = await db.PriceLists
            .AsNoTracking()
            .Include(p => p.Branches)
            .Include(p => p.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PriceList), request.Id);

        return new PriceListDetailDto
        {
            Id = priceList.Id,
            Code = priceList.Code,
            NameAr = priceList.NameAr,
            NameEn = priceList.NameEn,
            EffectiveFromDate = priceList.EffectiveFromDate,
            EffectiveToDate = priceList.EffectiveToDate,
            IsActive = priceList.IsActive,
            BranchIds = priceList.Branches.Select(b => b.BranchId).ToList(),
            Lines = priceList.Lines.Select(l => new PriceListLineDto
            {
                ItemId = l.ItemId,
                ItemCode = l.Item!.Code,
                ItemNameAr = l.Item!.NameAr,
                DineInPrice = l.DineInPrice,
                TakeawayPrice = l.TakeawayPrice,
                DeliveryPrice = l.DeliveryPrice
            }).ToList()
        };
    }
}
