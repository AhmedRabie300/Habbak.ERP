using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Accounting.AccountOpeningBalances.Dtos;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.AccountOpeningBalances.Queries.GetAccountOpeningBalanceBatchById;

public sealed record GetAccountOpeningBalanceBatchByIdQuery(long Id) : IRequest<AccountOpeningBalanceBatchDetailDto>;

public sealed class GetAccountOpeningBalanceBatchByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountOpeningBalanceBatchByIdQuery, AccountOpeningBalanceBatchDetailDto>
{
    public async Task<AccountOpeningBalanceBatchDetailDto> Handle(GetAccountOpeningBalanceBatchByIdQuery request, CancellationToken cancellationToken)
    {
        var batch = await db.AccountOpeningBalanceBatches
            .AsNoTracking()
            .Include(b => b.Lines).ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(AccountOpeningBalanceBatch), request.Id);

        return new AccountOpeningBalanceBatchDetailDto
        {
            Id = batch.Id,
            BatchNumber = batch.BatchNumber,
            TransactionDate = batch.TransactionDate,
            Status = batch.Status.ToString(),
            JournalEntryId = batch.JournalEntryId,
            Notes = batch.Notes,
            RowVersion = Convert.ToBase64String(batch.RowVersion),
            Lines = batch.Lines
                .OrderBy(l => l.LineNumber)
                .Select(l => new AccountOpeningBalanceLineDto
                {
                    Id = l.Id,
                    AccountId = l.AccountId,
                    AccountCode = l.Account!.Code,
                    AccountNameAr = l.Account!.NameAr,
                    AccountNature = l.Account!.Nature.ToString(),
                    Amount = l.Amount,
                    Notes = l.Notes
                })
                .ToList()
        };
    }
}
