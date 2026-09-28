using Habbak.ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.Reports.Queries;

// ---- Report #8 (enhancement): مصروفات الشراء حسب النوع — aggregates the new PurchaseExpense
// breakdown rows (section 4.8) by ExpenseType, alongside the existing invoice-level report. ----

public sealed class PurchaseExpenseTypeSummaryDto
{
    public required string ExpenseType { get; init; }
    public required int Count { get; init; }
    public required decimal TotalAmount { get; init; }
}

public sealed record GetPurchaseExpensesByTypeReportQuery(DateOnly From, DateOnly To) : IRequest<IReadOnlyList<PurchaseExpenseTypeSummaryDto>>;

public sealed class GetPurchaseExpensesByTypeReportQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseExpensesByTypeReportQuery, IReadOnlyList<PurchaseExpenseTypeSummaryDto>>
{
    public async Task<IReadOnlyList<PurchaseExpenseTypeSummaryDto>> Handle(GetPurchaseExpensesByTypeReportQuery request, CancellationToken cancellationToken) =>
        await db.PurchaseExpenses
            .AsNoTracking()
            .Where(e => e.PurchaseInvoice!.InvoiceDate >= request.From && e.PurchaseInvoice!.InvoiceDate <= request.To)
            .GroupBy(e => e.ExpenseType)
            .Select(g => new PurchaseExpenseTypeSummaryDto
            {
                ExpenseType = g.Key.ToString(),
                Count = g.Count(),
                TotalAmount = g.Sum(e => e.Amount)
            })
            .OrderByDescending(r => r.TotalAmount)
            .ToListAsync(cancellationToken);
}
