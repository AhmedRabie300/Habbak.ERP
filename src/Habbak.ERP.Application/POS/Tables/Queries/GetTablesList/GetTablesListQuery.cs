using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Tables.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Tables.Queries.GetTablesList;

public sealed record GetTablesListQuery : IRequest<IReadOnlyList<TableDto>>;

public sealed class GetTablesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTablesListQuery, IReadOnlyList<TableDto>>
{
    public async Task<IReadOnlyList<TableDto>> Handle(GetTablesListQuery request, CancellationToken cancellationToken)
    {
        return await db.Tables
            .AsNoTracking()
            .OrderBy(t => t.Code)
            .Select(t => new TableDto
            {
                Id = t.Id,
                Code = t.Code,
                NameAr = t.NameAr,
                NameEn = t.NameEn,
                BranchId = t.BranchId!.Value,
                Status = t.Status.ToString(),
                IsActive = t.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
