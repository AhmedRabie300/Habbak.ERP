using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Sales.Customers.Commands.CreateCustomer;
using Habbak.ERP.Application.Sales.Customers.Commands.DeleteCustomer;
using Habbak.ERP.Application.Sales.Customers.Commands.UpdateCustomer;
using Habbak.ERP.Application.Sales.Customers.Queries.GetCustomerById;
using Habbak.ERP.Application.Sales.Customers.Queries.GetCustomersList;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Sales;

/// <summary>/sales/customers — screen #1 (04-Module-Sales.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("SALES_CUSTOMERS", LookupReads = true)]
[MaskFields("Customer")]
[Route("api/v1/sales/customers")]
public class CustomersController(ISender mediator) : ControllerBase
{
    public sealed record CreateCustomerRequest(
        string? Code, long? BranchId, string NameAr, string NameEn, CustomerType CustomerType,
        string? Phone, string? Email, string? Address, decimal CreditLimit, int PaymentTermDays,
        long? ReceivableAccountId, long? LoyaltyTierId);

    public sealed record UpdateCustomerRequest(
        long? BranchId, string NameAr, string NameEn, CustomerType CustomerType,
        string? Phone, string? Email, string? Address, decimal CreditLimit, int PaymentTermDays,
        long? ReceivableAccountId, long? LoyaltyTierId, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCustomersListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetCustomerByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateCustomerCommand
        {
            Code = request.Code,
            BranchId = request.BranchId,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            CustomerType = request.CustomerType,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            CreditLimit = request.CreditLimit,
            PaymentTermDays = request.PaymentTermDays,
            ReceivableAccountId = request.ReceivableAccountId,
            LoyaltyTierId = request.LoyaltyTierId
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateCustomerCommand
        {
            Id = id,
            BranchId = request.BranchId,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            CustomerType = request.CustomerType,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            CreditLimit = request.CreditLimit,
            PaymentTermDays = request.PaymentTermDays,
            ReceivableAccountId = request.ReceivableAccountId,
            LoyaltyTierId = request.LoyaltyTierId,
            IsActive = request.IsActive
        }, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteCustomerCommand(id), cancellationToken);
        return NoContent();
    }
}
