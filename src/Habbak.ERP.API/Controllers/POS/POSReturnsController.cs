using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.Returns.Commands.CreatePOSReturn;
using Habbak.ERP.Application.POS.Returns.Dtos;
using Habbak.ERP.Application.POS.Returns.Queries.GetPOSReturnById;
using Habbak.ERP.Application.POS.Returns.Queries.GetPOSReturnsList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/returns — screen #6 (05-Module-POS-Shifts.md، قاعدة 16). لا يوجد Update/Delete —
/// المرتجع يتسجل ويترحّل في نفس الفعل (زي POSInvoice، مفيش Draft).</summary>
[ApiController]
[Authorize]
[Screen("POS_RETURNS")]
[Route("api/v1/pos/returns")]
public class POSReturnsController(ISender mediator) : ControllerBase
{
    public sealed record CreateRequest(long ShiftId, long SourceInvoiceId, DateOnly ReturnDate, string Reason, IReadOnlyList<POSReturnLineInput> Lines);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPOSReturnsListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPOSReturnByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreatePOSReturnCommand(request.ShiftId, request.SourceInvoiceId, request.ReturnDate, request.Reason, request.Lines), cancellationToken);
        return Ok(new { id });
    }
}
