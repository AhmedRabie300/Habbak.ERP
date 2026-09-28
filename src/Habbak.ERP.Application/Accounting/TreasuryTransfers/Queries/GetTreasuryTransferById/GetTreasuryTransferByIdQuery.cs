using Habbak.ERP.Application.Accounting.TreasuryTransfers.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.TreasuryTransfers.Queries.GetTreasuryTransferById;

public sealed record GetTreasuryTransferByIdQuery(long Id) : IRequest<TreasuryTransferDetailDto>;

public sealed class GetTreasuryTransferByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTreasuryTransferByIdQuery, TreasuryTransferDetailDto>
{
    public async Task<TreasuryTransferDetailDto> Handle(GetTreasuryTransferByIdQuery request, CancellationToken cancellationToken)
    {
        var transfer = await db.TreasuryTransfers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(TreasuryTransfer), request.Id);

        return new TreasuryTransferDetailDto
        {
            Id = transfer.Id,
            BranchId = transfer.BranchId,
            FromTreasuryAccountId = transfer.FromTreasuryAccountId,
            ToTreasuryAccountId = transfer.ToTreasuryAccountId,
            Amount = transfer.Amount,
            TransferDate = transfer.TransferDate,
            Status = transfer.Status.ToString(),
            Notes = transfer.Notes,
            JournalEntryId = transfer.JournalEntryId,
            RowVersion = Convert.ToBase64String(transfer.RowVersion)
        };
    }
}
