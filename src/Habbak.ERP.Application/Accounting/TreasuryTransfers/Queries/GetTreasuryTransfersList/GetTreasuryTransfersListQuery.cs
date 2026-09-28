using Habbak.ERP.Application.Accounting.TreasuryTransfers.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.TreasuryTransfers.Queries.GetTreasuryTransfersList;

/// <summary>GetList (00-Frontend-Specs.md, section 6).</summary>
public sealed class GetTreasuryTransfersListQuery : ListQuery, IRequest<PagedResult<TreasuryTransferListItemDto>>;

public sealed class GetTreasuryTransfersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTreasuryTransfersListQuery, PagedResult<TreasuryTransferListItemDto>>
{
    public async Task<PagedResult<TreasuryTransferListItemDto>> Handle(
        GetTreasuryTransfersListQuery request, CancellationToken cancellationToken)
    {
        var query = db.TreasuryTransfers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<TreasuryTransferStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            decimal? amountTerm = decimal.TryParse(term.Replace(",", ""), out var parsedAmount) ? parsedAmount : null;
            DateOnly? dateTerm = DateOnly.TryParse(term, out var parsedDate) ? parsedDate : null;

            query = query.Where(t =>
                matchingStatuses.Contains(t.Status) ||
                (amountTerm != null && t.Amount == amountTerm) ||
                (dateTerm != null && t.TransferDate == dateTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "amount" => descending ? query.OrderByDescending(t => t.Amount) : query.OrderBy(t => t.Amount),
            "status" => descending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            _ => descending ? query.OrderByDescending(t => t.TransferDate) : query.OrderBy(t => t.TransferDate)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TreasuryTransferListItemDto
            {
                Id = t.Id,
                FromTreasuryAccountId = t.FromTreasuryAccountId,
                ToTreasuryAccountId = t.ToTreasuryAccountId,
                Amount = t.Amount,
                TransferDate = t.TransferDate,
                Status = t.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<TreasuryTransferListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
