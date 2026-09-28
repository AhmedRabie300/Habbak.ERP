using Habbak.ERP.API.Contracts;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Commands.CreateWarehouseDocument;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Commands.PostWarehouseDocument;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Commands.UpdateWarehouseDocument;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Dtos;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Queries.GetWarehouseDocumentById;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Queries.GetWarehouseDocumentsList;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>
/// Shared implementation for the Stock In and Stock Out screens (02-Module-Inventory-Manufacturing.md,
/// section 5, screens 8-9) — same underlying WarehouseDocument entity, DocumentType fixed by which
/// concrete route was hit, never by client input (mirrors VouchersControllerBase).
/// </summary>
[ApiController]
[Authorize]
public abstract class WarehouseDocumentsControllerBase(ISender mediator) : ControllerBase
{
    protected abstract WarehouseDocumentType FixedDocumentType { get; }

    public sealed record CreateWarehouseDocumentRequest(
        long? BranchId, DateOnly DocumentDate, long? SourceWarehouseId, long? DestinationWarehouseId,
        long? CustodyOfficerId, string? Notes, IReadOnlyList<WarehouseDocumentLineInput> Lines);

    public sealed record UpdateWarehouseDocumentRequest(
        string RowVersion, long? BranchId, DateOnly DocumentDate, long? SourceWarehouseId, long? DestinationWarehouseId,
        long? CustodyOfficerId, string? Notes, IReadOnlyList<WarehouseDocumentLineInput> Lines);

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

        // A stock-out document's Id doesn't exist from the stock-in screen's perspective (and vice
        // versa) — same generic 404 either way, no existence leaked across the type boundary.
        if (!string.Equals(document.DocumentType, FixedDocumentType.ToString(), StringComparison.Ordinal))
        {
            return NotFound();
        }

        return Ok(document);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseDocumentRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateWarehouseDocumentCommand
        {
            DocumentType = FixedDocumentType,
            BranchId = request.BranchId,
            DocumentDate = request.DocumentDate,
            SourceWarehouseId = request.SourceWarehouseId,
            DestinationWarehouseId = request.DestinationWarehouseId,
            CustodyOfficerId = request.CustodyOfficerId,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateWarehouseDocumentRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateWarehouseDocumentCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            DocumentDate = request.DocumentDate,
            SourceWarehouseId = request.SourceWarehouseId,
            DestinationWarehouseId = request.DestinationWarehouseId,
            CustodyOfficerId = request.CustodyOfficerId,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new PostWarehouseDocumentCommand(id, IdempotencyHeader.Read(this)), cancellationToken));
    }
}
