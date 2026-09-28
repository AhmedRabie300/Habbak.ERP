using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.ShiftAssignments.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.ShiftAssignments.Queries.GetShiftAssignmentsList;

public sealed record GetShiftAssignmentsListQuery(long? POSTerminalId) : IRequest<IReadOnlyList<ShiftAssignmentDto>>;

public sealed class GetShiftAssignmentsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetShiftAssignmentsListQuery, IReadOnlyList<ShiftAssignmentDto>>
{
    public async Task<IReadOnlyList<ShiftAssignmentDto>> Handle(GetShiftAssignmentsListQuery request, CancellationToken cancellationToken)
    {
        var query = db.ShiftAssignments.AsNoTracking().Include(a => a.POSTerminal).AsQueryable();

        if (request.POSTerminalId is { } terminalId)
        {
            query = query.Where(a => a.POSTerminalId == terminalId);
        }

        return await query
            .OrderByDescending(a => a.AssignedDate)
            .Select(a => new ShiftAssignmentDto
            {
                Id = a.Id,
                POSTerminalId = a.POSTerminalId,
                POSTerminalNameAr = a.POSTerminal!.NameAr,
                POSTerminalNameEn = a.POSTerminal!.NameEn,
                UserId = a.UserId,
                AssignedDate = a.AssignedDate
            })
            .ToListAsync(cancellationToken);
    }
}
