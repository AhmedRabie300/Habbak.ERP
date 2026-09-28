using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Sales.SalesInvoices.Dtos;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Sales.SalesInvoices.Queries.GetSalesInvoicesList;

public sealed class GetSalesInvoicesListQuery : ListQuery, IRequest<PagedResult<SalesInvoiceListItemDto>>;

public sealed class GetSalesInvoicesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSalesInvoicesListQuery, PagedResult<SalesInvoiceListItemDto>>
{
    public async Task<PagedResult<SalesInvoiceListItemDto>> Handle(GetSalesInvoicesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.SalesInvoices.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<SalesInvoiceStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            query = query.Where(i =>
                i.InvoiceNumber.Contains(term) ||
                i.Customer!.NameAr.Contains(term) ||
                matchingStatuses.Contains(i.Status));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "invoiceDate" => descending ? query.OrderByDescending(i => i.InvoiceDate) : query.OrderBy(i => i.InvoiceDate),
            "status" => descending ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            _ => descending ? query.OrderByDescending(i => i.InvoiceNumber) : query.OrderBy(i => i.InvoiceNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new SalesInvoiceListItemDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                BranchId = i.BranchId,
                CustomerId = i.CustomerId,
                CustomerNameAr = i.Customer!.NameAr,
                PaymentType = i.PaymentType.ToString(),
                TotalAmount = i.TotalAmount,
                AmountPaid = i.AmountPaid,
                Status = i.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<SalesInvoiceListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
