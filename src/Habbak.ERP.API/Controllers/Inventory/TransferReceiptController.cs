using Habbak.ERP.API.Auth;
using Habbak.ERP.API.Contracts;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Commands.CreateTransferReceipt;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Commands.PostWarehouseDocument;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Commands.UpdateTransferReceipt;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Dtos;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Queries.GetPostedTransferOrdersList;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Queries.GetWarehouseDocumentById;
using Habbak.ERP.Application.Inventory.WarehouseDocuments.Queries.GetWarehouseDocumentsList;
using Habbak.ERP.Domain.Inventory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Inventory;

/// <summary>/inventory/transfer-receipt — screen #11 (02-Module-Inventory-Manufacturing.md,
/// section 5), the one explicitly marked "شاشة تفاعلية" (non-standard). Doesn't inherit
/// WarehouseDocumentsControllerBase: Create/Update copy fields from the related, already-Posted
/// TransferOrder rather than taking them from the caller (rule 34/37), a shape too different from
/// the generic Create/Update to share.</summary>
[ApiController]
[Authorize]
[Screen("INVENTORY_TRANSFER_RECEIPT")]
[Route("api/v1/inventory/transfer-receipt")]
public class TransferReceiptController(ISender mediator) : ControllerBase
{
    public sealed record CreateTransferReceiptRequest(
        long RelatedWarehouseDocumentId, long? BranchId, DateOnly DocumentDate, long DestinationWarehouseId,
        string? Notes, IReadOnlyList<TransferReceiptLineInput> Lines);

    public sealed record UpdateTransferReceiptRequest(
        string RowVersion, long? BranchId, DateOnly DocumentDate, long DestinationWarehouseId,
        string? Notes, IReadOnlyList<TransferReceiptLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ListQuery query, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetWarehouseDocumentsListQuery
        {
            DocumentType = WarehouseDocumentType.TransferReceipt,
            Search = query.Search,
            Page = query.Page,
            PageSize = query.PageSize,
            SortBy = query.SortBy,
            SortDir = query.SortDir
        }, cancellationToken);

        return Ok(result);
    }

    /// <summary>Feeds the order picker on the create screen.</summary>
    [HttpGet("postable-orders")]
    public async Task<IActionResult> GetPostableOrders(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPostedTransferOrdersListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var document = await mediator.Send(new GetWarehouseDocumentByIdQuery(id), cancellationToken);

        if (!string.Equals(document.DocumentType, nameof(WarehouseDocumentType.TransferReceipt), StringComparison.Ordinal))
        {
            return NotFound();
        }

        return Ok(document);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTransferReceiptRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateTransferReceiptCommand
        {
            RelatedWarehouseDocumentId = request.RelatedWarehouseDocumentId,
            BranchId = request.BranchId,
            DocumentDate = request.DocumentDate,
            DestinationWarehouseId = request.DestinationWarehouseId,
            Notes = request.Notes,
            Lines = request.Lines
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateTransferReceiptRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateTransferReceiptCommand
        {
            Id = id,
            RowVersion = request.RowVersion,
            BranchId = request.BranchId,
            DocumentDate = request.DocumentDate,
            DestinationWarehouseId = request.DestinationWarehouseId,
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
