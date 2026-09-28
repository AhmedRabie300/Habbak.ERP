using Habbak.ERP.Application.Approvals;
using Habbak.ERP.Application.Approvals.Commands;
using Habbak.ERP.Application.Approvals.Outcomes;
using Habbak.ERP.Application.Approvals.Services;
using Habbak.ERP.Application.Attendance.Attendances.Commands;
using Habbak.ERP.Application.Attendance.LeaveBalances;
using Habbak.ERP.Application.Attendance.LeaveBalances.Commands;
using Habbak.ERP.Application.Attendance.LeaveRequests;
using Habbak.ERP.Application.Attendance.LeaveRequests.Commands;
using Habbak.ERP.Application.Attendance.Services;
using Habbak.ERP.Application.Attendance.ShiftSchedules;
using Habbak.ERP.Application.Attendance.ShiftSchedules.Commands;
using Habbak.ERP.Application.Attendance.TimeEntries.Commands;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Approvals;
using Habbak.ERP.Domain.Attendance;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.POS;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests.Attendance;

/// <summary>
/// Docs/Implementation/HR-MASTER-PLAN.md §Phase 3, Sub-Batch 3.7 — السيناريوهات الإلزامية: عدم
/// تعديل TimeEntry (تصحيح بصف جديد)، حساب Attendance من TimeEntry المقبولة، تلقيم POS.Shift
/// (نداء مباشر + Idempotency + Silent Ignore + كشف التعارض)، استحقاق شهري Idempotent، ودورة
/// LeaveRequest كاملة (طلب → اعتماد/رفض → LeaveBalanceHistory). نفس نمط ApprovalWorkflowEngineTests.cs
/// (LocalDB حقيقية، مش Mocks).
/// </summary>
public sealed class AttendanceAndLeaveTests(PostingServiceFixture fixture) : IClassFixture<PostingServiceFixture>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);
    private static string Unique(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 10, 20)];

    private static User NewUser() => new()
    {
        Username = Unique("user"),
        Email = $"{Unique("user")}@test.local",
        PasswordHash = "$2a$12$placeholderplaceholderplaceholderplaceholderplacehold",
        FullName = "مستخدم اختبار",
        Status = UserStatus.Active
    };

    private async Task<long> AddUserAsync(AppDbContext db)
    {
        var user = NewUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<long> AddEmployeeAsync(AppDbContext db, long companyId, long? userId = null, long? branchId = 1)
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
            OrgUnitId = orgUnit.Id, JobPositionId = jobPosition.Id, JobGradeId = grade.Id, UserId = userId,
            HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime, Status = EmployeeStatus.Active
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee.Id;
    }

    private async Task<long> AddPOSTerminalAsync(AppDbContext db, long companyId, long branchId)
    {
        var terminal = new POSTerminal { CompanyId = companyId, BranchId = branchId, Code = Unique("PT"), NameAr = "جهاز", NameEn = "Terminal", IsActive = true };
        db.POSTerminals.Add(terminal);
        await db.SaveChangesAsync();
        return terminal.Id;
    }

    private static Shift NewShift(long companyId, long branchId, long posTerminalId, long cashierUserId, DateTime openedAtUtc, DateTime? closedAtUtc = null) => new()
    {
        CompanyId = companyId, BranchId = branchId, POSTerminalId = posTerminalId, CashierUserId = cashierUserId,
        Status = closedAtUtc is null ? ShiftStatus.Open : ShiftStatus.Closed, OpenedAtUtc = openedAtUtc, ClosedAtUtc = closedAtUtc
    };

    /// <summary>Screen.Code فريد على مستوى الـDB كله (مش لكل شركة) — الفكسشر بيتشارك بين كل
    /// الاختبارات في نفس الكلاس، فلازم Get-or-Create مش Add مباشر (نفس الكود الحقيقي بالظبط
    /// "HR_LEAVE_REQUESTS" مُسجَّل مرة واحدة بس في ScreenSeedData).</summary>
    private async Task<long> GetOrAddScreenAsync(AppDbContext db, string code)
    {
        var existing = await db.Screens.FirstOrDefaultAsync(s => s.Code == code);
        if (existing is not null)
        {
            return existing.Id;
        }

        var screen = new Screen { Code = code, NameAr = "شاشة اختبار", NameEn = "Test Screen", ModuleCode = "HR" };
        db.Screens.Add(screen);
        await db.SaveChangesAsync();
        return screen.Id;
    }

    // --------------------------------------------------------------------------------- TimeEntry immutability

    [Fact]
    public async Task Correcting_a_time_entry_creates_a_new_row_and_leaves_the_original_untouched()
    {
        var companyId = NewCompanyId();
        long employeeId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            employeeId = await AddEmployeeAsync(db, companyId);
        }

        long originalId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var handler = new CreateTimeEntryCommandHandler(db);
            originalId = await handler.Handle(new CreateTimeEntryCommand(employeeId, TimeEntryType.In, new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc)), CancellationToken.None);
        }

        long correctionId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var handler = new CorrectTimeEntryCommandHandler(db);
            correctionId = await handler.Handle(new CorrectTimeEntryCommand(originalId, TimeEntryType.In, new DateTime(2026, 10, 5, 8, 15, 0, DateTimeKind.Utc), "تصحيح وقت الدخول"), CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var original = await probe.TimeEntries.FindAsync(originalId);
        var correction = await probe.TimeEntries.FindAsync(correctionId);

        Assert.NotEqual(originalId, correctionId);
        Assert.False(original!.IsCorrection);
        Assert.Equal(new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc), original.TimestampUtc); // الأصل مايتلمسش
        Assert.True(correction!.IsCorrection);
        Assert.Equal(originalId, correction.CorrectsTimeEntryId);
        Assert.Equal(new DateTime(2026, 10, 5, 8, 15, 0, DateTimeKind.Utc), correction.TimestampUtc);
    }

    // --------------------------------------------------------------------------------- Attendance compute

    [Fact]
    public async Task Recompute_daily_attendance_marks_present_and_computes_worked_minutes_from_accepted_entries()
    {
        var companyId = NewCompanyId();
        long employeeId;
        var workDate = new DateOnly(2026, 10, 5);
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            employeeId = await AddEmployeeAsync(db, companyId);
            db.TimeEntries.Add(new TimeEntry
            {
                CompanyId = companyId, BranchId = 1, EmployeeId = employeeId, EntryType = TimeEntryType.In,
                TimestampUtc = workDate.ToDateTime(new TimeOnly(8, 0)), Source = TimeEntrySource.Manual, Status = TimeEntryStatus.Accepted
            });
            db.TimeEntries.Add(new TimeEntry
            {
                CompanyId = companyId, BranchId = 1, EmployeeId = employeeId, EntryType = TimeEntryType.Out,
                TimestampUtc = workDate.ToDateTime(new TimeOnly(16, 0)), Source = TimeEntrySource.Manual, Status = TimeEntryStatus.Accepted
            });
            // مقترح مرفوض — مايدخلش الحساب خالص (قاعدة 14).
            db.TimeEntries.Add(new TimeEntry
            {
                CompanyId = companyId, BranchId = 1, EmployeeId = employeeId, EntryType = TimeEntryType.Out,
                TimestampUtc = workDate.ToDateTime(new TimeOnly(20, 0)), Source = TimeEntrySource.POSShift, Status = TimeEntryStatus.Dismissed
            });
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var handler = new RecomputeDailyAttendanceCommandHandler(db);
            await handler.Handle(new RecomputeDailyAttendanceCommand(employeeId, workDate), CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var attendance = await probe.Attendances.SingleAsync(a => a.EmployeeId == employeeId && a.WorkDate == workDate);
        Assert.Equal(AttendanceStatus.Present, attendance.Status);
        Assert.Equal(8 * 60, attendance.WorkedMinutes); // 8:00 -> 16:00، مفيش وردية معرَّفة فمفيش استراحة تتخصم
        Assert.False(attendance.IsApproved);
    }

    [Fact]
    public async Task Recompute_daily_attendance_marks_absent_with_no_entries_no_schedule_no_holiday_no_leave()
    {
        var companyId = NewCompanyId();
        long employeeId;
        var workDate = new DateOnly(2026, 10, 6);
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            employeeId = await AddEmployeeAsync(db, companyId);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var handler = new RecomputeDailyAttendanceCommandHandler(db);
            await handler.Handle(new RecomputeDailyAttendanceCommand(employeeId, workDate), CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var attendance = await probe.Attendances.SingleAsync(a => a.EmployeeId == employeeId && a.WorkDate == workDate);
        Assert.Equal(AttendanceStatus.Absent, attendance.Status);
    }

    [Fact]
    public async Task Recompute_daily_attendance_marks_rest_day_from_the_schedule_when_no_entries_exist()
    {
        var companyId = NewCompanyId();
        long employeeId;
        var workDate = new DateOnly(2026, 10, 7);
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            employeeId = await AddEmployeeAsync(db, companyId);
            db.ShiftSchedules.Add(new ShiftSchedule { CompanyId = companyId, BranchId = 1, EmployeeId = employeeId, StartDate = workDate, IsRestDay = true });
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var handler = new RecomputeDailyAttendanceCommandHandler(db);
            await handler.Handle(new RecomputeDailyAttendanceCommand(employeeId, workDate), CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var attendance = await probe.Attendances.SingleAsync(a => a.EmployeeId == employeeId && a.WorkDate == workDate);
        Assert.Equal(AttendanceStatus.RestDay, attendance.Status);
    }

    // --------------------------------------------------------------------------------- POS Shift -> TimeEntry feed

    [Fact]
    public async Task TimeEntryFeed_creates_an_accepted_entry_and_is_idempotent_for_the_same_shift()
    {
        var companyId = NewCompanyId();
        long employeeId, userId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            userId = await AddUserAsync(db);
            employeeId = await AddEmployeeAsync(db, companyId, userId: userId);
        }

        var openedAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        long shiftId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var posTerminalId = await AddPOSTerminalAsync(db, companyId, 1);
            var shift = NewShift(companyId, 1, posTerminalId, userId, openedAt);
            db.Shifts.Add(shift);
            await db.SaveChangesAsync();
            shiftId = shift.Id;

            var feed = new TimeEntryFeedService(db);
            await feed.SuggestEntryForShiftOpenAsync(shift, CancellationToken.None);
            // نفس الوردية تاني — Idempotency (Phase-3-Research.md §3.3).
            await feed.SuggestEntryForShiftOpenAsync(shift, CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var entries = await probe.TimeEntries.Where(t => t.POSShiftId == shiftId).ToListAsync();
        Assert.Single(entries);
        Assert.Equal(employeeId, entries[0].EmployeeId);
        Assert.Equal(TimeEntrySource.POSShift, entries[0].Source);
        Assert.Equal(TimeEntryStatus.Accepted, entries[0].Status); // مفيش تعارض -> قبول تلقائي (قاعدة 11)
        Assert.Equal(TimeEntryType.In, entries[0].EntryType);
    }

    [Fact]
    public async Task TimeEntryFeed_silently_ignores_a_shift_whose_cashier_has_no_linked_employee()
    {
        var companyId = NewCompanyId();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));

        // مستخدم حقيقي (لازم يكون له صف Users فعلي — FK حقيقي على CashierUserId) بس من غير أي
        // Employee مربوط بيه — ده بالظبط سيناريو "الكاشير مش مربوط بموظف" (Phase-3-Research.md §3.2).
        var unlinkedUserId = await AddUserAsync(db);
        var posTerminalId = await AddPOSTerminalAsync(db, companyId, 1);
        var shift = NewShift(companyId, 1, posTerminalId, unlinkedUserId, new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc));
        db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        var feed = new TimeEntryFeedService(db);
        // Best-Effort — مفيش استثناء (Phase-3-Research.md §3.2)، والوردية نفسها مايتلمسش.
        await feed.SuggestEntryForShiftOpenAsync(shift, CancellationToken.None);

        Assert.False(await db.TimeEntries.AnyAsync(t => t.POSShiftId == shift.Id));
        Assert.Equal(ShiftStatus.Open, shift.Status);
    }

    [Fact]
    public async Task TimeEntryFeed_marks_suggested_when_the_employee_already_has_another_entry_that_day()
    {
        var companyId = NewCompanyId();
        long employeeId, userId;
        var day = new DateOnly(2026, 10, 5);
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            userId = await AddUserAsync(db);
            employeeId = await AddEmployeeAsync(db, companyId, userId: userId);
            // تسجيل يدوي سابق في نفس اليوم — تعارض محتمل.
            db.TimeEntries.Add(new TimeEntry
            {
                CompanyId = companyId, BranchId = 1, EmployeeId = employeeId, EntryType = TimeEntryType.In,
                TimestampUtc = day.ToDateTime(new TimeOnly(7, 0)), Source = TimeEntrySource.Manual, Status = TimeEntryStatus.Accepted
            });
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var posTerminalId = await AddPOSTerminalAsync(db, companyId, 1);
            var shift = NewShift(companyId, 1, posTerminalId, userId, day.ToDateTime(new TimeOnly(8, 0)));
            db.Shifts.Add(shift);
            await db.SaveChangesAsync();

            var feed = new TimeEntryFeedService(db);
            await feed.SuggestEntryForShiftOpenAsync(shift, CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var suggested = await probe.TimeEntries.SingleAsync(t => t.Source == TimeEntrySource.POSShift);
        Assert.Equal(TimeEntryStatus.Suggested, suggested.Status);
    }

    // --------------------------------------------------------------------------------- Leave accrual

    [Fact]
    public async Task Accrue_monthly_leave_is_idempotent_for_the_same_employee_and_month()
    {
        var companyId = NewCompanyId();
        long employeeId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            employeeId = await AddEmployeeAsync(db, companyId);
            db.LeaveTypes.Add(new LeaveType
            {
                CompanyId = companyId, Code = Unique("LT"), NameAr = "سنوية", NameEn = "Annual", IsActive = true,
                AccrualMethod = LeaveAccrualMethod.Monthly, AnnualDays = 24, PaidPercentage = 100
            });
            await db.SaveChangesAsync();
        }

        for (var i = 0; i < 2; i++)
        {
            await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
            var handler = new AccrueMonthlyLeaveCommandHandler(db, new TestCurrentCompanyContext(companyId));
            await handler.Handle(new AccrueMonthlyLeaveCommand(2026, 10), CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var balance = await probe.LeaveBalances.SingleAsync(b => b.EmployeeId == employeeId);
        Assert.Equal(2m, balance.AccruedThisYear); // 24/12 = 2 — مرة واحدة بس رغم تشغيلتين
        Assert.Equal(1, await probe.LeaveBalanceHistories.CountAsync(h => h.LeaveBalanceId == balance.Id && h.SourceType == "MonthlyAccrual"));
    }

    // --------------------------------------------------------------------------------- LeaveRequest full cycle

    private async Task<(long EmployeeId, long LeaveTypeId)> SeedEmployeeWithLeaveTypeAndBalanceAsync(AppDbContext db, long companyId, decimal accrued = 21)
    {
        var employeeId = await AddEmployeeAsync(db, companyId);
        var leaveType = new LeaveType
        {
            CompanyId = companyId, Code = Unique("LT"), NameAr = "سنوية", NameEn = "Annual", IsActive = true,
            AccrualMethod = LeaveAccrualMethod.Annual, AnnualDays = 21, PaidPercentage = 100
        };
        db.LeaveTypes.Add(leaveType);
        await db.SaveChangesAsync();

        db.LeaveBalances.Add(new LeaveBalance { CompanyId = companyId, EmployeeId = employeeId, LeaveTypeId = leaveType.Id, Year = 2026, AccruedThisYear = accrued });
        await db.SaveChangesAsync();

        return (employeeId, leaveType.Id);
    }

    [Fact]
    public async Task Create_leave_request_rejects_when_days_exceed_available_balance()
    {
        var companyId = NewCompanyId();
        long employeeId, leaveTypeId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            (employeeId, leaveTypeId) = await SeedEmployeeWithLeaveTypeAndBalanceAsync(db, companyId, accrued: 2);
        }

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var handler = new CreateLeaveRequestCommandHandler(db2);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            new CreateLeaveRequestCommand(employeeId, leaveTypeId, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9), null), CancellationToken.None));
        Assert.Equal("HR-LEAVE-INSUFFICIENT-BALANCE", ex.Code);
    }

    [Fact]
    public async Task Submit_leave_request_without_an_active_workflow_approves_immediately_and_moves_pending_to_used()
    {
        var companyId = NewCompanyId();
        long employeeId, leaveTypeId, requestId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            (employeeId, leaveTypeId) = await SeedEmployeeWithLeaveTypeAndBalanceAsync(db, companyId);
            var createHandler = new CreateLeaveRequestCommandHandler(db);
            requestId = await createHandler.Handle(new CreateLeaveRequestCommand(employeeId, leaveTypeId, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9), null), CancellationToken.None);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var approvalService = new ApprovalWorkflowService(db, new ApprovalStepResolutionService(db), new Habbak.ERP.Application.Notifications.NotificationService(db, new TestCurrentCompanyContext(companyId)));
            var submitHandler = new SubmitLeaveRequestCommandHandler(db, new TestCurrentCompanyContext(companyId), approvalService);
            await submitHandler.Handle(new SubmitLeaveRequestCommand(requestId), CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var request = await probe.LeaveRequests.FindAsync(requestId);
        Assert.Equal(HrRequestStatus.Approved, request!.Status);
        Assert.Null(request.ApprovalInstanceId);

        var balance = await probe.LeaveBalances.SingleAsync(b => b.EmployeeId == employeeId);
        Assert.Equal(5m, balance.Used);
        Assert.Equal(0m, balance.Pending);
        Assert.Equal(1, await probe.LeaveBalanceHistories.CountAsync(h => h.LeaveBalanceId == balance.Id && h.MovementType == LeaveBalanceMovementType.Usage));
    }

    [Fact]
    public async Task Submit_leave_request_with_an_active_workflow_goes_pending_then_approval_moves_the_balance()
    {
        var companyId = NewCompanyId();
        long employeeId, leaveTypeId, requestId, managerUserId, requesterUserId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            managerUserId = await AddUserAsync(db);
            requesterUserId = await AddUserAsync(db);
            (employeeId, leaveTypeId) = await SeedEmployeeWithLeaveTypeAndBalanceAsync(db, companyId);

            var screenId = await GetOrAddScreenAsync(db, "HR_LEAVE_REQUESTS");
            var workflow = new ApprovalWorkflow { CompanyId = companyId, Code = Unique("WF"), NameAr = "سلسلة", NameEn = "Workflow", IsActive = true };
            var step = new ApprovalWorkflowStep { StepOrder = 1, Mode = ApprovalStepMode.AnyOne };
            step.Approvers.Add(new ApprovalStepApprover { ApproverType = ApprovalApproverType.SpecificEmployee, ApproverReferenceId = employeeId });
            workflow.Steps.Add(step);
            db.ApprovalWorkflows.Add(workflow);
            await db.SaveChangesAsync();

            // المعتمد المحدد (SpecificEmployee) لازم موظف مختلف عن مقدّم الطلب — الحارس هنا بس عشان
            // نتجنب اعتماد ذاتي في الاختبار، فبنربط الموظف الأول بـ managerUserId مش requesterUserId.
            var approverEmployee = await db.Employees.FindAsync(employeeId);
            approverEmployee!.UserId = managerUserId;

            db.ApprovalWorkflowAssignments.Add(new ApprovalWorkflowAssignment { CompanyId = companyId, ScreenId = screenId, ApprovalWorkflowId = workflow.Id, IsActive = true });
            await db.SaveChangesAsync();

            var createHandler = new CreateLeaveRequestCommandHandler(db);
            requestId = await createHandler.Handle(new CreateLeaveRequestCommand(employeeId, leaveTypeId, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9), null), CancellationToken.None);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: requesterUserId)))
        {
            var approvalService = new ApprovalWorkflowService(db, new ApprovalStepResolutionService(db), new Habbak.ERP.Application.Notifications.NotificationService(db, new TestCurrentCompanyContext(companyId, userId: requesterUserId)));
            var submitHandler = new SubmitLeaveRequestCommandHandler(db, new TestCurrentCompanyContext(companyId, userId: requesterUserId), approvalService);
            await submitHandler.Handle(new SubmitLeaveRequestCommand(requestId), CancellationToken.None);
        }

        await using (var probe1 = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var pending = await probe1.LeaveRequests.FindAsync(requestId);
            Assert.Equal(HrRequestStatus.Pending, pending!.Status);
            Assert.NotNull(pending.ApprovalInstanceId);

            var balanceMidway = await probe1.LeaveBalances.SingleAsync(b => b.EmployeeId == employeeId);
            Assert.Equal(5m, balanceMidway.Pending); // لسه محجوزة، مش مستخدمة
            Assert.Equal(0m, balanceMidway.Used);
        }

        long instanceId;
        await using (var probe2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            instanceId = (await probe2.LeaveRequests.FindAsync(requestId))!.ApprovalInstanceId!.Value;
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: managerUserId)))
        {
            var approveHandler = new ApproveStepCommandHandler(
                db, new TestCurrentCompanyContext(companyId, userId: managerUserId), new ApprovalStepResolutionService(db),
                new Habbak.ERP.Application.Notifications.NotificationService(db, new TestCurrentCompanyContext(companyId, userId: managerUserId)),
                [new LeaveRequestApprovalOutcomeHandler(db)]);
            await approveHandler.Handle(new ApproveStepCommand(instanceId, null), CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var approved = await probe.LeaveRequests.FindAsync(requestId);
        Assert.Equal(HrRequestStatus.Approved, approved!.Status);

        var balance = await probe.LeaveBalances.SingleAsync(b => b.EmployeeId == employeeId);
        Assert.Equal(5m, balance.Used);
        Assert.Equal(0m, balance.Pending);
        Assert.Equal(1, await probe.LeaveBalanceHistories.CountAsync(h => h.LeaveBalanceId == balance.Id && h.MovementType == LeaveBalanceMovementType.Usage));
    }

    [Fact]
    public async Task Rejecting_a_leave_request_releases_the_pending_hold_without_a_history_row()
    {
        var companyId = NewCompanyId();
        long employeeId, leaveTypeId, requestId, managerUserId, requesterUserId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            managerUserId = await AddUserAsync(db);
            requesterUserId = await AddUserAsync(db);
            (employeeId, leaveTypeId) = await SeedEmployeeWithLeaveTypeAndBalanceAsync(db, companyId);

            var approverEmployeeId = await AddEmployeeAsync(db, companyId, userId: managerUserId);

            var screenId = await GetOrAddScreenAsync(db, "HR_LEAVE_REQUESTS");
            var workflow = new ApprovalWorkflow { CompanyId = companyId, Code = Unique("WF"), NameAr = "سلسلة", NameEn = "Workflow", IsActive = true };
            var step = new ApprovalWorkflowStep { StepOrder = 1, Mode = ApprovalStepMode.AnyOne };
            step.Approvers.Add(new ApprovalStepApprover { ApproverType = ApprovalApproverType.SpecificEmployee, ApproverReferenceId = approverEmployeeId });
            workflow.Steps.Add(step);
            db.ApprovalWorkflows.Add(workflow);
            await db.SaveChangesAsync();

            db.ApprovalWorkflowAssignments.Add(new ApprovalWorkflowAssignment { CompanyId = companyId, ScreenId = screenId, ApprovalWorkflowId = workflow.Id, IsActive = true });
            await db.SaveChangesAsync();

            var createHandler = new CreateLeaveRequestCommandHandler(db);
            requestId = await createHandler.Handle(new CreateLeaveRequestCommand(employeeId, leaveTypeId, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9), null), CancellationToken.None);
        }

        long instanceId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: requesterUserId)))
        {
            var approvalService = new ApprovalWorkflowService(db, new ApprovalStepResolutionService(db), new Habbak.ERP.Application.Notifications.NotificationService(db, new TestCurrentCompanyContext(companyId, userId: requesterUserId)));
            var submitHandler = new SubmitLeaveRequestCommandHandler(db, new TestCurrentCompanyContext(companyId, userId: requesterUserId), approvalService);
            await submitHandler.Handle(new SubmitLeaveRequestCommand(requestId), CancellationToken.None);
            instanceId = (await db.LeaveRequests.FindAsync(requestId))!.ApprovalInstanceId!.Value;
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: managerUserId)))
        {
            var rejectHandler = new RejectStepCommandHandler(
                db, new TestCurrentCompanyContext(companyId, userId: managerUserId), new ApprovalStepResolutionService(db),
                new Habbak.ERP.Application.Notifications.NotificationService(db, new TestCurrentCompanyContext(companyId, userId: managerUserId)),
                [new LeaveRequestApprovalOutcomeHandler(db)]);
            await rejectHandler.Handle(new RejectStepCommand(instanceId, "مش موافَق عليه"), CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var rejected = await probe.LeaveRequests.FindAsync(requestId);
        Assert.Equal(HrRequestStatus.Rejected, rejected!.Status);

        var balance = await probe.LeaveBalances.SingleAsync(b => b.EmployeeId == employeeId);
        Assert.Equal(0m, balance.Pending);
        Assert.Equal(0m, balance.Used);
        Assert.False(await probe.LeaveBalanceHistories.AnyAsync(h => h.LeaveBalanceId == balance.Id)); // قاعدة 26 — Pending بدون History
    }

    // --------------------------------------------------------------------------------- LeaveDaysCalculator

    [Fact]
    public async Task Leave_days_calculator_counts_calendar_days_inclusive_by_default()
    {
        var companyId = NewCompanyId();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var employeeId = await AddEmployeeAsync(db, companyId);

        var result = await LeaveDaysCalculator.CalculateAsync(db, employeeId, companyId, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9), CancellationToken.None);
        Assert.Equal(5m, result.Days);
        Assert.Equal(LeaveDayCountingMode.Calendar, result.Mode);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public async Task Leave_days_calculator_excludes_rest_days_in_working_days_mode()
    {
        var companyId = NewCompanyId();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var employeeId = await AddEmployeeAsync(db, companyId);
        db.HrSettingsRows.Add(new HrSettings { CompanyId = companyId, LeaveDayCountingMode = LeaveDayCountingMode.WorkingDays });
        db.ShiftSchedules.Add(new ShiftSchedule { CompanyId = companyId, BranchId = 1, EmployeeId = employeeId, StartDate = new DateOnly(2026, 10, 7), IsRestDay = true });
        await db.SaveChangesAsync();

        var result = await LeaveDaysCalculator.CalculateAsync(db, employeeId, companyId, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9), CancellationToken.None);
        Assert.Equal(4m, result.Days);
        Assert.Equal(LeaveDayCountingMode.WorkingDays, result.Mode);
        Assert.False(result.UsedFallback);
    }

    [Fact]
    public async Task Leave_days_calculator_falls_back_to_calendar_when_the_employee_has_no_schedule_at_all()
    {
        var companyId = NewCompanyId();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var employeeId = await AddEmployeeAsync(db, companyId);
        db.HrSettingsRows.Add(new HrSettings { CompanyId = companyId, LeaveDayCountingMode = LeaveDayCountingMode.WorkingDays });
        await db.SaveChangesAsync();

        var result = await LeaveDaysCalculator.CalculateAsync(db, employeeId, companyId, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 9), CancellationToken.None);
        Assert.Equal(5m, result.Days);
        Assert.Equal(LeaveDayCountingMode.Calendar, result.Mode);
        Assert.True(result.UsedFallback);
    }

    // --------------------------------------------------------------------------------- Remarks8 Amendments

    [Fact]
    public async Task A_single_shift_schedule_range_row_covers_every_day_inside_it()
    {
        var companyId = NewCompanyId();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var employeeId = await AddEmployeeAsync(db, companyId);
        var workShift = new WorkShiftDefinition { CompanyId = companyId, Code = Unique("WS"), NameAr = "صباحي", NameEn = "Morning", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0) };
        db.WorkShiftDefinitions.Add(workShift);
        await db.SaveChangesAsync();

        db.ShiftSchedules.Add(new ShiftSchedule
        {
            CompanyId = companyId, BranchId = 1, EmployeeId = employeeId,
            StartDate = new DateOnly(2026, 10, 4), EndDate = new DateOnly(2026, 10, 17), // أسبوعين (Remarks8 Item 5)
            WorkShiftDefinitionId = workShift.Id, IsRestDay = false
        });
        await db.SaveChangesAsync();

        var midway = await ShiftScheduleLookup.FindForDateAsync(db, employeeId, new DateOnly(2026, 10, 10), CancellationToken.None);
        Assert.NotNull(midway);
        Assert.Equal(workShift.Id, midway!.WorkShiftDefinitionId);

        var outside = await ShiftScheduleLookup.FindForDateAsync(db, employeeId, new DateOnly(2026, 10, 18), CancellationToken.None);
        Assert.Null(outside);
    }

    [Fact]
    public async Task Creating_an_overlapping_shift_schedule_range_is_rejected()
    {
        var companyId = NewCompanyId();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var employeeId = await AddEmployeeAsync(db, companyId);

        var createHandler = new CreateShiftScheduleCommandHandler(db);
        await createHandler.Handle(new CreateShiftScheduleCommand(employeeId, new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 10), null, false), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => createHandler.Handle(
            new CreateShiftScheduleCommand(employeeId, new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 14), null, false), CancellationToken.None));
        Assert.Equal("HR-SHIFT-SCHEDULE-OVERLAPS", ex.Code);
    }

    [Fact]
    public async Task Holiday_range_marks_every_day_inside_it_as_a_holiday_in_attendance()
    {
        var companyId = NewCompanyId();
        long employeeId;
        var holidayStart = new DateOnly(2026, 10, 20);
        var holidayEnd = new DateOnly(2026, 10, 22);
        var middleDay = new DateOnly(2026, 10, 21);
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            employeeId = await AddEmployeeAsync(db, companyId);
            db.Holidays.Add(new Holiday
            {
                CompanyId = companyId, Code = Unique("HOL"), NameAr = "عيد", NameEn = "Holiday", IsActive = true,
                StartDate = holidayStart, EndDate = holidayEnd, Year = holidayStart.Year, IsNational = true
            });
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var handler = new RecomputeDailyAttendanceCommandHandler(db);
            await handler.Handle(new RecomputeDailyAttendanceCommand(employeeId, middleDay), CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var attendance = await probe.Attendances.SingleAsync(a => a.EmployeeId == employeeId && a.WorkDate == middleDay);
        Assert.Equal(AttendanceStatus.Holiday, attendance.Status);
    }

    [Fact]
    public async Task Bulk_generator_applies_the_weekly_rest_mask_and_respects_per_employee_exceptions()
    {
        var companyId = NewCompanyId();
        long orgUnitId, employeeWithDefaultsId, employeeWithExceptionId, workShiftId, exceptionShiftId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var orgUnit = new OrgUnit { CompanyId = companyId, Code = Unique("OU"), NameAr = "فرع", NameEn = "Branch" };
            db.OrgUnits.Add(orgUnit);
            var grade = new JobGrade { CompanyId = companyId, Code = Unique("JG"), NameAr = "أ", NameEn = "A", Level = 1 };
            db.JobGrades.Add(grade);
            await db.SaveChangesAsync();
            orgUnitId = orgUnit.Id;

            var jobPosition = new JobPosition { CompanyId = companyId, Code = Unique("JP"), NameAr = "كاشير", NameEn = "Cashier", OrgUnitId = orgUnitId };
            db.JobPositions.Add(jobPosition);
            var morningShift = new WorkShiftDefinition { CompanyId = companyId, Code = Unique("WS"), NameAr = "صباحي", NameEn = "Morning", StartTime = new TimeOnly(8, 0), EndTime = new TimeOnly(16, 0) };
            var eveningShift = new WorkShiftDefinition { CompanyId = companyId, Code = Unique("WS"), NameAr = "مسائي", NameEn = "Evening", StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(0, 0) };
            db.WorkShiftDefinitions.AddRange(morningShift, eveningShift);
            await db.SaveChangesAsync();
            workShiftId = morningShift.Id;
            exceptionShiftId = eveningShift.Id;

            var e1 = new Employee { CompanyId = companyId, BranchId = 1, Code = Unique("E"), NameAr = "موظف1", NameEn = "E1", OrgUnitId = orgUnitId, JobPositionId = jobPosition.Id, JobGradeId = grade.Id, HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime, Status = EmployeeStatus.Active };
            var e2 = new Employee { CompanyId = companyId, BranchId = 1, Code = Unique("E"), NameAr = "موظف2", NameEn = "E2", OrgUnitId = orgUnitId, JobPositionId = jobPosition.Id, JobGradeId = grade.Id, HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime, Status = EmployeeStatus.Active };
            db.Employees.AddRange(e1, e2);
            await db.SaveChangesAsync();
            employeeWithDefaultsId = e1.Id;
            employeeWithExceptionId = e2.Id;
        }

        // أسبوع كامل: الأحد 2026-10-04 -> السبت 2026-10-10. الافتراضي: الجمعة راحة بس (Mask = 1<<5 = 32).
        var fromDate = new DateOnly(2026, 10, 4);
        var toDate = new DateOnly(2026, 10, 10);
        const int fridayOnlyMask = 1 << (int)DayOfWeek.Friday;
        const int fridaySaturdayMask = fridayOnlyMask | (1 << (int)DayOfWeek.Saturday);

        BulkGenerateResultDto result;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var handler = new GenerateBulkShiftSchedulesCommandHandler(db, new TestCurrentCompanyContext(companyId));
            result = await handler.Handle(new GenerateBulkShiftSchedulesCommand(
                orgUnitId, fromDate, toDate, workShiftId, fridayOnlyMask,
                [new BulkScheduleExceptionInput(employeeWithExceptionId, exceptionShiftId, fridaySaturdayMask)]), CancellationToken.None);
        }

        Assert.Equal(2, result.EmployeesProcessed);
        Assert.Equal(14, result.DaysCreated); // 7 أيام × موظفين

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var defaultsSchedules = await probe.ShiftSchedules.Where(s => s.EmployeeId == employeeWithDefaultsId).ToListAsync();
        Assert.Equal(6, defaultsSchedules.Count(s => !s.IsRestDay && s.WorkShiftDefinitionId == workShiftId));
        Assert.Single(defaultsSchedules, s => s.IsRestDay); // الجمعة بس

        var exceptionSchedules = await probe.ShiftSchedules.Where(s => s.EmployeeId == employeeWithExceptionId).ToListAsync();
        Assert.Equal(5, exceptionSchedules.Count(s => !s.IsRestDay && s.WorkShiftDefinitionId == exceptionShiftId));
        Assert.Equal(2, exceptionSchedules.Count(s => s.IsRestDay)); // الجمعة والسبت

        var pattern = await probe.EmployeeWeeklyRestDays.SingleAsync(w => w.EmployeeId == employeeWithExceptionId);
        Assert.Equal(fridaySaturdayMask, pattern.WeeklyRestDaysMask);
    }

    [Fact]
    public async Task Bulk_generator_is_idempotent_and_skips_days_that_already_have_a_schedule()
    {
        var companyId = NewCompanyId();
        long orgUnitId, employeeId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var orgUnit = new OrgUnit { CompanyId = companyId, Code = Unique("OU"), NameAr = "فرع", NameEn = "Branch" };
            db.OrgUnits.Add(orgUnit);
            var grade = new JobGrade { CompanyId = companyId, Code = Unique("JG"), NameAr = "أ", NameEn = "A", Level = 1 };
            db.JobGrades.Add(grade);
            await db.SaveChangesAsync();
            orgUnitId = orgUnit.Id;

            var jobPosition = new JobPosition { CompanyId = companyId, Code = Unique("JP"), NameAr = "كاشير", NameEn = "Cashier", OrgUnitId = orgUnitId };
            db.JobPositions.Add(jobPosition);
            await db.SaveChangesAsync();

            var employee = new Employee { CompanyId = companyId, BranchId = 1, Code = Unique("E"), NameAr = "موظف", NameEn = "Employee", OrgUnitId = orgUnitId, JobPositionId = jobPosition.Id, JobGradeId = grade.Id, HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime, Status = EmployeeStatus.Active };
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            employeeId = employee.Id;
        }

        var fromDate = new DateOnly(2026, 11, 1);
        var toDate = new DateOnly(2026, 11, 7);

        for (var i = 0; i < 2; i++)
        {
            await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
            var handler = new GenerateBulkShiftSchedulesCommandHandler(db, new TestCurrentCompanyContext(companyId));
            await handler.Handle(new GenerateBulkShiftSchedulesCommand(orgUnitId, fromDate, toDate, null, 0, null), CancellationToken.None);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        Assert.Equal(7, await probe.ShiftSchedules.CountAsync(s => s.EmployeeId == employeeId)); // مش 14
    }
}
