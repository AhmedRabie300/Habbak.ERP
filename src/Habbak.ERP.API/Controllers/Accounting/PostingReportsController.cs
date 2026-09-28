using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Posting.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.Accounting;

/// <summary>/accounting/posting-reports — the posting engine's reports (00-Posting-Engine-Architecture.md, section 10).</summary>
[ApiController]
[Authorize]
[Screen("ACCOUNTING_POSTING_REPORTS")]
[Route("api/v1/accounting/posting-reports")]
public class PostingReportsController(ISender mediator) : ControllerBase
{
    [HttpGet("auto-entries")]
    public async Task<IActionResult> AutoEntries([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string? sourceModule,
        [FromQuery] long? branchId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAutoEntriesReportQuery(from, to, sourceModule, branchId), cancellationToken));

    [HttpGet("failures")]
    public async Task<IActionResult> Failures([FromQuery] bool includeResolved, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPostingFailuresReportQuery(includeResolved), cancellationToken));

    [HttpGet("by-source")]
    public async Task<IActionResult> BySource([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEntriesBySourceReportQuery(from, to), cancellationToken));

    [HttpGet("pos-entries")]
    public async Task<IActionResult> POSEntries([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] long? branchId,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPOSEntriesReportQuery(from, to, branchId), cancellationToken));

    [HttpGet("shift-variances")]
    public async Task<IActionResult> ShiftVariances([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] long? branchId,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetShiftVarianceReportQuery(from, to, branchId), cancellationToken));

    [HttpGet("periods")]
    public async Task<IActionResult> Periods(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPeriodStatusReportQuery(), cancellationToken));

    [HttpGet("integrity")]
    public async Task<IActionResult> Integrity(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEntryIntegrityReportQuery(), cancellationToken));

    [HttpGet("template-usage")]
    public async Task<IActionResult> TemplateUsage(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetTemplateUsageReportQuery(), cancellationToken));

    [HttpGet("unposted-documents")]
    public async Task<IActionResult> UnpostedDocuments(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetUnpostedDocumentsReportQuery(), cancellationToken));
}
