using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.POS.BlendTypes.Commands.CreateBlendType;
using Habbak.ERP.Application.POS.BlendTypes.Commands.DeleteBlendType;
using Habbak.ERP.Application.POS.BlendTypes.Commands.GenerateBlendTicket;
using Habbak.ERP.Application.POS.BlendTypes.Commands.UpdateBlendType;
using Habbak.ERP.Application.POS.BlendTypes.Queries.GetBlendTypesList;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.POS;

/// <summary>/pos/blend-types — مراجعة 2026-09-13، بند 2.1: قائمة مخصّصة ومبسّطة لأنواع البن
/// المتاحة للخلط في شاشة استشاري التصنيع، منفصلة عن شاشة الأصناف العامة.</summary>
[ApiController]
[Authorize]
[Screen("POS_BLEND_TYPES", "POS_BLEND_CONSULTATION", LookupReads = true)]
[Route("api/v1/pos/blend-types")]
public class BlendTypesController(ISender mediator) : ControllerBase
{
    public sealed record CreateBlendTypeRequest(string? Code, string NameAr, string NameEn, decimal PricePerGram);
    public sealed record UpdateBlendTypeRequest(string NameAr, string NameEn, decimal PricePerGram, bool IsActive);
    public sealed record CompositionRequest(long BlendTypeId, decimal WeightGrams);
    public sealed record GenerateTicketRequest(IReadOnlyList<CompositionRequest> Compositions);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetBlendTypesListQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBlendTypeRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateBlendTypeCommand(request.Code, request.NameAr, request.NameEn, request.PricePerGram), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateBlendTypeRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateBlendTypeCommand(id, request.NameAr, request.NameEn, request.PricePerGram, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteBlendTypeCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("generate-ticket")]
    public async Task<IActionResult> GenerateTicket([FromBody] GenerateTicketRequest request, CancellationToken cancellationToken)
    {
        var compositions = request.Compositions.Select(c => new BlendCompositionInput(c.BlendTypeId, c.WeightGrams)).ToList();
        var result = await mediator.Send(new GenerateBlendTicketCommand(compositions), cancellationToken);
        return Ok(new { id = result.Id, idempotencyKey = result.IdempotencyKey, totalWeightGrams = result.TotalWeightGrams, totalPrice = result.TotalPrice });
    }
}
