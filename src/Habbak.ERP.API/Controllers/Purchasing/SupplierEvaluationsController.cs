using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Purchasing.SupplierEvaluations.Commands.CreateSupplierEvaluation;
using Habbak.ERP.Application.Purchasing.SupplierEvaluations.Commands.DeleteSupplierEvaluation;
using Habbak.ERP.Application.Purchasing.SupplierEvaluations.Commands.UpdateSupplierEvaluation;
using Habbak.ERP.Application.Purchasing.SupplierEvaluations.Queries.GetSupplierEvaluationById;
using Habbak.ERP.Application.Purchasing.SupplierEvaluations.Queries.GetSupplierEvaluationsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Purchasing;

/// <summary>/purchasing/supplier-evaluations — screen #12 (03-Module-Purchasing.md, section 8).</summary>
[ApiController]
[Authorize]
[Screen("PURCHASING_SUPPLIER_EVALUATIONS")]
[Route("api/v1/purchasing/supplier-evaluations")]
public class SupplierEvaluationsController(ISender mediator) : ControllerBase
{
    public sealed record CreateSupplierEvaluationRequest(
        long SupplierId, DateOnly EvaluationDate, decimal QualityScore, decimal DeliveryTimeScore, decimal QuantityComplianceScore, string? Notes);

    public sealed record UpdateSupplierEvaluationRequest(
        string RowVersion, long SupplierId, DateOnly EvaluationDate, decimal QualityScore, decimal DeliveryTimeScore, decimal QuantityComplianceScore, string? Notes);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] GetSupplierEvaluationsListQuery query, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(query, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetSupplierEvaluationByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSupplierEvaluationRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateSupplierEvaluationCommand
        {
            SupplierId = request.SupplierId,
            EvaluationDate = request.EvaluationDate,
            QualityScore = request.QualityScore,
            DeliveryTimeScore = request.DeliveryTimeScore,
            QuantityComplianceScore = request.QuantityComplianceScore,
            Notes = request.Notes
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateSupplierEvaluationRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateSupplierEvaluationCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            SupplierId = request.SupplierId,
            EvaluationDate = request.EvaluationDate,
            QualityScore = request.QualityScore,
            DeliveryTimeScore = request.DeliveryTimeScore,
            QuantityComplianceScore = request.QuantityComplianceScore,
            Notes = request.Notes
        }, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteSupplierEvaluationCommand(id), cancellationToken);
        return NoContent();
    }
}
