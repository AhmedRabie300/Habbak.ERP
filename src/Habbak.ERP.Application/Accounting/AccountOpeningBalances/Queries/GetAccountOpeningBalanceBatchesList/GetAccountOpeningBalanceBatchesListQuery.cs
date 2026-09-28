using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Accounting.AccountOpeningBalances.Dtos;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.AccountOpeningBalances.Queries.GetAccountOpeningBalanceBatchesList;

public sealed class GetAccountOpeningBalanceBatchesListQuery : ListQuery, IRequest<PagedResult<AccountOpeningBalanceBatchListItemDto>>;

public sealed class GetAccountOpeningBalanceBatchesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountOpeningBalanceBatchesListQuery, PagedResult<AccountOpeningBalanceBatchListItemDto>>
{
    public async Task<PagedResult<AccountOpeningBalanceBatchListItemDto>> Handle(GetAccountOpeningBalanceBatchesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.AccountOpeningBalanceBatches.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<AccountOpeningBalanceStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            DateOnly? dateTerm = DateOnly.TryParse(term, out var parsedDate) ? parsedDate : null;

            query = query.Where(b =>
                b.BatchNumber.Contains(term) ||
                matchingStatuses.Contains(b.Status) ||
                (dateTerm != null && b.TransactionDate == dateTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "transactionDate" => descending ? query.OrderByDescending(b => b.TransactionDate) : query.OrderBy(b => b.TransactionDate),
            "status" => descending ? query.OrderByDescending(b => b.Status) : query.OrderBy(b => b.Status),
            _ => descending ? query.OrderByDescending(b => b.BatchNumber) : query.OrderBy(b => b.BatchNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new AccountOpeningBalanceBatchListItemDto
            {
                Id = b.Id,
                BatchNumber = b.BatchNumber,
                TransactionDate = b.TransactionDate,
                LineCount = b.Lines.Count,
                TotalAmount = b.Lines.Sum(l => l.Amount),
                Status = b.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AccountOpeningBalanceBatchListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
