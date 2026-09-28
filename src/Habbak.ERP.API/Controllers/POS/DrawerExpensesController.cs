using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.DrawerExpenses.Commands.CreateDrawerExpense;
using Habbak.ERP.Application.POS.DrawerExpenses.Queries.GetDrawerExpensesList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/drawer-expenses — screen #17 (05-Module-POS-Shifts.md, section 3، قاعدة 33).</summary>
[ApiController]
[Authorize]
[Screen("POS_DRAWER_EXPENSES")]
[Route("api/v1/pos/drawer-expenses")]
public class DrawerExpensesController(ISender mediator) : ControllerBase
{
    public sealed record CreateRequest(long ShiftId, long ExpenseAccountId, decimal Amount, string Description);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long? shiftId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDrawerExpensesListQuery(shiftId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateDrawerExpenseCommand(request.ShiftId, request.ExpenseAccountId, request.Amount, request.Description), cancellationToken);
        return Ok(new { id });
    }
}
