using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Queries.GetWarehouseDocumentById;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Queries.GetWarehouseDocumentsList;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>
/// Screen #20 (02-Module-Inventory-Manufacturing.md, section 5) — "صرف واستلام الإنتاج", a
/// READ-ONLY view. ProductionIssue/ProductionReceipt WarehouseDocuments are only ever created by
/// CompleteProductionOrderCommand, already Posted (section 4.1's automatic-document rule) — unlike
/// StockIn/StockOut/TransferOrder, there is deliberately no Create/Update/Post action here.
/// </summary>
[ApiController]
[Authorize]
public abstract class ProductionDocumentsControllerBase(ISender mediator) : ControllerBase
{
    protected abstract WarehouseDocumentType FixedDocumentType { get; }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWarehouseDocumentsListQuery
        {
            DocumentType = FixedDocumentType,
            Search = query.Search,
            Page = query.Page,
            PageSize = query.PageSize,
            SortBy = query.SortBy,
            SortDir = query.SortDir
        }, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var document = await mediator.Send(new GetWarehouseDocumentByIdQuery(id), cancellationToken);

        if (!string.Equals(document.DocumentType, FixedDocumentType.ToString(), StringComparison.Ordinal))
        {
            return NotFound();
        }

        return Ok(document);
    }
}
