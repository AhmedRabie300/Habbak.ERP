using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Purchasing.PurchaseInvoices.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseInvoices.Queries.GetPurchaseInvoicesList;

public sealed class GetPurchaseInvoicesListQuery : ListQuery, IRequest<PagedResult<PurchaseInvoiceListItemDto>>;

public sealed class GetPurchaseInvoicesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseInvoicesListQuery, PagedResult<PurchaseInvoiceListItemDto>>
{
    public async Task<PagedResult<PurchaseInvoiceListItemDto>> Handle(GetPurchaseInvoicesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.PurchaseInvoices.AsNoTracking().Include(i => i.Supplier).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingStatuses = Enum.GetValues<PurchaseInvoiceStatus>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            DateOnly? dateTerm = DateOnly.TryParse(term, out var parsedDate) ? parsedDate : null;

            query = query.Where(i =>
                i.InvoiceNumber.Contains(term) ||
                (i.SupplierInvoiceNumber != null && i.SupplierInvoiceNumber.Contains(term)) ||
                i.Supplier!.Code.Contains(term) ||
                i.Supplier!.NameAr.Contains(term) ||
                matchingStatuses.Contains(i.Status) ||
                (dateTerm != null && i.InvoiceDate == dateTerm));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "invoiceDate" => descending ? query.OrderByDescending(i => i.InvoiceDate) : query.OrderBy(i => i.InvoiceDate),
            "dueDate" => descending ? query.OrderByDescending(i => i.DueDate) : query.OrderBy(i => i.DueDate),
            "status" => descending ? query.OrderByDescending(i => i.Status) : query.OrderBy(i => i.Status),
            _ => descending ? query.OrderByDescending(i => i.InvoiceNumber) : query.OrderBy(i => i.InvoiceNumber)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new PurchaseInvoiceListItemDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                SupplierId = i.SupplierId,
                SupplierCode = i.Supplier!.Code,
                SupplierNameAr = i.Supplier!.NameAr,
                TotalAmount = i.TotalAmount,
                CurrencyCode = i.CurrencyCode,
                LineCount = i.Lines.Count,
                Status = i.Status.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseInvoiceListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
