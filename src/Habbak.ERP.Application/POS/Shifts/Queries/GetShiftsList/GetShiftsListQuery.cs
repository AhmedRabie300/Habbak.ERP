using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Shifts.Dtos;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Shifts.Queries.GetShiftsList;

public sealed record GetShiftsListQuery(long? POSTerminalId, ShiftStatus? Status) : IRequest<IReadOnlyList<ShiftListItemDto>>;

public sealed class GetShiftsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetShiftsListQuery, IReadOnlyList<ShiftListItemDto>>
{
    public async Task<IReadOnlyList<ShiftListItemDto>> Handle(GetShiftsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.Shifts.AsNoTracking().Include(s => s.POSTerminal).AsQueryable();

        if (request.POSTerminalId is { } terminalId)
        {
            query = query.Where(s => s.POSTerminalId == terminalId);
        }

        if (request.Status is { } status)
        {
            query = query.Where(s => s.Status == status);
        }

        return await query
            .OrderByDescending(s => s.OpenedAtUtc)
            .Select(s => new ShiftListItemDto
            {
                Id = s.Id,
                POSTerminalId = s.POSTerminalId,
                POSTerminalNameAr = s.POSTerminal!.NameAr,
                POSTerminalNameEn = s.POSTerminal!.NameEn,
                CashierUserId = s.CashierUserId,
                Status = s.Status.ToString(),
                OpenedAtUtc = s.OpenedAtUtc,
                ClosedAtUtc = s.ClosedAtUtc,
                OpeningCashAmount = s.OpeningCashAmount,
                ActualClosingCashAmount = s.ActualClosingCashAmount,
                DifferenceAmount = s.DifferenceAmount
            })
            .ToListAsync(cancellationToken);
    }
}
