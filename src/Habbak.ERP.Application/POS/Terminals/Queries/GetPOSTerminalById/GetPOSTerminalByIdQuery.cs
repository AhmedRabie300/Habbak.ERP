using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Terminals.Dtos;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Terminals.Queries.GetPOSTerminalById;

public sealed record GetPOSTerminalByIdQuery(long Id) : IRequest<POSTerminalDto>;

public sealed class GetPOSTerminalByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetPOSTerminalByIdQuery, POSTerminalDto>
{
    public async Task<POSTerminalDto> Handle(GetPOSTerminalByIdQuery request, CancellationToken cancellationToken)
    {
        var terminal = await db.POSTerminals.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(POSTerminal), request.Id);

        return new POSTerminalDto
        {
            Id = terminal.Id,
            Code = terminal.Code,
            NameAr = terminal.NameAr,
            NameEn = terminal.NameEn,
            BranchId = terminal.BranchId!.Value,
            DefaultWarehouseId = terminal.DefaultWarehouseId,
            IsActive = terminal.IsActive
        };
    }
}
