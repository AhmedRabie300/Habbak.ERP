using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.FixedAssets;
using Habbak.ERP.Domain.FixedAssets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.FixedAssets;

/// <summary>/maintenance/categories — screen #8 (08-Module-Maintenance-FixedAssets.md, section 2.2.1).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.MaintenanceCategories, LookupReads = true)]
[Route("api/v1/maintenance/categories")]
public class MaintenanceCategoriesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetMaintenanceCategoriesQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveMaintenanceCategoryCommand command, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(command with { Id = null }, cancellationToken) });

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveMaintenanceCategoryCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteMaintenanceCategoryCommand(id), cancellationToken);
        return NoContent();
    }
}

/// <summary>/maintenance/issues — screen #9, fault reports (section 2.2.2).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.MaintenanceIssues)]
[Route("api/v1/maintenance/issues")]
public class MaintenanceIssuesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] MaintenanceIssueStatus? status, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetMaintenanceIssuesQuery(status), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMaintenanceIssueCommand command, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(command, cancellationToken) });

    [HttpPost("{id:long}/inspect")]
    public async Task<IActionResult> Inspect(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new InspectMaintenanceIssueCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectMaintenanceIssueCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelMaintenanceIssueCommand(id), cancellationToken);
        return NoContent();
    }
}

/// <summary>/maintenance/requests — screen #10, the maintenance job itself (section 2.2.3).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.MaintenanceRequests)]
[Route("api/v1/maintenance/requests")]
public class MaintenanceRequestsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] MaintenanceRequestStatus? status, [FromQuery] long? fixedAssetId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetMaintenanceRequestsQuery(status, fixedAssetId), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetMaintenanceRequestQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMaintenanceRequestCommand command, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(command, cancellationToken) });

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateMaintenanceRequestCommand(id, request.RowVersion, request.Data), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new ApproveMaintenanceRequestCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/start")]
    public async Task<IActionResult> Start(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new StartMaintenanceRequestCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Issues the stocked parts and posts the cost — the entries of rule 20.</summary>
    [HttpPost("{id:long}/complete")]
    public async Task<IActionResult> Complete(long id, [FromBody] CompleteRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new CompleteMaintenanceRequestCommand(id, request.CompletedDate), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    public async Task<IActionResult> Reject(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RejectMaintenanceRequestCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new CancelMaintenanceRequestCommand(id), cancellationToken);
        return NoContent();
    }

    public sealed record UpdateRequest(string RowVersion, MaintenanceRequestInput Data);
    public sealed record CompleteRequest(DateOnly CompletedDate);
}

/// <summary>/maintenance/schedules — screen #11, preventive maintenance (section 2.2.5).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.MaintenanceSchedules)]
[Route("api/v1/maintenance/schedules")]
public class MaintenanceSchedulesController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetMaintenanceSchedulesQuery(), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveMaintenanceScheduleCommand command, CancellationToken cancellationToken) =>
        Ok(new { id = await mediator.Send(command with { Id = null }, cancellationToken) });

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveMaintenanceScheduleCommand command, CancellationToken cancellationToken)
    {
        await mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteMaintenanceScheduleCommand(id), cancellationToken);
        return NoContent();
    }
}

/// <summary>/maintenance/board — screen #12, the Kanban board (section 2.2.6).</summary>
[ApiController]
[Authorize]
[Screen(FixedAssetScreens.MaintenanceBoard)]
[Route("api/v1/maintenance/board")]
public class MaintenanceBoardController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int doneDays = 14, CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetMaintenanceBoardQuery(DateOnly.FromDateTime(DateTime.Now), doneDays), cancellationToken));
}
