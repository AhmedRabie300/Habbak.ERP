using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.Tables.Commands.CreateTable;
using Habbak.ERP.Application.POS.Tables.Commands.DeleteTable;
using Habbak.ERP.Application.POS.Tables.Commands.SetTableStatus;
using Habbak.ERP.Application.POS.Tables.Commands.UpdateTable;
using Habbak.ERP.Application.POS.Tables.Queries.GetTableBoard;
using Habbak.ERP.Application.POS.Tables.Queries.GetTableById;
using Habbak.ERP.Application.POS.Tables.Queries.GetTablesList;
using Habbak.ERP.Domain.POS;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/tables — screen #8 (05-Module-POS-Shifts.md, section 2.2).</summary>
[ApiController]
[Authorize]
[Screen("POS_TABLE_BOARD", LookupReads = true)]
[Route("api/v1/pos/tables")]
public class TablesController(ISender mediator) : ControllerBase
{
    public sealed record CreateTableRequest(string? Code, string NameAr, string NameEn, long BranchId);
    public sealed record UpdateTableRequest(string NameAr, string NameEn, long BranchId, bool IsActive);
    public sealed record SetTableStatusRequest(TableStatus Status);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetTablesListQuery(), cancellationToken));

    [HttpGet("board/{branchId:long}")]
    public async Task<IActionResult> GetBoard(long branchId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetTableBoardQuery(branchId), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetTableByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTableRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateTableCommand(request.Code, request.NameAr, request.NameEn, request.BranchId), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateTableRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateTableCommand(id, request.NameAr, request.NameEn, request.BranchId, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/status")]
    public async Task<IActionResult> SetStatus(long id, [FromBody] SetTableStatusRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new SetTableStatusCommand(id, request.Status), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTableCommand(id), cancellationToken);
        return NoContent();
    }
}
