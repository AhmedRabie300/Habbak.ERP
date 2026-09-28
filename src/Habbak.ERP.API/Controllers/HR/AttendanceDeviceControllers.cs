using Habbak.ERP.API.Auth;
using Habbak.ERP.Application.Attendance.AttendanceDevices.Commands;
using Habbak.ERP.Application.Attendance.AttendanceDevices.Queries;
using Habbak.ERP.Application.Attendance.EmployeeDeviceMappings.Commands;
using Habbak.ERP.Application.Attendance.EmployeeDeviceMappings.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Habbak.ERP.API.Controllers.HR;

/// <summary>
/// Docs/Implementation/Phase-3B-Research.md §6, Sub-Batch 3B.3 — إدارة أجهزة البصمة (Admin CRUD،
/// نفس نمط AttendanceControllers.cs). التسجيل/إعادة التوليد بيرجّع السر الخام مرة واحدة بس — مش
/// متخزّن، Hash فقط (DeviceSecretHash). بروتوكول الاستقبال الفعلي (Push/File Import) في Controllers
/// تانية منفصلة (AttendanceDevicePushControllers.cs) — دول [AllowAnonymous] بمصادقة يدوية بالسر،
/// عكس الشاشات هنا اللي محتاجة JWT + صلاحية شاشة عادية.
/// </summary>
[ApiController]
[Authorize]
[Screen("HR_ATTENDANCE_DEVICES")]
[Route("api/v1/hr/attendance-devices")]
public class AttendanceDevicesController(ISender mediator) : ControllerBase
{
    /// <summary>10 MB — نفس حد AttachmentsController.</summary>
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public sealed record CreateRequest(string? Code, string NameAr, string NameEn, string? Model, string? SerialNumber, long? BranchId);
    public sealed record UpdateRequest(string NameAr, string NameEn, string? Model, string? SerialNumber, long? BranchId, bool IsActive);

    /// <summary>Swashbuckle محتاج IFormFile جوه Model class مستقل (نفس ملاحظة AttachmentsController).</summary>
    public sealed class ImportRequest
    {
        public IFormFile File { get; set; } = null!;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken cancellationToken) => Ok(await mediator.Send(new GetAttendanceDevicesListQuery(), cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken) => Ok(await mediator.Send(new GetAttendanceDeviceByIdQuery(id), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new CreateAttendanceDeviceCommand(request.Code, request.NameAr, request.NameEn, request.Model, request.SerialNumber, request.BranchId), cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateAttendanceDeviceCommand(id, request.NameAr, request.NameEn, request.Model, request.SerialNumber, request.BranchId, request.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteAttendanceDeviceCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/regenerate-secret")]
    public async Task<IActionResult> RegenerateSecret(long id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new RegenerateAttendanceDeviceSecretCommand(id), cancellationToken));

    /// <summary>Sub-Batch 3B.5 — رفع يدوي (تصدير USB: csv/txt/dat/xlsx)، نفس مسار المعالجة بتاع الـPush.</summary>
    [HttpPost("{id:long}/import")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> Import(long id, [FromForm] ImportRequest request, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await request.File.CopyToAsync(stream, cancellationToken);

        var result = await mediator.Send(new ImportRawPunchesFromFileCommand(id, request.File.FileName, stream.ToArray()), cancellationToken);
        return Ok(result);
    }
}

[ApiController]
[Authorize]
[Screen("HR_EMPLOYEE_DEVICE_MAPPINGS")]
[Route("api/v1/hr/employee-device-mappings")]
public class EmployeeDeviceMappingsController(ISender mediator) : ControllerBase
{
    public sealed record CreateRequest(long EmployeeId, long AttendanceDeviceId, string DeviceUserId);
    public sealed record UpdateRequest(string DeviceUserId);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long? employeeId, [FromQuery] long? attendanceDeviceId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetEmployeeDeviceMappingsListQuery(employeeId, attendanceDeviceId), cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest request, CancellationToken cancellationToken)
    {
        var id = await mediator.Send(new CreateEmployeeDeviceMappingCommand(request.EmployeeId, request.AttendanceDeviceId, request.DeviceUserId), cancellationToken);
        return Ok(new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UpdateRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new UpdateEmployeeDeviceMappingCommand(id, request.DeviceUserId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteEmployeeDeviceMappingCommand(id), cancellationToken);
        return NoContent();
    }
}

/// <summary>شاشة عرض بس (§6: 3B.6) — سجل كل محاولة Sync.</summary>
[ApiController]
[Authorize]
[Screen("HR_ATTENDANCE_DEVICE_LOGS", LookupReads = true)]
[Route("api/v1/hr/attendance-device-logs")]
public class AttendanceDeviceLogsController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] long? attendanceDeviceId, [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAttendanceDeviceLogsQuery(attendanceDeviceId, fromDate, toDate), cancellationToken));
}

/// <summary>شاشة عرض بس (§5 Reconciliation) — عدد البصمات الخام مقابل TimeEntry الناتجة + الاستثناءات.</summary>
[ApiController]
[Authorize]
[Screen("HR_ATTENDANCE_RECONCILIATION", LookupReads = true)]
[Route("api/v1/hr/attendance-reconciliation")]
public class AttendanceReconciliationController(ISender mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long? attendanceDeviceId, [FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate, CancellationToken cancellationToken)
    {
        var (summary, exceptions) = await mediator.Send(new GetRawPunchReconciliationQuery(attendanceDeviceId, fromDate, toDate), cancellationToken);
        return Ok(new { summary, exceptions });
    }
}
