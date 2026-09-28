using Habbak.ERP.Application.Approvals;
using Habbak.ERP.Application.Approvals.Commands;
using Habbak.ERP.Application.Approvals.Outcomes;
using Habbak.ERP.Application.Approvals.Queries;
using Habbak.ERP.Application.Approvals.Services;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Approvals;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Notifications;
using Habbak.ERP.Domain.Settings;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests.Approvals;

/// <summary>
/// Docs/Modules/00-Project-Overview.md §12 (Phase 2) — the mandatory scenarios from
/// Docs/Implementation/HR-MASTER-PLAN.md's Phase 2 spec: Self-Approval Guard, ManagerId=null
/// escalation, Manual Fallback reassignment, an empty JobGrade's explicit configuration alert,
/// AnyOne vs. All step modes, workflow versioning after first use, a rejection reflecting on the
/// underlying entity, and the "بانتظار اعتمادي" query. Exercises the real command/query handlers
/// against a real migrated LocalDB (PostingServiceFixture), not mocks.
/// </summary>
public sealed class ApprovalWorkflowEngineTests(PostingServiceFixture fixture) : IClassFixture<PostingServiceFixture>
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

    private async Task<long> AddScreenAsync(AppDbContext db, string? code = null)
    {
        var screen = new Screen { Code = code ?? Unique("SCR"), NameAr = "شاشة اختبار", NameEn = "Test Screen", ModuleCode = "TEST" };
        db.Screens.Add(screen);
        await db.SaveChangesAsync();
        return screen.Id;
    }

    private async Task<long> AddEmployeeAsync(AppDbContext db, long companyId, long? managerId = null, long? userId = null, long? jobGradeId = null)
    {
        var orgUnit = new OrgUnit { CompanyId = companyId, Code = Unique("OU"), NameAr = "إدارة", NameEn = "Dept" };
        db.OrgUnits.Add(orgUnit);
        await db.SaveChangesAsync();

        var jobGrade = jobGradeId is null ? null : await db.JobGrades.FindAsync(jobGradeId.Value);
        if (jobGradeId is null)
        {
            var grade = new JobGrade { CompanyId = companyId, Code = Unique("JG"), NameAr = "أ", NameEn = "A", Level = 1 };
            db.JobGrades.Add(grade);
            await db.SaveChangesAsync();
            jobGradeId = grade.Id;
        }

        var jobPosition = new JobPosition { CompanyId = companyId, Code = Unique("JP"), NameAr = "محاسب", NameEn = "Accountant", OrgUnitId = orgUnit.Id };
        db.JobPositions.Add(jobPosition);
        await db.SaveChangesAsync();

        var employee = new Employee
        {
            CompanyId = companyId, BranchId = 1, Code = Unique("E"), NameAr = "موظف", NameEn = "Employee",
            OrgUnitId = orgUnit.Id, JobPositionId = jobPosition.Id, JobGradeId = jobGradeId.Value, ManagerId = managerId, UserId = userId,
            HireDate = new DateOnly(2026, 1, 1), EmploymentType = EmploymentType.FullTime
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee.Id;
    }

    private async Task<long> AddJobGradeAsync(AppDbContext db, long companyId)
    {
        var grade = new JobGrade { CompanyId = companyId, Code = Unique("JG"), NameAr = "فاضية", NameEn = "Empty", Level = 9 };
        db.JobGrades.Add(grade);
        await db.SaveChangesAsync();
        return grade.Id;
    }

    private async Task<(long WorkflowId, long StepId)> AddSingleStepWorkflowAsync(
        AppDbContext db, long companyId, ApprovalApproverType approverType, long? approverReferenceId, ApprovalStepMode mode = ApprovalStepMode.AnyOne)
    {
        var workflow = new ApprovalWorkflow { CompanyId = companyId, Code = Unique("WF"), NameAr = "سلسلة اختبار", NameEn = "Test Workflow", IsActive = true };
        var step = new ApprovalWorkflowStep { StepOrder = 1, Mode = mode };
        step.Approvers.Add(new ApprovalStepApprover { ApproverType = approverType, ApproverReferenceId = approverReferenceId });
        workflow.Steps.Add(step);
        db.ApprovalWorkflows.Add(workflow);
        await db.SaveChangesAsync();
        return (workflow.Id, step.Id);
    }

    private async Task AssignAsync(AppDbContext db, long companyId, long screenId, long workflowId)
    {
        db.ApprovalWorkflowAssignments.Add(new ApprovalWorkflowAssignment { CompanyId = companyId, ScreenId = screenId, ApprovalWorkflowId = workflowId, IsActive = true });
        await db.SaveChangesAsync();
    }

    private async Task<long> StartInstanceAsync(long companyId, string entityType, long entityId, long requestedByUserId, decimal amount = 100)
    {
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: requestedByUserId));
        var service = new ApprovalWorkflowService(
            db, new ApprovalStepResolutionService(db),
            new Habbak.ERP.Application.Notifications.NotificationService(db, new TestCurrentCompanyContext(companyId, userId: requestedByUserId)));
        var id = await service.TryStartApprovalAsync(new ApprovalWorkflowTrigger
        {
            CompanyId = companyId, EntityType = entityType, EntityId = entityId, Amount = amount, RequestedByUserId = requestedByUserId
        });
        Assert.NotNull(id);
        return id!.Value;
    }

    // ---------------------------------------------------------------- Self-Approval Guard

    [Fact]
    public async Task Self_approval_is_forbidden_even_when_the_requester_is_the_only_eligible_approver()
    {
        var companyId = NewCompanyId();
        long managerUserId, employeeId, screenId, workflowId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            managerUserId = await AddUserAsync(db);
            employeeId = await AddEmployeeAsync(db, companyId, userId: managerUserId);
            screenId = await AddScreenAsync(db);
            (workflowId, _) = await AddSingleStepWorkflowAsync(db, companyId, ApprovalApproverType.SpecificEmployee, employeeId);
            await AssignAsync(db, companyId, screenId, workflowId);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var screenCode = (await probe.Screens.FindAsync(screenId))!.Code;

        var instanceId = await StartInstanceAsync(companyId, screenCode, entityId: 555, requestedByUserId: managerUserId);

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: managerUserId));
        var handler = new ApproveStepCommandHandler(db2, new TestCurrentCompanyContext(companyId, userId: managerUserId), new ApprovalStepResolutionService(db2), new Habbak.ERP.Application.Notifications.NotificationService(db2, new TestCurrentCompanyContext(companyId, userId: managerUserId)), []);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new ApproveStepCommand(instanceId, null), CancellationToken.None));
        Assert.Equal("APPROVAL-SELF-APPROVAL-FORBIDDEN", ex.Code);
    }

    // ---------------------------------------------------------------- ManagerId = null -> Company Default Approver

    [Fact]
    public async Task Null_manager_escalates_to_the_company_default_approver()
    {
        var companyId = NewCompanyId();
        long requesterUserId, defaultApproverUserId, screenId, workflowId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            requesterUserId = await AddUserAsync(db);
            defaultApproverUserId = await AddUserAsync(db);
            await AddEmployeeAsync(db, companyId, managerId: null, userId: requesterUserId);
            db.HrSettingsRows.Add(new HrSettings { CompanyId = companyId, CompanyDefaultApproverUserId = defaultApproverUserId });
            screenId = await AddScreenAsync(db);
            (workflowId, _) = await AddSingleStepWorkflowAsync(db, companyId, ApprovalApproverType.DirectManager, null);
            await AssignAsync(db, companyId, screenId, workflowId);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var screenCode = (await probe.Screens.FindAsync(screenId))!.Code;

        var instanceId = await StartInstanceAsync(companyId, screenCode, entityId: 1, requestedByUserId: requesterUserId);

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var instance = await db2.ApprovalInstances.FindAsync(instanceId);
        var step = await db2.ApprovalWorkflowSteps.FirstAsync(s => s.ApprovalWorkflowId == workflowId);
        var eligible = await new ApprovalStepResolutionService(db2).GetEligibleApproverUserIdsAsync(instance!, step, CancellationToken.None);

        Assert.Equal([defaultApproverUserId], eligible);
    }

    // ---------------------------------------------------------------- Manual Fallback / Reassign

    [Fact]
    public async Task Manual_reassign_overrides_the_current_step_for_a_manager_on_leave()
    {
        var companyId = NewCompanyId();
        long requesterUserId, managerUserId, standInUserId, adminUserId, screenId, workflowId, managerEmployeeId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            requesterUserId = await AddUserAsync(db);
            managerUserId = await AddUserAsync(db);
            standInUserId = await AddUserAsync(db);
            adminUserId = await AddUserAsync(db);
            managerEmployeeId = await AddEmployeeAsync(db, companyId, userId: managerUserId);
            await AddEmployeeAsync(db, companyId, managerId: managerEmployeeId, userId: requesterUserId);
            screenId = await AddScreenAsync(db);
            (workflowId, _) = await AddSingleStepWorkflowAsync(db, companyId, ApprovalApproverType.DirectManager, null);
            await AssignAsync(db, companyId, screenId, workflowId);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var screenCode = (await probe.Screens.FindAsync(screenId))!.Code;
        var instanceId = await StartInstanceAsync(companyId, screenCode, entityId: 2, requestedByUserId: requesterUserId);

        await using (var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: adminUserId)))
        {
            var reassignHandler = new ReassignInstanceCommandHandler(
                db2, new TestCurrentCompanyContext(companyId, userId: adminUserId), new ApprovalStepResolutionService(db2),
                new Habbak.ERP.Application.Notifications.NotificationService(db2, new TestCurrentCompanyContext(companyId, userId: adminUserId)));
            await reassignHandler.Handle(new ReassignInstanceCommand(instanceId, standInUserId, "المدير في إجازة"), CancellationToken.None);
        }

        await using var db3 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var instance = await db3.ApprovalInstances.FindAsync(instanceId);
        var step = await db3.ApprovalWorkflowSteps.FirstAsync(s => s.ApprovalWorkflowId == workflowId);
        var eligible = await new ApprovalStepResolutionService(db3).GetEligibleApproverUserIdsAsync(instance!, step, CancellationToken.None);

        Assert.Equal([standInUserId], eligible); // the original manager is no longer eligible while reassigned

        await using var db4 = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: standInUserId));
        var approveHandler = new ApproveStepCommandHandler(db4, new TestCurrentCompanyContext(companyId, userId: standInUserId), new ApprovalStepResolutionService(db4), new Habbak.ERP.Application.Notifications.NotificationService(db4, new TestCurrentCompanyContext(companyId, userId: standInUserId)), []);
        await approveHandler.Handle(new ApproveStepCommand(instanceId, null), CancellationToken.None);

        await using var db5 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        Assert.Equal(ApprovalInstanceStatus.Approved, (await db5.ApprovalInstances.FindAsync(instanceId))!.Status);
    }

    // ---------------------------------------------------------------- Empty JobGrade -> explicit config alert

    [Fact]
    public async Task Empty_job_grade_surfaces_an_explicit_error_instead_of_a_silent_skip()
    {
        var companyId = NewCompanyId();
        long requesterUserId, actingUserId, screenId, workflowId, emptyGradeId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            requesterUserId = await AddUserAsync(db);
            actingUserId = await AddUserAsync(db);
            await AddEmployeeAsync(db, companyId, userId: requesterUserId);
            emptyGradeId = await AddJobGradeAsync(db, companyId); // no employee assigned to it
            screenId = await AddScreenAsync(db);
            (workflowId, _) = await AddSingleStepWorkflowAsync(db, companyId, ApprovalApproverType.JobGrade, emptyGradeId);
            await AssignAsync(db, companyId, screenId, workflowId);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var screenCode = (await probe.Screens.FindAsync(screenId))!.Code;
        var instanceId = await StartInstanceAsync(companyId, screenCode, entityId: 3, requestedByUserId: requesterUserId);

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: actingUserId));
        var handler = new ApproveStepCommandHandler(db2, new TestCurrentCompanyContext(companyId, userId: actingUserId), new ApprovalStepResolutionService(db2), new Habbak.ERP.Application.Notifications.NotificationService(db2, new TestCurrentCompanyContext(companyId, userId: actingUserId)), []);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new ApproveStepCommand(instanceId, null), CancellationToken.None));
        Assert.Equal("APPROVAL-STEP-NO-ELIGIBLE-APPROVERS", ex.Code);
    }

    // ---------------------------------------------------------------- AnyOne vs. All

    [Fact]
    public async Task AnyOne_mode_is_satisfied_by_a_single_approval()
    {
        var companyId = NewCompanyId();
        long requesterUserId, userA, userB, employeeA, employeeB, screenId, workflowId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            requesterUserId = await AddUserAsync(db);
            userA = await AddUserAsync(db);
            userB = await AddUserAsync(db);
            employeeA = await AddEmployeeAsync(db, companyId, userId: userA);
            employeeB = await AddEmployeeAsync(db, companyId, userId: userB);
            await AddEmployeeAsync(db, companyId, userId: requesterUserId);

            var workflow = new ApprovalWorkflow { CompanyId = companyId, Code = Unique("WF"), NameAr = "AnyOne", NameEn = "AnyOne", IsActive = true };
            var step = new ApprovalWorkflowStep { StepOrder = 1, Mode = ApprovalStepMode.AnyOne };
            step.Approvers.Add(new ApprovalStepApprover { ApproverType = ApprovalApproverType.SpecificEmployee, ApproverReferenceId = employeeA });
            step.Approvers.Add(new ApprovalStepApprover { ApproverType = ApprovalApproverType.SpecificEmployee, ApproverReferenceId = employeeB });
            workflow.Steps.Add(step);
            db.ApprovalWorkflows.Add(workflow);
            await db.SaveChangesAsync();
            workflowId = workflow.Id;

            screenId = await AddScreenAsync(db);
            await AssignAsync(db, companyId, screenId, workflowId);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var screenCode = (await probe.Screens.FindAsync(screenId))!.Code;
        var instanceId = await StartInstanceAsync(companyId, screenCode, entityId: 4, requestedByUserId: requesterUserId);

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: userA));
        var handler = new ApproveStepCommandHandler(db2, new TestCurrentCompanyContext(companyId, userId: userA), new ApprovalStepResolutionService(db2), new Habbak.ERP.Application.Notifications.NotificationService(db2, new TestCurrentCompanyContext(companyId, userId: userA)), []);
        await handler.Handle(new ApproveStepCommand(instanceId, null), CancellationToken.None);

        await using var db3 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        Assert.Equal(ApprovalInstanceStatus.Approved, (await db3.ApprovalInstances.FindAsync(instanceId))!.Status);
    }

    [Fact]
    public async Task All_mode_requires_every_eligible_approver()
    {
        var companyId = NewCompanyId();
        long requesterUserId, userA, userB, employeeA, employeeB, screenId, workflowId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            requesterUserId = await AddUserAsync(db);
            userA = await AddUserAsync(db);
            userB = await AddUserAsync(db);
            employeeA = await AddEmployeeAsync(db, companyId, userId: userA);
            employeeB = await AddEmployeeAsync(db, companyId, userId: userB);
            await AddEmployeeAsync(db, companyId, userId: requesterUserId);

            var workflow = new ApprovalWorkflow { CompanyId = companyId, Code = Unique("WF"), NameAr = "All", NameEn = "All", IsActive = true };
            var step = new ApprovalWorkflowStep { StepOrder = 1, Mode = ApprovalStepMode.All };
            step.Approvers.Add(new ApprovalStepApprover { ApproverType = ApprovalApproverType.SpecificEmployee, ApproverReferenceId = employeeA });
            step.Approvers.Add(new ApprovalStepApprover { ApproverType = ApprovalApproverType.SpecificEmployee, ApproverReferenceId = employeeB });
            workflow.Steps.Add(step);
            db.ApprovalWorkflows.Add(workflow);
            await db.SaveChangesAsync();
            workflowId = workflow.Id;

            screenId = await AddScreenAsync(db);
            await AssignAsync(db, companyId, screenId, workflowId);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var screenCode = (await probe.Screens.FindAsync(screenId))!.Code;
        var instanceId = await StartInstanceAsync(companyId, screenCode, entityId: 5, requestedByUserId: requesterUserId);

        await using (var dbA = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: userA)))
        {
            var handlerA = new ApproveStepCommandHandler(dbA, new TestCurrentCompanyContext(companyId, userId: userA), new ApprovalStepResolutionService(dbA), new Habbak.ERP.Application.Notifications.NotificationService(dbA, new TestCurrentCompanyContext(companyId, userId: userA)), []);
            await handlerA.Handle(new ApproveStepCommand(instanceId, null), CancellationToken.None);
        }

        await using (var probe2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            Assert.Equal(ApprovalInstanceStatus.Pending, (await probe2.ApprovalInstances.FindAsync(instanceId))!.Status); // still waiting on userB
        }

        await using (var dbB = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: userB)))
        {
            var handlerB = new ApproveStepCommandHandler(dbB, new TestCurrentCompanyContext(companyId, userId: userB), new ApprovalStepResolutionService(dbB), new Habbak.ERP.Application.Notifications.NotificationService(dbB, new TestCurrentCompanyContext(companyId, userId: userB)), []);
            await handlerB.Handle(new ApproveStepCommand(instanceId, null), CancellationToken.None);
        }

        await using var final = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        Assert.Equal(ApprovalInstanceStatus.Approved, (await final.ApprovalInstances.FindAsync(instanceId))!.Status);
    }

    // ---------------------------------------------------------------- Workflow versioning

    [Fact]
    public async Task Updating_a_workflow_that_already_started_an_instance_creates_a_new_version()
    {
        var companyId = NewCompanyId();
        long requesterUserId, employeeId, screenId, workflowId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            requesterUserId = await AddUserAsync(db);
            employeeId = await AddEmployeeAsync(db, companyId, userId: requesterUserId);
            screenId = await AddScreenAsync(db);
            (workflowId, _) = await AddSingleStepWorkflowAsync(db, companyId, ApprovalApproverType.SpecificEmployee, employeeId);
            await AssignAsync(db, companyId, screenId, workflowId);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var screenCode = (await probe.Screens.FindAsync(screenId))!.Code;
        await StartInstanceAsync(companyId, screenCode, entityId: 6, requestedByUserId: requesterUserId);

        var definition = new ApprovalWorkflowDefinition("won't-change", "معدّل", "Edited",
            [new ApprovalWorkflowStepInput(1, ApprovalStepMode.AnyOne, [new ApprovalStepApproverInput(ApprovalApproverType.SpecificEmployee, employeeId)])]);

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var updateHandler = new UpdateApprovalWorkflowCommandHandler(db2);
        var result = await updateHandler.Handle(new UpdateApprovalWorkflowCommand(workflowId, definition with { Code = (await db2.ApprovalWorkflows.FindAsync(workflowId))!.Code }), CancellationToken.None);

        Assert.True(result.IsNewVersion);
        Assert.Equal(2, result.VersionNumber);
        Assert.NotEqual(workflowId, result.Id);

        await using var db3 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var original = await db3.ApprovalWorkflows.FindAsync(workflowId);
        Assert.False(original!.IsCurrentVersion);
    }

    [Fact]
    public async Task Updating_a_workflow_that_never_ran_edits_in_place()
    {
        var companyId = NewCompanyId();
        long employeeId, workflowId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var userId = await AddUserAsync(db);
            employeeId = await AddEmployeeAsync(db, companyId, userId: userId);
            (workflowId, _) = await AddSingleStepWorkflowAsync(db, companyId, ApprovalApproverType.SpecificEmployee, employeeId);
        }

        await using var db1 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var code = (await db1.ApprovalWorkflows.FindAsync(workflowId))!.Code;
        var definition = new ApprovalWorkflowDefinition(code, "معدّل بدون إصدار جديد", "Edited in place",
            [new ApprovalWorkflowStepInput(1, ApprovalStepMode.AnyOne, [new ApprovalStepApproverInput(ApprovalApproverType.SpecificEmployee, employeeId)])]);

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var updateHandler = new UpdateApprovalWorkflowCommandHandler(db2);
        var result = await updateHandler.Handle(new UpdateApprovalWorkflowCommand(workflowId, definition), CancellationToken.None);

        Assert.False(result.IsNewVersion);
        Assert.Equal(workflowId, result.Id);
        Assert.Equal(1, result.VersionNumber);
    }

    // ---------------------------------------------------------------- Rejection reflects on the entity

    [Fact]
    public async Task Rejecting_an_instance_flips_the_journal_entrys_own_status()
    {
        var companyId = NewCompanyId();
        long requesterUserId, actingUserId, employeeId, entryId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            requesterUserId = await AddUserAsync(db);
            actingUserId = await AddUserAsync(db);
            employeeId = await AddEmployeeAsync(db, companyId, userId: actingUserId);
            await AddEmployeeAsync(db, companyId, userId: requesterUserId);

            var entry = new JournalEntry
            {
                CompanyId = companyId, EntryNumber = Unique("JV"), EntryDate = new DateOnly(2026, 9, 27),
                Description = "test", TotalDebit = 100, TotalCredit = 100, Status = JournalEntryStatus.Draft
            };
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();
            entryId = entry.Id;
        }

        long workflowId, screenId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            (workflowId, _) = await AddSingleStepWorkflowAsync(db, companyId, ApprovalApproverType.SpecificEmployee, employeeId);
            // ApprovalTriggerScreenMap maps EntityType "JournalEntry" to this exact Screen code
            // (Application/Approvals/ApprovalTriggerScreenMap.cs) — using "JournalEntry" as the
            // trigger's EntityType directly is what PostingService itself does.
            screenId = await AddScreenAsync(db, "ACCOUNTING_JOURNAL_ENTRIES");
            await AssignAsync(db, companyId, screenId, workflowId);
        }

        var instanceId = await StartInstanceAsync(companyId, "JournalEntry", entryId, requestedByUserId: requesterUserId);

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: actingUserId));
        var rejectHandler = new RejectStepCommandHandler(
            db2, new TestCurrentCompanyContext(companyId, userId: actingUserId), new ApprovalStepResolutionService(db2),
            new Habbak.ERP.Application.Notifications.NotificationService(db2, new TestCurrentCompanyContext(companyId, userId: actingUserId)),
            [new JournalEntryApprovalOutcomeHandler(db2, new TestCurrentCompanyContext(companyId, userId: actingUserId))]);

        await rejectHandler.Handle(new RejectStepCommand(instanceId, "بيانات ناقصة"), CancellationToken.None);

        await using var db3 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        Assert.Equal(JournalEntryStatus.Rejected, (await db3.JournalEntries.FindAsync(entryId))!.Status);
        Assert.Equal(ApprovalInstanceStatus.Rejected, (await db3.ApprovalInstances.FindAsync(instanceId))!.Status);
    }

    // ---------------------------------------------------------------- My Pending Approvals

    [Fact]
    public async Task Pending_approvals_query_excludes_own_requests_and_ineligible_users()
    {
        var companyId = NewCompanyId();
        long requesterUserId, eligibleUserId, otherUserId, eligibleEmployeeId, screenId, workflowId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            requesterUserId = await AddUserAsync(db);
            eligibleUserId = await AddUserAsync(db);
            otherUserId = await AddUserAsync(db);
            eligibleEmployeeId = await AddEmployeeAsync(db, companyId, userId: eligibleUserId);
            await AddEmployeeAsync(db, companyId, userId: requesterUserId);
            await AddEmployeeAsync(db, companyId, userId: otherUserId);

            screenId = await AddScreenAsync(db);
            (workflowId, _) = await AddSingleStepWorkflowAsync(db, companyId, ApprovalApproverType.SpecificEmployee, eligibleEmployeeId);
            await AssignAsync(db, companyId, screenId, workflowId);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var screenCode = (await probe.Screens.FindAsync(screenId))!.Code;
        await StartInstanceAsync(companyId, screenCode, entityId: 7, requestedByUserId: requesterUserId);

        // requester's own request never shows in their own pending list (rule 7)
        await using (var dbRequester = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: requesterUserId)))
        {
            var handler = new GetMyPendingApprovalsQueryHandler(dbRequester, new TestCurrentCompanyContext(companyId, userId: requesterUserId), new ApprovalStepResolutionService(dbRequester));
            var result = await handler.Handle(new GetMyPendingApprovalsQuery(), CancellationToken.None);
            Assert.Empty(result);
        }

        // an unrelated user, not an eligible approver, sees nothing either
        await using (var dbOther = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: otherUserId)))
        {
            var handler = new GetMyPendingApprovalsQueryHandler(dbOther, new TestCurrentCompanyContext(companyId, userId: otherUserId), new ApprovalStepResolutionService(dbOther));
            var result = await handler.Handle(new GetMyPendingApprovalsQuery(), CancellationToken.None);
            Assert.Empty(result);
        }

        // the eligible approver sees exactly this one instance
        await using (var dbEligible = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: eligibleUserId)))
        {
            var handler = new GetMyPendingApprovalsQueryHandler(dbEligible, new TestCurrentCompanyContext(companyId, userId: eligibleUserId), new ApprovalStepResolutionService(dbEligible));
            var result = await handler.Handle(new GetMyPendingApprovalsQuery(), CancellationToken.None);
            Assert.Single(result);
            Assert.Equal(requesterUserId, result[0].RequestedByUserId);
        }
    }

    // ---------------------------------------------------------------- Phase 2.5: notification integration

    [Fact]
    public async Task Starting_an_instance_notifies_the_first_steps_eligible_approver()
    {
        var companyId = NewCompanyId();
        long requesterUserId, approverUserId, employeeId, screenId, workflowId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            requesterUserId = await AddUserAsync(db);
            approverUserId = await AddUserAsync(db);
            employeeId = await AddEmployeeAsync(db, companyId, userId: approverUserId);
            await AddEmployeeAsync(db, companyId, userId: requesterUserId);
            screenId = await AddScreenAsync(db);
            (workflowId, _) = await AddSingleStepWorkflowAsync(db, companyId, ApprovalApproverType.SpecificEmployee, employeeId);
            await AssignAsync(db, companyId, screenId, workflowId);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var screenCode = (await probe.Screens.FindAsync(screenId))!.Code;
        var instanceId = await StartInstanceAsync(companyId, screenCode, entityId: 8, requestedByUserId: requesterUserId);

        await using var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var notification = await db2.Notifications.SingleAsync(n => n.RecipientUserId == approverUserId);
        Assert.Equal(NotificationType.ApprovalPending, notification.Type);
        Assert.True(notification.RequiresAction);
        Assert.Equal("ApprovalInstance", notification.RelatedEntityType);
        Assert.Equal(instanceId, notification.RelatedEntityId);

        Assert.False(await db2.Notifications.AnyAsync(n => n.RecipientUserId == requesterUserId)); // requester isn't notified of their own request starting
    }

    [Fact]
    public async Task Final_approval_notifies_the_requester_but_an_intermediate_step_does_not()
    {
        var companyId = NewCompanyId();
        long requesterUserId, userA, userB, employeeA, employeeB, screenId, workflowId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            requesterUserId = await AddUserAsync(db);
            userA = await AddUserAsync(db);
            userB = await AddUserAsync(db);
            employeeA = await AddEmployeeAsync(db, companyId, userId: userA);
            employeeB = await AddEmployeeAsync(db, companyId, userId: userB);
            await AddEmployeeAsync(db, companyId, userId: requesterUserId);

            var workflow = new ApprovalWorkflow { CompanyId = companyId, Code = Unique("WF"), NameAr = "خطوتين", NameEn = "TwoSteps", IsActive = true };
            workflow.Steps.Add(new ApprovalWorkflowStep
            {
                StepOrder = 1, Mode = ApprovalStepMode.AnyOne,
                Approvers = { new ApprovalStepApprover { ApproverType = ApprovalApproverType.SpecificEmployee, ApproverReferenceId = employeeA } }
            });
            workflow.Steps.Add(new ApprovalWorkflowStep
            {
                StepOrder = 2, Mode = ApprovalStepMode.AnyOne,
                Approvers = { new ApprovalStepApprover { ApproverType = ApprovalApproverType.SpecificEmployee, ApproverReferenceId = employeeB } }
            });
            db.ApprovalWorkflows.Add(workflow);
            await db.SaveChangesAsync();
            workflowId = workflow.Id;

            screenId = await AddScreenAsync(db);
            await AssignAsync(db, companyId, screenId, workflowId);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var screenCode = (await probe.Screens.FindAsync(screenId))!.Code;
        var instanceId = await StartInstanceAsync(companyId, screenCode, entityId: 9, requestedByUserId: requesterUserId);

        await using (var dbA = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: userA)))
        {
            var handlerA = new ApproveStepCommandHandler(
                dbA, new TestCurrentCompanyContext(companyId, userId: userA), new ApprovalStepResolutionService(dbA),
                new Habbak.ERP.Application.Notifications.NotificationService(dbA, new TestCurrentCompanyContext(companyId, userId: userA)), []);
            await handlerA.Handle(new ApproveStepCommand(instanceId, null), CancellationToken.None);
        }

        await using (var probe2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            // step 1 done, step 2 pending — the requester must NOT see "approved" yet (§1.6.2 decision)
            Assert.False(await probe2.Notifications.AnyAsync(n => n.RecipientUserId == requesterUserId && n.Type == NotificationType.ApprovalApproved));
            Assert.True(await probe2.Notifications.AnyAsync(n => n.RecipientUserId == userB && n.Type == NotificationType.ApprovalPending));
        }

        await using (var dbB = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: userB)))
        {
            var handlerB = new ApproveStepCommandHandler(
                dbB, new TestCurrentCompanyContext(companyId, userId: userB), new ApprovalStepResolutionService(dbB),
                new Habbak.ERP.Application.Notifications.NotificationService(dbB, new TestCurrentCompanyContext(companyId, userId: userB)), []);
            await handlerB.Handle(new ApproveStepCommand(instanceId, null), CancellationToken.None);
        }

        await using var final = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        Assert.True(await final.Notifications.AnyAsync(n => n.RecipientUserId == requesterUserId && n.Type == NotificationType.ApprovalApproved));
    }

    [Fact]
    public async Task Reassigning_notifies_the_old_approver_and_the_new_one()
    {
        var companyId = NewCompanyId();
        long requesterUserId, managerUserId, standInUserId, adminUserId, screenId, workflowId, managerEmployeeId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            requesterUserId = await AddUserAsync(db);
            managerUserId = await AddUserAsync(db);
            standInUserId = await AddUserAsync(db);
            adminUserId = await AddUserAsync(db);
            managerEmployeeId = await AddEmployeeAsync(db, companyId, userId: managerUserId);
            await AddEmployeeAsync(db, companyId, managerId: managerEmployeeId, userId: requesterUserId);
            screenId = await AddScreenAsync(db);
            (workflowId, _) = await AddSingleStepWorkflowAsync(db, companyId, ApprovalApproverType.DirectManager, null);
            await AssignAsync(db, companyId, screenId, workflowId);
        }

        await using var probe = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var screenCode = (await probe.Screens.FindAsync(screenId))!.Code;
        var instanceId = await StartInstanceAsync(companyId, screenCode, entityId: 10, requestedByUserId: requesterUserId);

        await using (var db2 = fixture.CreateContext(new TestCurrentCompanyContext(companyId, userId: adminUserId)))
        {
            var reassignHandler = new ReassignInstanceCommandHandler(
                db2, new TestCurrentCompanyContext(companyId, userId: adminUserId), new ApprovalStepResolutionService(db2),
                new Habbak.ERP.Application.Notifications.NotificationService(db2, new TestCurrentCompanyContext(companyId, userId: adminUserId)));
            await reassignHandler.Handle(new ReassignInstanceCommand(instanceId, standInUserId, "المدير في إجازة"), CancellationToken.None);
        }

        await using var db3 = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        Assert.True(await db3.Notifications.AnyAsync(n => n.RecipientUserId == managerUserId && n.Type == NotificationType.ApprovalReassigned));
        Assert.True(await db3.Notifications.AnyAsync(n => n.RecipientUserId == standInUserId && n.Type == NotificationType.ApprovalPending));
    }
}
