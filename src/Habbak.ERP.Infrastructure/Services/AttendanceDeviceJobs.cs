using Habbak.ERP.Application.Attendance.Attendances.Commands;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Habbak.ERP.Infrastructure.Services;

public sealed record AttendanceDeviceJobResult(long CompanyId, int Processed, int Skipped, string? Error);

/// <summary>
/// Docs/Implementation/Phase-3B-Research.md §4/§6 (3B.4) — يترجم RawPunch (خام) لـTimeEntry
/// (Source = Device). نفس بنية FixedAssetJobs: لف على كل الشركات النشطة، كل شركة جوه
/// BackgroundCompanyScope مستقل، شركة فشلت بتتسجّل وبتتخطّى من غير ما توقف الباقي. عكس
/// FixedAssetJobs (اللي بيستدعي Commands جاهزة عن طريق ISender)، معالجة RawPunch نفسها مالهاش
/// Command جاهز فبتحصل مباشرة على AppDbContext هنا (نفس أسلوب MaintenanceJobs) — الـRecompute
/// (خطوة موجودة بالفعل من Phase 3) بيتنادى بنداء مباشر لـHandler-ها (RecomputeDailyAttendanceCommandHandler
/// محتاج IApplicationDbContext بس)، مش عن طريق ISender — نفس فلسفة RawPunchIngestor، وبيخلّي
/// <see cref="ProcessCompanyAsync"/> قابل للاختبار مباشرة بـAppDbContext من غير أي DI Container.
/// </summary>
public sealed class AttendanceDeviceJobs(IServiceScopeFactory scopes, ILogger logger)
{
    public async Task<IReadOnlyList<AttendanceDeviceJobResult>> RunAsync(CancellationToken cancellationToken)
    {
        List<long> companies;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            companies = await db.RawPunches.IgnoreQueryFilters()
                .Where(p => p.ProcessingStatus != RawPunchProcessingStatus.Processed && p.CompanyId != null)
                .Select(p => p.CompanyId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        var results = new List<AttendanceDeviceJobResult>();
        foreach (var companyId in companies)
        {
            results.Add(await RunForCompanyAsync(companyId, cancellationToken));
        }

        return results;
    }

    public async Task<AttendanceDeviceJobResult> RunForCompanyAsync(long companyId, CancellationToken cancellationToken)
    {
        using var company = BackgroundCompanyScope.Begin(companyId);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await ProcessCompanyAsync(companyId, db, logger, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Attendance device job failed for company {Company}.", companyId);
            return new AttendanceDeviceJobResult(companyId, 0, 0, ex.Message);
        }
    }

    /// <summary>المنطق الفعلي، مفصول عن إنشاء الـScope عشان الاختبارات تقدر تستدعيه مباشرة بـ
    /// AppDbContext حقيقي (نفس فلسفة IngestDevicePunchesCommandHandler/RawPunchIngestor).</summary>
    public static async Task<AttendanceDeviceJobResult> ProcessCompanyAsync(long companyId, IApplicationDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        // §4 بند 2 — "بيفضل قابل لإعادة المحاولة (مش Terminal)": أي صف Skipped (NoEmployeeMapping)
        // بيتحاول تاني كل تشغيلة، لحد ما الربط يتضاف.
        var pending = await db.RawPunches
            .Where(p => p.ProcessingStatus != RawPunchProcessingStatus.Processed)
            .OrderBy(p => p.PunchTimestampUtc)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return new AttendanceDeviceJobResult(companyId, 0, 0, null);
        }

        var mappingLookup = await db.EmployeeDeviceMappings
            .ToDictionaryAsync(m => (m.AttendanceDeviceId, m.DeviceUserId), m => m.EmployeeId, cancellationToken);

        var employeeIds = mappingLookup.Values.Distinct().ToList();
        var employees = await db.Employees.Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => new { e.CompanyId, e.BranchId }, cancellationToken);

        var dayCounters = new Dictionary<(long EmployeeId, DateOnly Day), int>();
        var touchedDays = new HashSet<(long EmployeeId, DateOnly Day)>();
        var utcNow = DateTime.UtcNow;
        var processed = 0;
        var skipped = 0;

        foreach (var punch in pending)
        {
            if (!mappingLookup.TryGetValue((punch.AttendanceDeviceId, punch.DeviceUserId), out var employeeId) || !employees.TryGetValue(employeeId, out var employee))
            {
                punch.ProcessingStatus = RawPunchProcessingStatus.Skipped;
                punch.SkipReason = RawPunchSkipReason.NoEmployeeMapping;
                skipped++;
                continue;
            }

            var day = DateOnly.FromDateTime(punch.PunchTimestampUtc);
            var key = (employeeId, day);
            if (!dayCounters.TryGetValue(key, out var countSoFar))
            {
                var dayStart = day.ToDateTime(TimeOnly.MinValue);
                countSoFar = await db.TimeEntries.CountAsync(
                    t => t.EmployeeId == employeeId && t.Status == TimeEntryStatus.Accepted && t.TimestampUtc >= dayStart && t.TimestampUtc < dayStart.AddDays(1),
                    cancellationToken);
            }

            // §7 قرار 2 — RawStatus معروف (0=In, 1=Out) بيتستخدم زي ما هو، وإلا تبادل تلقائي
            // (أول بصمة في اليوم In، اللي بعدها Out...) على مستوى (EmployeeId, Day) شامل أي
            // TimeEntry تاني (مش بس اللي من الجهاز)، عشان التبادل يفضل متّسق مع الواقع.
            var entryType = punch.RawStatus switch
            {
                0 => TimeEntryType.In,
                1 => TimeEntryType.Out,
                _ => countSoFar % 2 == 0 ? TimeEntryType.In : TimeEntryType.Out
            };

            var entry = new TimeEntry
            {
                CompanyId = employee.CompanyId,
                BranchId = employee.BranchId,
                EmployeeId = employeeId,
                EntryType = entryType,
                TimestampUtc = punch.PunchTimestampUtc,
                Source = TimeEntrySource.Device,
                DeviceId = punch.AttendanceDeviceId,
                Status = TimeEntryStatus.Accepted
            };
            db.TimeEntries.Add(entry);

            punch.ProcessingStatus = RawPunchProcessingStatus.Processed;
            punch.ProcessedAtUtc = utcNow;
            punch.ResultTimeEntry = entry; // FK بيتظبط لوحده وقت SaveChanges من غير ما نستنى الـId.
            punch.SkipReason = null;

            dayCounters[key] = countSoFar + 1;
            touchedDays.Add(key);
            processed++;
        }

        await db.SaveChangesAsync(cancellationToken);

        // نداء مباشر لـHandler موجود بالفعل من Phase 3 (محتاج IApplicationDbContext بس) — نفس
        // فلسفة RawPunchIngestor، مفيش داعي لـISender/Scope تاني لكل يوم.
        foreach (var (employeeId, day) in touchedDays)
        {
            await new RecomputeDailyAttendanceCommandHandler(db).Handle(new RecomputeDailyAttendanceCommand(employeeId, day), cancellationToken);
        }

        if (processed > 0 || skipped > 0)
        {
            logger.LogInformation("Attendance device job for company {Company}: {Processed} punches processed, {Skipped} skipped.", companyId, processed, skipped);
        }

        return new AttendanceDeviceJobResult(companyId, processed, skipped, null);
    }
}

/// <summary>
/// يشتغل كل IntervalMinutes (افتراضيًا 10 — §7 قرار 5، قابل للتهيئة عن طريق
/// AttendanceDeviceJob:IntervalMinutes)، مطفي لما AttendanceDeviceJob:Enabled = "false" (Test hosts،
/// نفس نمط MaintenanceHostedService).
/// </summary>
public sealed class AttendanceDeviceProcessingHostedService(IConfiguration configuration, IServiceScopeFactory scopes, ILogger<AttendanceDeviceProcessingHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(configuration["AttendanceDeviceJob:Enabled"], "false", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var intervalMinutes = int.TryParse(configuration["AttendanceDeviceJob:IntervalMinutes"], out var configured) && configured > 0 ? configured : 10;

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await new AttendanceDeviceJobs(scopes, logger).RunAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
