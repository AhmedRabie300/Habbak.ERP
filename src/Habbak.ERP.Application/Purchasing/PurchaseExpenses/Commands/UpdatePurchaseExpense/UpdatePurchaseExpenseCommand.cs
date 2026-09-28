using FluentValidation;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseExpenses.Commands.UpdatePurchaseExpense;

/// <summary>No Status/workflow to lock against — always editable, same as ContractItem/SupplierEvaluation.</summary>
public sealed record UpdatePurchaseExpenseCommand : IRequest
{
    public required long Id { get; init; }

    /// <summary>Base64 RowVersion from the GetById response — 409 Conflict on mismatch.</summary>
    public required string RowVersion { get; init; }

    public required PurchaseExpenseType ExpenseType { get; init; }
    public required decimal Amount { get; init; }
    public required CostAllocationMethod AllocationMethod { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdatePurchaseExpenseCommandValidator : AbstractValidator<UpdatePurchaseExpenseCommand>
{
    public UpdatePurchaseExpenseCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public sealed class UpdatePurchaseExpenseCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdatePurchaseExpenseCommand>
{
    public async Task Handle(UpdatePurchaseExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await db.PurchaseExpenses.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseExpense), request.Id);

        db.Entry(expense).Property(nameof(PurchaseExpense.RowVersion)).OriginalValue = Convert.FromBase64String(request.RowVersion);

        expense.ExpenseType = request.ExpenseType;
        expense.Amount = request.Amount;
        expense.AllocationMethod = request.AllocationMethod;
        expense.Notes = request.Notes;

        await db.SaveChangesAsync(cancellationToken);
    }
}
