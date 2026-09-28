using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.FixedAssets;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.FixedAssets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.FixedAssets;

/// <summary>/fixed-assets/categories — screen #1 (08-Module-Maintenance-FixedAssets.md, section 2.1.1).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.Categories, LookupReads = true)]
[Route("api/v1/fixed-assets/categories")]
public class FixedAssetCategoriesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetFixedAssetCategoriesQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetFixedAssetCategoryQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFixedAssetCategoryCommand command, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(command, cancellationToken) });

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] FixedAssetCategoryUpdateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateFixedAssetCategoryCommand(id, request.RowVersion, request.Data), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteFixedAssetCategoryCommand(id), cancellationToken);
        return NoContent();
    }

    public sealed record FixedAssetCategoryUpdateRequest(string RowVersion, FixedAssetCategoryInput Data);
}

/// <summary>/fixed-assets — screen #2, plus the schedule of one asset (section 2.1.2).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.Assets, LookupReads = true)]
[Route("api/v1/fixed-assets")]
public class FixedAssetsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] FixedAssetStatus? status, [FromQuery] long? categoryId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetFixedAssetsQuery(status, categoryId), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetFixedAssetQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFixedAssetRequest request, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(new CreateFixedAssetCommand(request.AssetNumber, request.Data), cancellationToken) });

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateFixedAssetRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateFixedAssetCommand(id, request.RowVersion, request.Data), cancellationToken);
        return NoContent();
    }

    /// <summary>Capitalises the asset: posts the acquisition and lays out its depreciation (rules 8-10).</summary>
    [ScreenAction(ScreenAction.Approve)]
    [HttpPost("{id:long}/activate")]
    public async Task<IActionResult> Activate(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ActivateFixedAssetCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteFixedAssetCommand(id), cancellationToken);
        return NoContent();
    }

    public sealed record CreateFixedAssetRequest(string? AssetNumber, FixedAssetInput Data);
    public sealed record UpdateFixedAssetRequest(string RowVersion, FixedAssetInput Data);
}

/// <summary>/fixed-assets/depreciation-schedule — screen #3, the periods of every asset (section 2.1.3).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.Schedule)]
[Route("api/v1/fixed-assets/depreciation-schedule")]
public class DepreciationScheduleController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] long? fixedAssetId, [FromQuery] int? year, [FromQuery] int? month, [FromQuery] DepreciationScheduleStatus? status,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDepreciationScheduleQuery(fixedAssetId, year, month, status), cancellationToken));
}

/// <summary>/fixed-assets/depreciation-runs — screen #4, the month's run (section 2.1.4).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.DepreciationRuns)]
[Route("api/v1/fixed-assets/depreciation-runs")]
public class DepreciationRunsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDepreciationRunsQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDepreciationRunQuery(id), cancellationToken));

    /// <summary>Creates the month's run, or returns the one it already has (rule 28).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDepreciationRunCommand command, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(command, cancellationToken));

    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new PostDepreciationRunCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reverse")]
    public async Task<IActionResult> Reverse(long id, [FromBody] ReverseRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new ReverseDepreciationRunCommand(id, request.Reason), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteDepreciationRunCommand(id), cancellationToken);
        return NoContent();
    }

    public sealed record ReverseRequest(string Reason);
}

/// <summary>/fixed-assets/transfers — screen #5 (section 2.1.5).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.Transfers)]
[Route("api/v1/fixed-assets/transfers")]
public class AssetTransfersController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long? fixedAssetId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAssetTransfersQuery(fixedAssetId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAssetTransferCommand command, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(command, cancellationToken) });

    /// <summary>
    /// Edit is enough to post; the Approve permission is demanded by the handler only when the
    /// settings ask for an approval on transfers (so the button is not hidden from the people who do it).
    /// </summary>
    [ScreenAction(ScreenAction.Edit)]
    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new PostAssetTransferCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectAssetTransferCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelAssetTransferCommand(id), cancellationToken);
        return NoContent();
    }
}

/// <summary>/fixed-assets/disposals — screen #6 (section 2.1.6).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.Disposals)]
[Route("api/v1/fixed-assets/disposals")]
public class AssetDisposalsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long? fixedAssetId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAssetDisposalsQuery(fixedAssetId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAssetDisposalCommand command, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(command, cancellationToken) });

    /// <summary>Approval is demanded by the handler when the settings require it (they do by default).</summary>
    [ScreenAction(ScreenAction.Edit)]
    [HttpPost("{id:long}/post")]
    public async Task<IActionResult> Post(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new PostAssetDisposalCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectAssetDisposalCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelAssetDisposalCommand(id), cancellationToken);
        return NoContent();
    }
}

/// <summary>/fixed-assets/physical-counts — screen #7, with the next count due (sections 2.1.7-2.1.8).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.PhysicalCounts)]
[Route("api/v1/fixed-assets/physical-counts")]
public class AssetPhysicalCountsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAssetPhysicalCountsQuery(), cancellationToken));

    [HttpGet("schedule")]
    public async Task<IActionResult> GetSchedule(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAssetCountScheduleQuery(DateOnly.FromDateTime(DateTime.Now)), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAssetPhysicalCountQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAssetPhysicalCountCommand command, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(command, cancellationToken) });

    [HttpPost("{id:long}/start")]
    public async Task<IActionResult> Start(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new StartAssetPhysicalCountCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/record")]
    public async Task<IActionResult> Record(long id, [FromBody] RecordRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new RecordAssetPhysicalCountCommand(id, request.Lines), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/complete")]
    public async Task<IActionResult> Complete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CompleteAssetPhysicalCountCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectAssetPhysicalCountCommand(id), cancellationToken);
        return NoContent();
    }

    public sealed record RecordRequest(IReadOnlyList<AssetCountLineInput> Lines);
}

/// <summary>/fixed-assets/settings — screen #13 (section 2.3.1).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.Settings)]
[Route("api/v1/fixed-assets/settings")]
public class AssetSettingsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAssetSettingsQuery(), cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] AssetSettingsDto settings, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateAssetSettingsCommand(settings), cancellationToken);
        return NoContent();
    }
}
