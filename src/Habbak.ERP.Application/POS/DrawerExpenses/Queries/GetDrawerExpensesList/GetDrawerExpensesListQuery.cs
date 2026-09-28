using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.POS.DrawerExpenses.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.DrawerExpenses.Queries.GetDrawerExpensesList;

public sealed record GetDrawerExpensesListQuery(long? ShiftId) : IRequest<IReadOnlyList<DrawerExpenseDto>>;

public sealed class GetDrawerExpensesListQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDrawerExpensesListQuery, IReadOnlyList<DrawerExpenseDto>>
{
    public async Task<IReadOnlyList<DrawerExpenseDto>> Handle(GetDrawerExpensesListQuery request, CancellationToken cancellationToken)
    {
        var query = db.DrawerExpenses.AsNoTracking().Include(e => e.ExpenseAccount).AsQueryable();

        if (request.ShiftId is { } shiftId)
        {
            query = query.Where(e => e.ShiftId == shiftId);
        }

        return await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .Select(e => new DrawerExpenseDto
            {
                Id = e.Id,
                ShiftId = e.ShiftId,
                ExpenseAccountId = e.ExpenseAccountId,
                ExpenseAccountNameAr = e.ExpenseAccount!.NameAr,
                Amount = e.Amount,
                Description = e.Description,
                CreatedAtUtc = e.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }
}
