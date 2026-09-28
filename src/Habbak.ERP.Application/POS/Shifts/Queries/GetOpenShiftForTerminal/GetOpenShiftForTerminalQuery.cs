using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Shifts.Dtos;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Shifts.Queries.GetOpenShiftForTerminal;

/// <summary>يستخدمها كاشير نقطة البيع لمعرفة هل فيه وردية مفتوحة بالفعل على الجهاز قبل محاولة فتح
/// وردية جديدة — يرجع null لو مفيش.</summary>
public sealed record GetOpenShiftForTerminalQuery(long POSTerminalId) : IRequest<ShiftDetailDto?>;

public sealed class GetOpenShiftForTerminalQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetOpenShiftForTerminalQuery, ShiftDetailDto?>
{
    public async Task<ShiftDetailDto?> Handle(GetOpenShiftForTerminalQuery request, CancellationToken cancellationToken)
    {
        var shift = await db.Shifts
            .AsNoTracking()
            .Include(s => s.POSTerminal)
            .Include(s => s.DenominationCounts)
            .FirstOrDefaultAsync(s => s.POSTerminalId == request.POSTerminalId && s.Status == ShiftStatus.Open, cancellationToken);

        if (shift is null)
        {
            return null;
        }

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
