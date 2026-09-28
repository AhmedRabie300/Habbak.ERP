using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Tables.Dtos;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Tables.Queries.GetTableById;

public sealed record GetTableByIdQuery(long Id) : IRequest<TableDto>;

public sealed class GetTableByIdQueryHandler(IApplicationDbContext db) : IRequestHandler<GetTableByIdQuery, TableDto>
{
    public async Task<TableDto> Handle(GetTableByIdQuery request, CancellationToken cancellationToken)
    {
        var table = await db.Tables.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Table), request.Id);

        return new TableDto
        {
            Id = table.Id,
            Code = table.Code,
            NameAr = table.NameAr,
            NameEn = table.NameEn,
            BranchId = table.BranchId!.Value,
            Status = table.Status.ToString(),
            IsActive = table.IsActive
        };
    }
}
