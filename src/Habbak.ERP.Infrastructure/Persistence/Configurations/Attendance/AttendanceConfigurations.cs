using Habbak.ERP.Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Attendance;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 3, Sub-Batch 3.2 — 10 كيانات، ملف واحد (نفس نمط
// HrConfigurations.cs). BranchId إلزامي بالـDB (نمط EmploymentContract) لكل كيانات IBranchScopedEntity
// عدا Holiday (اختياري فعلًا — Phase-3-Research.md، LeaveEntities.cs doc). ApprovalInstanceId على
// LeaveRequest/OvertimeRequest بدون FK (نفس نمط EmploymentContract.ApprovalInstanceId).

public class WorkShiftDefinitionConfiguration : IEntityTypeConfiguration<WorkShiftDefinition>
{
    public void Configure(EntityTypeBuilder<WorkShiftDefinition> builder)
    {
        builder.ToTable("WorkShiftDefinitions");
        builder.Property(w => w.Code).IsRequired().HasMaxLength(50);
        builder.Property(w => w.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(w => w.NameEn).IsRequired().HasMaxLength(200);

        builder.HasIndex(w => new { w.CompanyId, w.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ShiftScheduleConfiguration : IEntityTypeConfiguration<ShiftSchedule>
{
    public void Configure(EntityTypeBuilder<ShiftSchedule> builder)
    {
        builder.ToTable("ShiftSchedules");
        builder.Property(s => s.BranchId).IsRequired();

        builder.HasOne(s => s.Employee).WithMany().HasForeignKey(s => s.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.WorkShiftDefinition).WithMany().HasForeignKey(s => s.WorkShiftDefinitionId).OnDelete(DeleteBehavior.Restrict);

        // Remarks8 Item 5 — StartDate/EndDate بقت فترة، مش يوم واحد فريد؛ التحقق من التداخل بقى
        // مسؤولية الـ Command (CreateShiftScheduleCommand)، مش قيد DB (فهرس فريد على يوم واحد
        // مايشتغلش صح مع فترات متراكبة). Index عادي بس للأداء.
        builder.HasIndex(s => new { s.EmployeeId, s.StartDate });
    }
}

public class EmployeeWeeklyRestDaysConfiguration : IEntityTypeConfiguration<EmployeeWeeklyRestDays>
{
    public void Configure(EntityTypeBuilder<EmployeeWeeklyRestDays> builder)
    {
        builder.ToTable("EmployeeWeeklyRestDays");

        builder.HasOne(w => w.Employee).WithMany().HasForeignKey(w => w.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => new { w.EmployeeId, w.EffectiveFrom });
    }
}

public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("TimeEntries");
        builder.Property(t => t.BranchId).IsRequired();

        builder.HasOne(t => t.Employee).WithMany().HasForeignKey(t => t.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.POSShift).WithMany().HasForeignKey(t => t.POSShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.CorrectsTimeEntry).WithMany().HasForeignKey(t => t.CorrectsTimeEntryId).OnDelete(DeleteBehavior.Restrict);
        // Phase 3B — FK الناقص المؤجَّل من Phase 3 (Phase-3B-Research.md §1.1).
        builder.HasOne(t => t.Device).WithMany().HasForeignKey(t => t.DeviceId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.EmployeeId, t.TimestampUtc });
        // Idempotency check قبل إنشاء صف مقترح جديد من POS.Shift (Phase-3-Research.md §3.3).
        builder.HasIndex(t => new { t.POSShiftId, t.EntryType });
    }
}

public class AttendanceConfiguration : IEntityTypeConfiguration<Habbak.ERP.Domain.Attendance.Attendance>
{
    public void Configure(EntityTypeBuilder<Habbak.ERP.Domain.Attendance.Attendance> builder)
    {
        builder.ToTable("Attendances");
        builder.Property(a => a.BranchId).IsRequired();

        builder.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ShiftSchedule).WithMany().HasForeignKey(a => a.ShiftScheduleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.EmployeeId, a.WorkDate }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("LeaveTypes");
        builder.Property(l => l.Code).IsRequired().HasMaxLength(50);
        builder.Property(l => l.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(l => l.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(l => l.PaidPercentage).HasPrecision(5, 2);

        builder.HasOne(l => l.DeductFromLeaveType).WithMany().HasForeignKey(l => l.DeductFromLeaveTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.CompanyId, l.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class LeaveBalanceConfiguration : IEntityTypeConfiguration<LeaveBalance>
{
    public void Configure(EntityTypeBuilder<LeaveBalance> builder)
    {
        builder.ToTable("LeaveBalances");
        builder.Property(b => b.AccruedThisYear).HasPrecision(9, 2);
        builder.Property(b => b.CarriedOver).HasPrecision(9, 2);
        builder.Property(b => b.Used).HasPrecision(9, 2);
        builder.Property(b => b.Pending).HasPrecision(9, 2);
        builder.Ignore(b => b.Available);

        builder.HasOne(b => b.Employee).WithMany().HasForeignKey(b => b.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(b => b.LeaveType).WithMany().HasForeignKey(b => b.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.EmployeeId, b.LeaveTypeId, b.Year }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class LeaveBalanceHistoryConfiguration : IEntityTypeConfiguration<LeaveBalanceHistory>
{
    public void Configure(EntityTypeBuilder<LeaveBalanceHistory> builder)
    {
        builder.ToTable("LeaveBalanceHistories");
        builder.Property(h => h.Days).HasPrecision(9, 2);
        builder.Property(h => h.SourceType).HasMaxLength(100);
        builder.Property(h => h.Reason).HasMaxLength(500);

        builder.HasOne(h => h.LeaveBalance).WithMany().HasForeignKey(h => h.LeaveBalanceId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => h.LeaveBalanceId);
    }
}

public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests");
        builder.Property(r => r.BranchId).IsRequired();
        builder.Property(r => r.Days).HasPrecision(9, 2);
        builder.Property(r => r.Reason).HasMaxLength(500);

        builder.HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.LeaveType).WithMany().HasForeignKey(r => r.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.EmployeeId);
    }
}

public class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.ToTable("Holidays");
        builder.Property(h => h.Code).IsRequired().HasMaxLength(50);
        builder.Property(h => h.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(h => h.NameEn).IsRequired().HasMaxLength(200);
        // BranchId فعلًا اختياري هنا (null = كل الفروع) — عكس نمط EmploymentContract/ShiftSchedule.

        builder.HasIndex(h => new { h.CompanyId, h.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(h => new { h.BranchId, h.StartDate });
    }
}

public class OvertimeRequestConfiguration : IEntityTypeConfiguration<OvertimeRequest>
{
    public void Configure(EntityTypeBuilder<OvertimeRequest> builder)
    {
        builder.ToTable("OvertimeRequests");
        builder.Property(o => o.BranchId).IsRequired();
        builder.Property(o => o.Reason).HasMaxLength(500);

        builder.HasOne(o => o.Employee).WithMany().HasForeignKey(o => o.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(o => o.EmployeeId);
    }
}
