using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.Posting;
using Habbak.ERP.Domain.POS;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.DrawerExpenses.Commands.CreateDrawerExpense;

/// <summary>قاعدة 33: ExpenseAccountId إلزامي. القيد بيترحّل مع المصروف في كل أوضاع الترحيل (قالب
/// POS_DRAWER_EXPENSE): مدين حساب المصروف ودائن خزينة كاش الجهاز. قاعدة 3: لازم ترتبط بوردية Open
/// حاليًا.</summary>
public sealed record CreateDrawerExpenseCommand(long ShiftId, long ExpenseAccountId, decimal Amount, string Description) : IRequest<long>;

public sealed class CreateDrawerExpenseCommandValidator : AbstractValidator<CreateDrawerExpenseCommand>
{
    public CreateDrawerExpenseCommandValidator()
    {
        RuleFor(x => x.ShiftId).GreaterThan(0);
        RuleFor(x => x.ExpenseAccountId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
    }
}

public sealed class CreateDrawerExpenseCommandHandler(IApplicationDbContext db, IPOSPostingService posPosting)
    : IRequestHandler<CreateDrawerExpenseCommand, long>
{
    public async Task<long> Handle(CreateDrawerExpenseCommand request, CancellationToken cancellationToken)
    {
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == request.ShiftId, cancellationToken)
            ?? throw new NotFoundException(nameof(Shift), request.ShiftId);

        if (shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleException("POS-SHIFT-NOT-OPEN", "لا يمكن تسجيل مصروف درج إلا على وردية مفتوحة حاليًا.");
        }

        if (!await db.Accounts.AnyAsync(a => a.Id == request.ExpenseAccountId, cancellationToken))
        {
            throw new NotFoundException("Account", request.ExpenseAccountId);
        }

        var expense = new DrawerExpense
        {
            CompanyId = shift.CompanyId,
            BranchId = shift.BranchId,
            ShiftId = shift.Id,
            ExpenseAccountId = request.ExpenseAccountId,
            Amount = request.Amount,
            Description = request.Description
        };

        // Two saves (the entry needs the expense's id) in one transaction, so a failed entry does not
        // leave the expense behind without it.
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        db.DrawerExpenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);

        await posPosting.PostDrawerExpenseAsync(expense, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return expense.Id;
    }
}
