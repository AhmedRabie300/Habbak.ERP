using Habbak.ERP.Application.Accounting.Custody.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Custody.Queries.GetCustodyRegisterById;

public sealed record GetCustodyRegisterByIdQuery(long Id) : IRequest<CustodyRegisterDetailDto>;

public sealed class GetCustodyRegisterByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetCustodyRegisterByIdQuery, CustodyRegisterDetailDto>
{
    public async Task<CustodyRegisterDetailDto> Handle(GetCustodyRegisterByIdQuery request, CancellationToken cancellationToken)
    {
        var custody = await db.CustodyRegisters
            .AsNoTracking()
            .Include(c => c.Settlements)
                .ThenInclude(s => s.Lines)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CustodyRegister), request.Id);

        return new CustodyRegisterDetailDto
        {
            Id = custody.Id,
            EmployeeId = custody.EmployeeId,
            BranchId = custody.BranchId,
            Amount = custody.Amount,
            IssueDate = custody.IssueDate,
            Status = custody.Status.ToString(),
            JournalEntryId = custody.JournalEntryId,
            Settlements = custody.Settlements.Select(s => new CustodySettlementDto
            {
                Id = s.Id,
                SettlementDate = s.SettlementDate,
                RemainingAmount = s.RemainingAmount,
                JournalEntryId = s.JournalEntryId,
                Lines = s.Lines.Select(l => new CustodySettlementLineDto
                {
                    AccountId = l.AccountId,
                    Amount = l.Amount,
                    Description = l.Description
                }).ToList()
            }).ToList()
        };
    }
}
