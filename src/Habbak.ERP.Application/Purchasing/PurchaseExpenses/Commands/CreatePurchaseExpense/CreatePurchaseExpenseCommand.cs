using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseExpenses.Commands.CreatePurchaseExpense;

public sealed record CreatePurchaseExpenseCommand : IRequest<long>
{
    public required long PurchaseInvoiceId { get; init; }
    public required PurchaseExpenseType ExpenseType { get; init; }
    public required decimal Amount { get; init; }
    public required CostAllocationMethod AllocationMethod { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreatePurchaseExpenseCommandValidator : AbstractValidator<CreatePurchaseExpenseCommand>
{
    public CreatePurchaseExpenseCommandValidator()
    {
        RuleFor(x => x.PurchaseInvoiceId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class CreatePurchaseExpenseCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<CreatePurchaseExpenseCommand, long>
{
    public async Task<long> Handle(CreatePurchaseExpenseCommand request, CancellationToken cancellationToken)
    {
        if (!await db.PurchaseInvoices.AnyAsync(i => i.Id == request.PurchaseInvoiceId, cancellationToken))
        {
            throw new NotFoundException(nameof(PurchaseInvoice), request.PurchaseInvoiceId);
        }

        var expense = new PurchaseExpense
        {
            CompanyId = currentCompanyContext.CompanyId,
            PurchaseInvoiceId = request.PurchaseInvoiceId,
            ExpenseType = request.ExpenseType,
            Amount = request.Amount,
            AllocationMethod = request.AllocationMethod,
            Notes = request.Notes
        };

        db.PurchaseExpenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);

        return expense.Id;
    }
}
