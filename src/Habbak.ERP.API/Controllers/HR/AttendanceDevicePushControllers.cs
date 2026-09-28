using Habbak.ERP.Application.Attendance.AttendanceDevices.Commands;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.API.Controllers.HR;

/// <summary>
/// Docs/Implementation/Phase-3B-Research.md §2/§7 — يحلّ الجهاز عن طريق SerialNumber (المفتاح
/// الوحيد اللي الجهاز بيبعته في بروتوكوله، مش Id) ويتحقق من السر يدويًا (§7 قرار 4: Hash فقط
/// مخزَّن، زي كلمة مرور). مفيش JWT/CompanyId هنا أصلًا، فالاستعلام IgnoreQueryFilters() على طول
/// الخط. بعد النجاح، BackgroundCompanyScope.Begin(device.CompanyId) هو اللي بيخلّي أي Command
/// بيتنفّذ بعد كده (IngestDevicePunchesCommand) يشتغل بالـCompanyId الصح من غير أي JWT.
/// </summary>
internal static class DevicePushAuthenticator
{
    public static async Task<AttendanceDevice?> ResolveAsync(IApplicationDbContext db, IPasswordHasher passwordHasher, string? serialNumber, string? secret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) || string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        var device = await db.AttendanceDevices.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => !d.IsDeleted && d.SerialNumber == serialNumber, cancellationToken);

        return device is { IsActive: true } && passwordHasher.Verify(secret, device.DeviceSecretHash) ? device : null;
    }
}

/// <summary>
/// بروتوكول ADMS/iClock المتوافق مع أجهزة ZKTeco الفعلية (§7 قرار 1 — الأقرب لهدف "الجهاز شغّال من
/// أول يوم من غير وسيط"). المسار/الشكل ثابتين بحكم بروتوكول الجهاز نفسه (مش تصميمنا): GET handshake
/// أولي، ثم POST بجسم نصي خام Tab-Separated. السر بيتبعت عن طريق ?key= في الـQuery String (بعض
/// أجهزة/واجهات إدارة ZKTeco بتسمح بتخصيص URL كامل شامل Query String في إعداد عنوان السيرفر —
/// 🔴 فجوة محتاجة تأكيد على جهاز حقيقي، Phase-3B-Research.md §2). التوقيت في جسم الطلب بيتفسَّر
/// كـUTC مباشرة (تبسيط مؤقت — منطقة الجهاز الزمنية مش مؤكَّدة بعد، نفس الفجوة).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("iclock")]
public class IClockPushController(ISender mediator, IApplicationDbContext db, IPasswordHasher passwordHasher) : ControllerBase
{
    [HttpGet("cdata")]
    public async Task<IActionResult> Handshake([FromQuery(Name = "SN")] string sn, [FromQuery] string? key, CancellationToken cancellationToken)
    {
        var device = await DevicePushAuthenticator.ResolveAsync(db, passwordHasher, sn, key, cancellationToken);
        if (device is null)
        {
            return Unauthorized();
        }

        // استجابة الـHandshake القياسية لبروتوكول ADMS — بتخلي الجهاز يبدأ يبعت ATTLOG فورًا.
        return Content("GET OPTION FROM: " + sn + "\r\nStamp=9999\r\nOpStamp=9999\r\nErrorDelay=30\r\nDelay=30\r\nTransFlag=1111000000\r\nRealtime=1\r\nEncrypt=0\r\n", "text/plain");
    }

    [HttpPost("cdata")]
    public async Task<IActionResult> AttendanceLog([FromQuery(Name = "SN")] string sn, [FromQuery] string? key, [FromQuery] string? table, CancellationToken cancellationToken)
    {
        var device = await DevicePushAuthenticator.ResolveAsync(db, passwordHasher, sn, key, cancellationToken);
        if (device is null)
        {
            return Unauthorized();
        }

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var lines = ParseAttLog(body);

        using (Habbak.ERP.Application.Common.Interfaces.BackgroundCompanyScope.Begin(device.CompanyId!.Value))
        {
            await mediator.Send(new IngestDevicePunchesCommand(device.Id, lines, RawPunchSourceType.Push), cancellationToken);
        }

        // ADMS بيستنى "OK" نصي بسيط عشان يعتبر الدفعة اتسلّمت وميعديش يعيد الإرسال.
        return Content("OK", "text/plain");
    }

    /// <summary>كل سطر: PIN\tTime\tStatus\tVerifyType\tWorkCode... (§2). سطر مش قابل للتفسير بيتجاهل بهدوء.</summary>
    private static List<RawPunchLineInput> ParseAttLog(string body)
    {
        var result = new List<RawPunchLineInput>();
        foreach (var rawLine in body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = rawLine.Split('\t');
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]))
            {
                continue;
            }

            if (!DateTime.TryParse(parts[1], out var timestamp))
            {
                continue;
            }

            int? status = parts.Length > 2 && int.TryParse(parts[2], out var s) ? s : null;
            int? verifyType = parts.Length > 3 && int.TryParse(parts[3], out var v) ? v : null;

            result.Add(new RawPunchLineInput(parts[0], DateTime.SpecifyKind(timestamp, DateTimeKind.Utc), status, verifyType));
        }

        return result;
    }
}

/// <summary>
/// بديل JSON مبسّط (§7 قرار 1 — Fallback لأي جهاز/Agent بيتكلم JSON بدل ADMS الخام). نفس منطق
/// المصادقة بالسر، لكن عن طريق Header بدل Query String.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/hr/attendance-devices/push-json")]
public class AttendanceDevicePushJsonController(ISender mediator, IApplicationDbContext db, IPasswordHasher passwordHasher) : ControllerBase
{
    public const string SecretHeaderName = "X-Device-Secret";

    public sealed record PunchInput(string DeviceUserId, DateTime TimestampUtc, int? Status, int? VerifyType);
    public sealed record PushRequest(string SerialNumber, IReadOnlyList<PunchInput> Punches);

    [HttpPost]
    public async Task<IActionResult> Push([FromBody] PushRequest request, CancellationToken cancellationToken)
    {
        var secret = Request.Headers[SecretHeaderName].ToString();
        var device = await DevicePushAuthenticator.ResolveAsync(db, passwordHasher, request.SerialNumber, secret, cancellationToken);
        if (device is null)
        {
            return Unauthorized();
        }

        var lines = request.Punches.Select(p => new RawPunchLineInput(p.DeviceUserId, DateTime.SpecifyKind(p.TimestampUtc, DateTimeKind.Utc), p.Status, p.VerifyType)).ToList();

        IngestResultDto result;
        using (Habbak.ERP.Application.Common.Interfaces.BackgroundCompanyScope.Begin(device.CompanyId!.Value))
        {
            result = await mediator.Send(new IngestDevicePunchesCommand(device.Id, lines, RawPunchSourceType.Push), cancellationToken);
        }

        return Ok(result);
    }
}
