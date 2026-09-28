using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Shifts.Dtos;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Shifts.Queries.GetShiftById;

public sealed record GetShiftByIdQuery(long Id) : IRequest<ShiftDetailDto>;

public sealed class GetShiftByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetShiftByIdQuery, ShiftDetailDto>
{
    public async Task<ShiftDetailDto> Handle(GetShiftByIdQuery request, CancellationToken cancellationToken)
    {
        var shift = await db.Shifts
            .AsNoTracking()
            .Include(s => s.POSTerminal)
            .Include(s => s.DenominationCounts)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Shift), request.Id);

        return new ShiftDetailDto
        {
            Id = shift.Id,
            RowVersion = Convert.ToBase64String(shift.RowVersion),
            POSTerminalId = shift.POSTerminalId,
            POSTerminalNameAr = shift.POSTerminal!.NameAr,
            POSTerminalNameEn = shift.POSTerminal!.NameEn,
            CashierUserId = shift.CashierUserId,
            Status = shift.Status.ToString(),
            OpenedAtUtc = shift.OpenedAtUtc,
            ClosedAtUtc = shift.ClosedAtUtc,
            OpeningCashAmount = shift.OpeningCashAmount,
            ExpectedClosingCashAmount = shift.ExpectedClosingCashAmount,
            ActualClosingCashAmount = shift.ActualClosingCashAmount,
            DifferenceAmount = shift.DifferenceAmount,
            ClosedByUserId = shift.ClosedByUserId,
            DenominationCounts = shift.DenominationCounts
                .OrderBy(c => c.CountType).ThenByDescending(c => c.DenominationValue)
                .Select(c => new ShiftDenominationCountDto
                {
                    CountType = c.CountType.ToString(),
                    DenominationValue = c.DenominationValue,
                    Count = c.Count
                })
                .ToList()
        };
    }
}
