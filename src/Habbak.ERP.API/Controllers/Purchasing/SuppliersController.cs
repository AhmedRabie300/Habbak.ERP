using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Purchasing.Suppliers.Commands.CreateSupplier;
using Habbak.ERP.Application.Purchasing.Suppliers.Commands.DeleteSupplier;
using Habbak.ERP.Application.Purchasing.Suppliers.Commands.UpdateSupplier;
using Habbak.ERP.Application.Purchasing.Suppliers.Queries.GetSupplierById;
using Habbak.ERP.Application.Purchasing.Suppliers.Queries.GetSuppliersList;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/suppliers — screen #1 (03-Module-Purchasing.md, section 8).</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_SUPPLIERS", LookupReads = true)]
[MaskFields("Supplier")]
[Route("api/v1/purchasing/suppliers")]
public class SuppliersController(ISender mediator) : ControllerBase
{
    public sealed record CreateSupplierRequest(
        string? Code, string NameAr, string NameEn, string? TaxNumber, string? Phone, string? Email, string? Address,
        SupplierPaymentTerms PaymentTerms, decimal? CreditLimit, string CurrencyCode,
        long? DefaultWarehouseId, long? PayableAccountId, long? ExpenseAccountId);

    public sealed record UpdateSupplierRequest(
        string NameAr, string NameEn, string? TaxNumber, string? Phone, string? Email, string? Address,
        SupplierPaymentTerms PaymentTerms, decimal? CreditLimit, string CurrencyCode,
        long? DefaultWarehouseId, long? PayableAccountId, long? ExpenseAccountId, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetSuppliersListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetSupplierByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateSupplierCommand
        {
            Code = request.Code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            TaxNumber = request.TaxNumber,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            PaymentTerms = request.PaymentTerms,
            CreditLimit = request.CreditLimit,
            CurrencyCode = request.CurrencyCode,
            DefaultWarehouseId = request.DefaultWarehouseId,
            PayableAccountId = request.PayableAccountId,
            ExpenseAccountId = request.ExpenseAccountId
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSupplierRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSupplierCommand
        {
            Id = id,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            TaxNumber = request.TaxNumber,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            PaymentTerms = request.PaymentTerms,
            CreditLimit = request.CreditLimit,
            CurrencyCode = request.CurrencyCode,
            DefaultWarehouseId = request.DefaultWarehouseId,
            PayableAccountId = request.PayableAccountId,
            ExpenseAccountId = request.ExpenseAccountId,
            IsActive = request.IsActive
        }, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteSupplierCommand(id), cancellationToken);
        return NoContent();
    }
}
