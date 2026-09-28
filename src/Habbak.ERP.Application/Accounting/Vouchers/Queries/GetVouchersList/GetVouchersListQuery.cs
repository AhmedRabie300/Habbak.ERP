using Habbak.ERP.Application.Accounting.Vouchers.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Vouchers.Queries.GetVouchersList;

/// <summary>
/// GetList (00-Frontend-Specs.md, section 6). VoucherType is required so the Receipt Vouchers
/// screen and the Payment Vouchers screen (section 5, screens 5-6) each see only their own rows
/// despite sharing one underlying table.
/// </summary>
public sealed class GetVouchersListQuery : ListQuery, IRequest<PagedResult<VoucherListItemDto>>
{
    public required VoucherType VoucherType { get; init; }

    /// <summary>Optional — narrows further to one counterparty type, e.g. Supplier for the
    /// Purchasing module's own "سداد الموردين" screen (#9) layered on this same list.</summary>
    public CounterpartyType? CounterpartyType { get; init; }
}

public sealed class GetVouchersListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetVouchersListQuery, PagedResult<VoucherListItemDto>>
{
    public async Task<PagedResult<VoucherListItemDto>> Handle(
        GetVouchersListQuery request, CancellationToken cancellationToken)
    {
        var query = db.Vouchers.AsNoTracking().Where(v => v.VoucherType == request.VoucherType);

        if (request.CounterpartyType is { } counterpartyType)
        {
            query = query.Where(v => v.CounterpartyType == counterpartyType);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            // Grid also shows CounterpartyType/Status (enums) and Amount/Date columns — match those
            // too, not just VoucherNumber. Enum names matched by substring (e.g. "post" -> Posted);
            // amount/date matched by exact value since the grid formats them (commas, ISO date) rather
            // than storing display text.
            var matchingCounterpartyTypes = Enum.GetValues<CounterpartyType>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var matchingStatuses = Enum.GetValues<VoucherStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            decimal? amountTerm = decimal.TryParse(term.Replace(",", ""), out var parsedAmount) ? parsedAmount : null;
            DateOnly? dateTerm = DateOnly.TryParse(term, out var parsedDate) ? parsedDate : null;

            query = query.Where(v =>
                v.VoucherNumber.Contains(term) ||
                (v.Description != null && v.Description.Contains(term)) ||
                matchingCounterpartyTypes.Contains(v.CounterpartyType) ||
                matchingStatuses.Contains(v.Status) ||
                (amountTerm != null && v.Amount == amountTerm) ||
                (dateTerm != null && v.VoucherDate == dateTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "voucherDate" => descending ? query.OrderByDescending(v => v.VoucherDate) : query.OrderBy(v => v.VoucherDate),
            "amount" => descending ? query.OrderByDescending(v => v.Amount) : query.OrderBy(v => v.Amount),
            "status" => descending ? query.OrderByDescending(v => v.Status) : query.OrderBy(v => v.Status),
            _ => descending ? query.OrderByDescending(v => v.VoucherNumber) : query.OrderBy(v => v.VoucherNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VoucherListItemDto
            {
                Id = v.Id,
                VoucherType = v.VoucherType.ToString(),
                VoucherNumber = v.VoucherNumber,
                VoucherDate = v.VoucherDate,
                TreasuryAccountId = v.TreasuryAccountId,
                Description = v.Description,
                CounterpartyType = v.CounterpartyType.ToString(),
                Amount = v.Amount,
                Status = v.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<VoucherListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
