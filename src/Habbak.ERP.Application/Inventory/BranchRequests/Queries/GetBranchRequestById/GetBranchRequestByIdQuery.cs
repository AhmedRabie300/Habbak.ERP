using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.BranchRequests.Dtos;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Inventory.BranchRequests.Queries.GetBranchRequestById;

public sealed record GetBranchRequestByIdQuery(long Id) : IRequest<BranchRequestDetailDto>;

public sealed class GetBranchRequestByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetBranchRequestByIdQuery, BranchRequestDetailDto>
{
    public async Task<BranchRequestDetailDto> Handle(GetBranchRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var branchRequest = await db.BranchRequests
            .AsNoTracking()
            .Include(r => r.Lines).ThenInclude(l => l.Item)
            .Include(r => r.Lines).ThenInclude(l => l.Unit)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(BranchRequest), request.Id);

        var itemIds = branchRequest.Lines.Select(l => l.ItemId).ToList();
        var limitsByItem = await db.BranchItemLimits
            .Where(l => l.BranchId == branchRequest.BranchId && itemIds.Contains(l.ItemId))
            .ToDictionaryAsync(l => l.ItemId, cancellationToken);

        return new BranchRequestDetailDto
        {
            Id = branchRequest.Id,
            BranchId = branchRequest.BranchId,
            RequestNumber = branchRequest.RequestNumber,
            RequestDate = branchRequest.RequestDate,
            RequestedByUserId = branchRequest.RequestedByUserId,
            Status = branchRequest.Status.ToString(),
            RowVersion = Convert.ToBase64String(branchRequest.RowVersion),
            Lines = branchRequest.Lines
                .Select(l =>
                {
                    limitsByItem.TryGetValue(l.ItemId, out var limit);
                    return new BranchRequestLineDto
                    {
                        Id = l.Id,
                        ItemId = l.ItemId,
                        ItemCode = l.Item.Code,
                        ItemNameAr = l.Item.NameAr,
                        RequestedQuantity = l.RequestedQuantity,
                        ApprovedQuantity = l.ApprovedQuantity,
                        UnitId = l.UnitId,
                        UnitCode = l.Unit?.Code,
                        UnitNameAr = l.Unit?.NameAr,
                        UnitFactor = l.UnitFactor,
                        MinRequestQuantity = limit?.MinRequestQuantity,
                        MaxRequestQuantity = limit?.MaxRequestQuantity
                    };
                })
                .ToList()
        };
    }
}
