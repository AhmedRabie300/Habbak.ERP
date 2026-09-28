using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.DrawerMovements.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.DrawerMovements.Queries.GetDrawerMovementsList;

public sealed record GetDrawerMovementsListQuery(long? ShiftId) : IRequest<IReadOnlyList<DrawerMovementDto>>;

public sealed class GetDrawerMovementsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDrawerMovementsListQuery, IReadOnlyList<DrawerMovementDto>>
{
    public async Task<IReadOnlyList<DrawerMovementDto>> Handle(GetDrawerMovementsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.DrawerMovements.AsNoTracking().AsQueryable();

        if (request.ShiftId is { } shiftId)
        {
            query = query.Where(m => m.ShiftId == shiftId);
        }

        return await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new DrawerMovementDto
            {
                Id = m.Id,
                ShiftId = m.ShiftId,
                MovementType = m.MovementType.ToString(),
                Amount = m.Amount,
                Reason = m.Reason,
                Status = m.Status.ToString(),
                ApprovedByUserId = m.ApprovedByUserId,
                ApprovedAtUtc = m.ApprovedAtUtc,
                CreatedAtUtc = m.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }
}
