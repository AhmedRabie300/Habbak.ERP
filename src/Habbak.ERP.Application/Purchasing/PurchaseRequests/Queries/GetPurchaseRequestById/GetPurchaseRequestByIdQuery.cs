using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseRequests.Queries.GetPurchaseRequestById;

public sealed record GetPurchaseRequestByIdQuery(long Id) : IRequest<PurchaseRequestDetailDto>;

public sealed class GetPurchaseRequestByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseRequestByIdQuery, PurchaseRequestDetailDto>
{
    public async Task<PurchaseRequestDetailDto> Handle(GetPurchaseRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var purchaseRequest = await db.PurchaseRequests
            .AsNoTracking()
            .Include(r => r.Lines).ThenInclude(l => l.Item)
            .Include(r => r.Lines).ThenInclude(l => l.Unit)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseRequest), request.Id);

        return new PurchaseRequestDetailDto
        {
            Id = purchaseRequest.Id,
            RequestNumber = purchaseRequest.RequestNumber,
            RequestDate = purchaseRequest.RequestDate,
            BranchId = purchaseRequest.BranchId,
            RequestedByUserId = purchaseRequest.RequestedByUserId,
            Priority = purchaseRequest.Priority.ToString(),
            Reason = purchaseRequest.Reason,
            Status = purchaseRequest.Status.ToString(),
            Notes = purchaseRequest.Notes,
            RowVersion = Convert.ToBase64String(purchaseRequest.RowVersion),
            Lines = purchaseRequest.Lines
                .Select(l => new PurchaseRequestLineDto
                {
                    Id = l.Id,
                    ItemId = l.ItemId,
                    ItemCode = l.Item!.Code,
                    ItemNameAr = l.Item!.NameAr,
                    Quantity = l.Quantity,
                    UnitId = l.UnitId,
                    UnitCode = l.Unit!.Code,
                    UnitFactor = l.UnitFactor,
                    BaseQuantity = l.BaseQuantity,
                    Notes = l.Notes
                })
                .ToList()
        };
    }
}
