using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Sales.Discounts.Commands.CreateDiscount;
using Habbak.ERP.Application.Sales.Discounts.Commands.DeleteDiscount;
using Habbak.ERP.Application.Sales.Discounts.Commands.UpdateDiscount;
using Habbak.ERP.Application.Sales.Discounts.Queries.GetDiscountById;
using Habbak.ERP.Application.Sales.Discounts.Queries.GetDiscountsList;
using Habbak.ERP.Domain.Sales;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Sales;

/// <summary>/sales/discounts — screen #3 (04-Module-Sales.md, section 5).</summary>
[ApiController]
[Authorize]
[Screen("SALES_DISCOUNTS", LookupReads = true)]
[Route("api/v1/sales/discounts")]
public class DiscountsController(ISender mediator) : ControllerBase
{
    public sealed record CreateDiscountRequest(
        string? Code, string NameAr, string NameEn, DiscountType DiscountType, decimal Value, int ApplicationPriority,
        bool IsStackable, decimal? MinInvoiceAmount, decimal? MinQuantity, bool IsHappyHour,
        TimeOnly? HappyHourFromTime, TimeOnly? HappyHourToTime, DateOnly EffectiveFromDate, DateOnly? EffectiveToDate);

    public sealed record UpdateDiscountRequest(
        string NameAr, string NameEn, DiscountType DiscountType, decimal Value, int ApplicationPriority,
        bool IsStackable, decimal? MinInvoiceAmount, decimal? MinQuantity, bool IsHappyHour,
        TimeOnly? HappyHourFromTime, TimeOnly? HappyHourToTime, DateOnly EffectiveFromDate, DateOnly? EffectiveToDate, bool IsActive);

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDiscountsListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDiscountByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDiscountRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateDiscountCommand
        {
            Code = request.Code,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            DiscountType = request.DiscountType,
            Value = request.Value,
            ApplicationPriority = request.ApplicationPriority,
            IsStackable = request.IsStackable,
            MinInvoiceAmount = request.MinInvoiceAmount,
            MinQuantity = request.MinQuantity,
            IsHappyHour = request.IsHappyHour,
            HappyHourFromTime = request.HappyHourFromTime,
            HappyHourToTime = request.HappyHourToTime,
            EffectiveFromDate = request.EffectiveFromDate,
            EffectiveToDate = request.EffectiveToDate
        }, cancellationToken);

        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateDiscountRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateDiscountCommand
        {
            Id = id,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            DiscountType = request.DiscountType,
            Value = request.Value,
            ApplicationPriority = request.ApplicationPriority,
            IsStackable = request.IsStackable,
            MinInvoiceAmount = request.MinInvoiceAmount,
            MinQuantity = request.MinQuantity,
            IsHappyHour = request.IsHappyHour,
            HappyHourFromTime = request.HappyHourFromTime,
            HappyHourToTime = request.HappyHourToTime,
            EffectiveFromDate = request.EffectiveFromDate,
            EffectiveToDate = request.EffectiveToDate,
            IsActive = request.IsActive
        }, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteDiscountCommand(id), cancellationToken);
        return NoContent();
    }
}
