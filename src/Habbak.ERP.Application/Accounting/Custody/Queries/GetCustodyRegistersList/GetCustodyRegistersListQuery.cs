using Habbak.ERP.Application.Accounting.Custody.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Custody.Queries.GetCustodyRegistersList;

public sealed record GetCustodyRegistersListQuery : IRequest<IReadOnlyList<CustodyRegisterListItemDto>>;

public sealed class GetCustodyRegistersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCustodyRegistersListQuery, IReadOnlyList<CustodyRegisterListItemDto>>
{
    public async Task<IReadOnlyList<CustodyRegisterListItemDto>> Handle(GetCustodyRegistersListQuery request, CancellationToken cancellationToken)
    {
        return await db.CustodyRegisters
            .AsNoTracking()
            .OrderByDescending(c => c.IssueDate)
            .Select(c => new CustodyRegisterListItemDto
            {
                Id = c.Id,
                EmployeeId = c.EmployeeId,
                Amount = c.Amount,
                IssueDate = c.IssueDate,
                Status = c.Status.ToString()
            })
            .ToListAsync(cancellationToken);
    }
}
