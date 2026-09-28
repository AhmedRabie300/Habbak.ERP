using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Terminals.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Terminals.Queries.GetPOSTerminalsList;

/// <summary>Not a paginated GetList screen — a company's POS-terminal count is always small.</summary>
public sealed record GetPOSTerminalsListQuery : IRequest<IReadOnlyList<POSTerminalDto>>;

public sealed class GetPOSTerminalsListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPOSTerminalsListQuery, IReadOnlyList<POSTerminalDto>>
{
    public async Task<IReadOnlyList<POSTerminalDto>> Handle(GetPOSTerminalsListQuery request, CancellationToken cancellationToken)
    {
        return await db.POSTerminals
            .AsNoTracking()
            .OrderBy(t => t.Code)
            .Select(t => new POSTerminalDto
            {
                Id = t.Id,
                Code = t.Code,
                NameAr = t.NameAr,
                NameEn = t.NameEn,
                BranchId = t.BranchId!.Value,
                DefaultWarehouseId = t.DefaultWarehouseId,
                IsActive = t.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
