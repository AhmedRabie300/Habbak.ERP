using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Purchasing.PurchaseExpenses.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseExpenses.Queries.GetPurchaseExpensesList;

public sealed class GetPurchaseExpensesListQuery : ListQuery, IRequest<PagedResult<PurchaseExpenseListItemDto>>
{
    /// <summary>Optional — narrows to one invoice's own breakdown.</summary>
    public long? PurchaseInvoiceId { get; init; }
}

public sealed class GetPurchaseExpensesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseExpensesListQuery, PagedResult<PurchaseExpenseListItemDto>>
{
    public async Task<PagedResult<PurchaseExpenseListItemDto>> Handle(GetPurchaseExpensesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.PurchaseExpenses.AsNoTracking().Include(e => e.PurchaseInvoice).ThenInclude(i => i!.Supplier).AsQueryable();

        if (request.PurchaseInvoiceId is { } purchaseInvoiceId)
        {
            query = query.Where(e => e.PurchaseInvoiceId == purchaseInvoiceId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();

            var matchingTypes = Enum.GetValues<Domain.Purchasing.PurchaseExpenseType>()
                .Where(v => v.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            query = query.Where(e =>
                e.PurchaseInvoice!.InvoiceNumber.Contains(term) ||
                e.PurchaseInvoice!.Supplier!.Code.Contains(term) ||
                e.PurchaseInvoice!.Supplier!.NameAr.Contains(term) ||
                matchingTypes.Contains(e.ExpenseType));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var descending = string.Equals(request.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = request.SortBy switch
        {
            "amount" => descending ? query.OrderByDescending(e => e.Amount) : query.OrderBy(e => e.Amount),
            _ => descending ? query.OrderByDescending(e => e.Id) : query.OrderBy(e => e.Id)
        };

        var pageSize = request.PageSize is > 0 and <= 500 ? request.PageSize : 25;
        var page = request.Page > 0 ? request.Page : 1;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new PurchaseExpenseListItemDto
            {
                Id = e.Id,
                PurchaseInvoiceId = e.PurchaseInvoiceId,
                InvoiceNumber = e.PurchaseInvoice!.InvoiceNumber,
                SupplierCode = e.PurchaseInvoice!.Supplier!.Code,
                SupplierNameAr = e.PurchaseInvoice!.Supplier!.NameAr,
                ExpenseType = e.ExpenseType.ToString(),
                Amount = e.Amount,
                AllocationMethod = e.AllocationMethod.ToString()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<PurchaseExpenseListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
