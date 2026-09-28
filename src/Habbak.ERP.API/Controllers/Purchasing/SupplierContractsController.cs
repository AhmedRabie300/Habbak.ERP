using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Purchasing.SupplierContracts.Commands.CancelSupplierContract;
using Habbak.ERP.Application.Purchasing.SupplierContracts.Commands.CreateSupplierContract;
using Habbak.ERP.Application.Purchasing.SupplierContracts.Commands.UpdateSupplierContract;
using Habbak.ERP.Application.Purchasing.SupplierContracts.Dtos;
using Habbak.ERP.Application.Purchasing.SupplierContracts.Queries.GetSupplierContractById;
using Habbak.ERP.Application.Purchasing.SupplierContracts.Queries.GetSupplierContractsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/supplier-contracts — screen #10 (03-Module-Purchasing.md, section 8).</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_SUPPLIER_CONTRACTS")]
[Route("api/v1/purchasing/supplier-contracts")]
public class SupplierContractsController(ISender mediator) : ControllerBase
{
    public sealed record CreateSupplierContractRequest(
        long SupplierId, DateOnly StartDate, DateOnly EndDate, bool AutoRenew, string? Notes, IReadOnlyList<ContractItemInput> Items);

    public sealed record UpdateSupplierContractRequest(
        string RowVersion, long SupplierId, DateOnly StartDate, DateOnly EndDate, bool AutoRenew, string? Notes, IReadOnlyList<ContractItemInput> Items);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetSupplierContractsListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetSupplierContractByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierContractRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateSupplierContractCommand
        {
            SupplierId = request.SupplierId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            AutoRenew = request.AutoRenew,
            Notes = request.Notes,
            Items = request.Items
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSupplierContractRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSupplierContractCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            SupplierId = request.SupplierId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            AutoRenew = request.AutoRenew,
            Notes = request.Notes,
            Items = request.Items
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelSupplierContractCommand(id), cancellationToken);
        return NoContent();
    }
}
