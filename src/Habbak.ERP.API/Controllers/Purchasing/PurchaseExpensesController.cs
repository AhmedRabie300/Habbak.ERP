using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Purchasing.PurchaseExpenses.Commands.CreatePurchaseExpense;
using Habbak.ERP.Application.Purchasing.PurchaseExpenses.Commands.DeletePurchaseExpense;
using Habbak.ERP.Application.Purchasing.PurchaseExpenses.Commands.UpdatePurchaseExpense;
using Habbak.ERP.Application.Purchasing.PurchaseExpenses.Queries.GetPurchaseExpenseById;
using Habbak.ERP.Application.Purchasing.PurchaseExpenses.Queries.GetPurchaseExpensesList;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/purchase-expenses — screen #8 (03-Module-Purchasing.md, section 8),
/// itemizes a PurchaseInvoice's AdditionalCosts breakdown.</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_PURCHASE_EXPENSES")]
[Route("api/v1/purchasing/purchase-expenses")]
public class PurchaseExpensesController(ISender mediator) : ControllerBase
{
    public sealed record CreatePurchaseExpenseRequest(
        long PurchaseInvoiceId, PurchaseExpenseType ExpenseType, decimal Amount, CostAllocationMethod AllocationMethod, string? Notes);

    public sealed record UpdatePurchaseExpenseRequest(
        string RowVersion, PurchaseExpenseType ExpenseType, decimal Amount, CostAllocationMethod AllocationMethod, string? Notes);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetPurchaseExpensesListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPurchaseExpenseByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseExpenseRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePurchaseExpenseCommand
        {
            PurchaseInvoiceId = request.PurchaseInvoiceId,
            ExpenseType = request.ExpenseType,
            Amount = request.Amount,
            AllocationMethod = request.AllocationMethod,
            Notes = request.Notes
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdatePurchaseExpenseRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdatePurchaseExpenseCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            ExpenseType = request.ExpenseType,
            Amount = request.Amount,
            AllocationMethod = request.AllocationMethod,
            Notes = request.Notes
        }, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeletePurchaseExpenseCommand(id), cancellationToken);
        return NoContent();
    }
}
