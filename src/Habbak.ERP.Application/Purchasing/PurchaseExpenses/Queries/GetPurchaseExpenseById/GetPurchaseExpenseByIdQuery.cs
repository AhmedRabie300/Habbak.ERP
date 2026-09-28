using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseExpenses.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseExpenses.Queries.GetPurchaseExpenseById;

public sealed record GetPurchaseExpenseByIdQuery(long Id) : IRequest<PurchaseExpenseDetailDto>;

public sealed class GetPurchaseExpenseByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetPurchaseExpenseByIdQuery, PurchaseExpenseDetailDto>
{
    public async Task<PurchaseExpenseDetailDto> Handle(GetPurchaseExpenseByIdQuery request, CancellationToken cancellationToken)
    {
        var expense = await db.PurchaseExpenses
            .AsNoTracking()
            .Include(e => e.PurchaseInvoice)
            .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseExpense), request.Id);

        return new PurchaseExpenseDetailDto
        {
            Id = expense.Id,
            PurchaseInvoiceId = expense.PurchaseInvoiceId,
            InvoiceNumber = expense.PurchaseInvoice!.InvoiceNumber,
            ExpenseType = expense.ExpenseType.ToString(),
            Amount = expense.Amount,
            AllocationMethod = expense.AllocationMethod.ToString(),
            Notes = expense.Notes,
            RowVersion = Convert.ToBase64String(expense.RowVersion)
        };
    }
}
