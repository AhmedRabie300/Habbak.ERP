using Habbak.ERP.Application.Attendance.AttendanceDevices;
using Habbak.ERP.Application.Attendance.AttendanceDevices.Commands;
using Habbak.ERP.Application.Attendance.EmployeeDeviceMappings.Commands;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Persistence;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Habbak.ERP.IntegrationTests.Attendance;

/// <summary>
/// Docs/Implementation/Phase-3B-Research.md §Phase 3B — السيناريوهات الإلزامية: Deduplication عند
/// الإدخال (§3.1)، ترجمة RawPunch → TimeEntry (RawStatus معروف/تبادل تلقائي، §7 قرار 2)، Skip
/// قابل لإعادة المحاولة (NoEmployeeMapping)، Recompute تلقائي بعد المعالجة، وقيود الفرادة (SerialNumber
/// على مستوى الـDB، DeviceUserId لكل جهاز). نفس نمط AttendanceAndLeaveTests.cs (LocalDB حقيقية).
/// </summary>
public sealed class AttendanceDevicesTests(PostingServiceFixture fixture) : IClassFixture<PostingServiceFixture>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);
    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, 20)];

    private async Task<long> AddEmployeeAsync(AppDbContext db, long companyId, long? branchId = 1)
    {
        var orgUnit = new OrgUnit { CompanyId = companyId, Code = Unique("OU"), NameAr = "إدارة", NameEn = "Dept" };
        db.OrgUnits.Add(orgUnit);
        var grade = new JobGrade { CompanyId = companyId, Code = Unique("JG"), NameAr = "أ", NameEn = "A", Level = 1 };
        db.JobGrades.Add(grade);
        await db.SaveChangesAsync();

        var jobPosition = new JobPosition { CompanyId = companyId, Code = Unique("JP"), NameAr = "كاشير", NameEn = "Cashier", OrgUnitId = orgUnit.Id };
        db.JobPositions.Add(jobPosition);
        await db.SaveChangesAsync();

        var employee = new Employee
        {
            CompanyId = companyId, BranchId = branchId, Code = Unique("E"), NameAr = "موظف", NameEn = "Employee",
            OrgUnitId = orgUnit.Id, JobPositionId = jobPosition.Id, JobGradeId = grade.Id,
            HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime, Status = EmployeeStatus.Active
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee.Id;
    }

    private static async Task<long> AddAttendanceDeviceAsync(AppDbContext db, long companyId, string? serialNumber = null)
    {
        var device = new AttendanceDevice
        {
            CompanyId = companyId, Code = Unique("DEV"), NameAr = "جهاز", NameEn = "Device",
            SerialNumber = serialNumber ?? Unique("SN"), IsActive = true, DeviceSecretHash = "irrelevant-for-these-tests"
        };
        db.AttendanceDevices.Add(device);
        await db.SaveChangesAsync();
        return device.Id;
    }

    private static async Task AddMappingAsync(AppDbContext db, long companyId, long employeeId, long attendanceDeviceId, string deviceUserId)
    {
        db.EmployeeDeviceMappings.Add(new EmployeeDeviceMapping { CompanyId = companyId, EmployeeId = employeeId, AttendanceDeviceId = attendanceDeviceId, DeviceUserId = deviceUserId });
        await db.SaveChangesAsync();
    }

    // --------------------------------------------------------------------------------- Ingestion / dedup

    [Fact]
    public async Task Ingesting_the_same_punch_twice_in_one_batch_stores_only_one_raw_punch()
    {
        var companyId = NewCompanyId();
        long deviceId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            deviceId = await AddAttendanceDeviceAsync(db, companyId);
        }

        var timestamp = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var lines = new[] { new RawPunchLineInput("77", timestamp, null, null), new RawPunchLineInput("77", timestamp, null, null) };
            var result = await new IngestDevicePunchesCommandHandler(db).Handle(new IngestDevicePunchesCommand(deviceId, lines, RawPunchSourceType.Push), CancellationToken.None);
            Assert.Equal(2, result.Received);
            Assert.Equal(1, result.Accepted);
            Assert.Equal(1, result.Duplicates);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        Assert.Equal(1, await probe.RawPunches.CountAsync(p => p.AttendanceDeviceId == deviceId));
    }

    [Fact]
    public async Task Ingesting_the_same_punch_again_in_a_second_batch_is_a_no_op()
    {
        var companyId = NewCompanyId();
        long deviceId;
        var timestamp = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            deviceId = await AddAttendanceDeviceAsync(db, companyId);
            await new IngestDevicePunchesCommandHandler(db).Handle(
                new IngestDevicePunchesCommand(deviceId, [new RawPunchLineInput("77", timestamp, null, null)], RawPunchSourceType.Push), CancellationToken.None);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            // نفس الـPush جالي تاني (إعادة إرسال الجهاز) — مفيش صف جديد.
            var result = await new IngestDevicePunchesCommandHandler(db).Handle(
                new IngestDevicePunchesCommand(deviceId, [new RawPunchLineInput("77", timestamp, null, null)], RawPunchSourceType.Push), CancellationToken.None);
            Assert.Equal(0, result.Accepted);
            Assert.Equal(1, result.Duplicates);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        Assert.Equal(1, await probe.RawPunches.CountAsync(p => p.AttendanceDeviceId == deviceId));
        // كل Push (نجح Accept أو Duplicate) بيسجّل صف AttendanceDeviceLog — سجل المزامنة نفسه، مش البصمات.
        Assert.Equal(2, await probe.AttendanceDeviceLogs.CountAsync(l => l.AttendanceDeviceId == deviceId));
    }

    [Fact]
    public async Task Ingesting_updates_the_device_last_seen_timestamp()
    {
        var companyId = NewCompanyId();
        long deviceId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            deviceId = await AddAttendanceDeviceAsync(db, companyId);
            Assert.Null((await db.AttendanceDevices.FindAsync(deviceId))!.LastSeenAtUtc);

            await new IngestDevicePunchesCommandHandler(db).Handle(
                new IngestDevicePunchesCommand(deviceId, [new RawPunchLineInput("1", DateTime.UtcNow, null, null)], RawPunchSourceType.Push), CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        Assert.NotNull((await probe.AttendanceDevices.FindAsync(deviceId))!.LastSeenAtUtc);
    }

    // --------------------------------------------------------------------------------- Job: RawPunch -> TimeEntry

    [Fact]
    public async Task Device_job_translates_a_mapped_punch_into_an_accepted_time_entry_and_recomputes_attendance()
    {
        var companyId = NewCompanyId();
        long deviceId, employeeId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            deviceId = await AddAttendanceDeviceAsync(db, companyId);
            employeeId = await AddEmployeeAsync(db, companyId);
            await AddMappingAsync(db, companyId, employeeId, deviceId, "501");

            var timestamp = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
            await new IngestDevicePunchesCommandHandler(db).Handle(
                new IngestDevicePunchesCommand(deviceId, [new RawPunchLineInput("501", timestamp, RawStatus: 0, RawVerifyType: null)], RawPunchSourceType.Push), CancellationToken.None);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var result = await AttendanceDeviceJobs.ProcessCompanyAsync(companyId, db, NullLogger.Instance, CancellationToken.None);
            Assert.Equal(1, result.Processed);
            Assert.Equal(0, result.Skipped);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var entry = await probe.TimeEntries.SingleAsync(t => t.EmployeeId == employeeId);
        Assert.Equal(TimeEntrySource.Device, entry.Source);
        Assert.Equal(deviceId, entry.DeviceId);
        Assert.Equal(TimeEntryType.In, entry.EntryType); // RawStatus=0 -> In (§7 قرار 2)
        Assert.Equal(TimeEntryStatus.Accepted, entry.Status);

        var rawPunch = await probe.RawPunches.SingleAsync(p => p.AttendanceDeviceId == deviceId);
        Assert.Equal(RawPunchProcessingStatus.Processed, rawPunch.ProcessingStatus);
        Assert.Equal(entry.Id, rawPunch.ResultTimeEntryId);

        // الـRecompute بيتنادى تلقائيًا بعد المعالجة (§4 بند 4) — يوم فيه دخول بس لسه مفيش خروج
        // يبقى Present (بمنطق RecomputeDailyAttendanceCommandHandler الموجود من Phase 3).
        var attendance = await probe.Attendances.SingleAsync(a => a.EmployeeId == employeeId);
        Assert.Equal(AttendanceStatus.Present, attendance.Status);
    }

    [Fact]
    public async Task Device_job_alternates_in_and_out_when_raw_status_is_unknown()
    {
        var companyId = NewCompanyId();
        long deviceId, employeeId;
        var day = new DateOnly(2026, 10, 6);
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            deviceId = await AddAttendanceDeviceAsync(db, companyId);
            employeeId = await AddEmployeeAsync(db, companyId);
            await AddMappingAsync(db, companyId, employeeId, deviceId, "502");

            var lines = new[]
            {
                new RawPunchLineInput("502", day.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Utc), null, null),
                new RawPunchLineInput("502", day.ToDateTime(new TimeOnly(17, 0), DateTimeKind.Utc), null, null)
            };
            await new IngestDevicePunchesCommandHandler(db).Handle(new IngestDevicePunchesCommand(deviceId, lines, RawPunchSourceType.Push), CancellationToken.None);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            await AttendanceDeviceJobs.ProcessCompanyAsync(companyId, db, NullLogger.Instance, CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var entries = await probe.TimeEntries.Where(t => t.EmployeeId == employeeId).OrderBy(t => t.TimestampUtc).ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Equal(TimeEntryType.In, entries[0].EntryType);
        Assert.Equal(TimeEntryType.Out, entries[1].EntryType);
    }

    [Fact]
    public async Task Device_job_skips_an_unmapped_punch_and_processes_it_once_the_mapping_is_added()
    {
        var companyId = NewCompanyId();
        long deviceId, employeeId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            deviceId = await AddAttendanceDeviceAsync(db, companyId);
            employeeId = await AddEmployeeAsync(db, companyId);
            await new IngestDevicePunchesCommandHandler(db).Handle(
                new IngestDevicePunchesCommand(deviceId, [new RawPunchLineInput("999", DateTime.UtcNow, null, null)], RawPunchSourceType.Push), CancellationToken.None);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var firstRun = await AttendanceDeviceJobs.ProcessCompanyAsync(companyId, db, NullLogger.Instance, CancellationToken.None);
            Assert.Equal(0, firstRun.Processed);
            Assert.Equal(1, firstRun.Skipped);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            Assert.Equal(RawPunchSkipReason.NoEmployeeMapping, (await db.RawPunches.SingleAsync(p => p.AttendanceDeviceId == deviceId)).SkipReason);
            await AddMappingAsync(db, companyId, employeeId, deviceId, "999");
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            // نفس الصف Skipped بيتحاول تاني (مش Terminal، §4 بند 2) — دلوقتي فيه ربط، فبيتعالج.
            var secondRun = await AttendanceDeviceJobs.ProcessCompanyAsync(companyId, db, NullLogger.Instance, CancellationToken.None);
            Assert.Equal(1, secondRun.Processed);
            Assert.Equal(0, secondRun.Skipped);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        Assert.Equal(RawPunchProcessingStatus.Processed, (await probe.RawPunches.SingleAsync(p => p.AttendanceDeviceId == deviceId)).ProcessingStatus);
    }

    // --------------------------------------------------------------------------------- Uniqueness rules

    [Fact]
    public async Task Creating_a_device_with_a_serial_number_already_in_use_is_rejected()
    {
        var companyId = NewCompanyId();
        var serial = Unique("SN");
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            await AddAttendanceDeviceAsync(db, companyId, serial);
        }

        await using var second = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var handler = new CreateAttendanceDeviceCommandHandler(second, new TestCurrentCompanyContext(companyId), new CodeGenerator(second, new TestCurrentCompanyContext(companyId)), new BcryptPasswordHasher());
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(new CreateAttendanceDeviceCommand(null, "جهاز تاني", "Second Device", null, serial, null), CancellationToken.None));
    }

    [Fact]
    public async Task Linking_two_employees_to_the_same_device_user_id_on_the_same_device_is_rejected()
    {
        var companyId = NewCompanyId();
        long deviceId, employeeAId, employeeBId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            deviceId = await AddAttendanceDeviceAsync(db, companyId);
            employeeAId = await AddEmployeeAsync(db, companyId);
            employeeBId = await AddEmployeeAsync(db, companyId);
            await AddMappingAsync(db, companyId, employeeAId, deviceId, "42");
        }

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var handler = new CreateEmployeeDeviceMappingCommandHandler(db2, new TestCurrentCompanyContext(companyId));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            handler.Handle(new CreateEmployeeDeviceMappingCommand(employeeBId, deviceId, "42"), CancellationToken.None));
    }

}

/// <summary>
/// Docs/Implementation/Phase-3B-Research.md §6 (3B.5) — Parsing خالص، مفيش داعي لـLocalDB (عكس باقي
/// كلاس AttendanceDevicesTests اللي بيستخدم PostingServiceFixture) — كلاس منفصل عشان Fixture واحد
/// بيتهيّأ لكل الاختبارات في نفس الكلاس حتى لو مش محتاجينه (xUnit IClassFixture).
/// </summary>
public sealed class RawPunchFileParserTests
{
    [Fact]
    public void Parses_tab_separated_dat_export_and_skips_a_header_row()
    {
        var content = "PIN\tTime\tStatus\tVerify\r\n501\t2026-10-05 08:00:00\t0\t1\r\n501\t2026-10-05 17:00:00\t1\t1\r\n";
        var lines = RawPunchFileParser.Parse("export.dat", System.Text.Encoding.UTF8.GetBytes(content));

        Assert.Equal(2, lines.Count);
        Assert.Equal("501", lines[0].DeviceUserId);
        Assert.Equal(0, lines[0].RawStatus);
        Assert.Equal(1, lines[1].RawStatus);
    }

    [Fact]
    public void Parses_comma_separated_csv()
    {
        var content = "601,2026-10-05T08:00:00Z,0,1\n601,2026-10-05T17:00:00Z,1,1\n";
        var lines = RawPunchFileParser.Parse("export.csv", System.Text.Encoding.UTF8.GetBytes(content));

        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Equal("601", l.DeviceUserId));
    }

    [Fact]
    public void Rejects_an_unsupported_extension()
    {
        Assert.Throws<BusinessRuleException>(() => RawPunchFileParser.Parse("export.pdf", [1, 2, 3]));
    }
}
