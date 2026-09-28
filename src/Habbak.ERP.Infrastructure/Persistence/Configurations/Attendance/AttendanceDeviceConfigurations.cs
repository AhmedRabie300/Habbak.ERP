using Habbak.ERP.Domain.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Attendance;

// Docs/Implementation/Phase-3B-Research.md §6, Sub-Batch 3B.2 — 4 كيانات، ملف واحد (نفس نمط
// AttendanceConfigurations.cs).

public class AttendanceDeviceConfiguration : IEntityTypeConfiguration<AttendanceDevice>
{
    public void Configure(EntityTypeBuilder<AttendanceDevice> builder)
    {
        builder.ToTable("AttendanceDevices");
        builder.Property(d => d.Code).IsRequired().HasMaxLength(50);
        builder.Property(d => d.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(d => d.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Model).HasMaxLength(100);
        builder.Property(d => d.SerialNumber).HasMaxLength(100);
        builder.Property(d => d.DeviceSecretHash).IsRequired().HasMaxLength(500);

        builder.HasIndex(d => new { d.CompanyId, d.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        // مفتاح المطابقة وقت الـPush (§7 قرار 1) — لازم يبقى فريد على مستوى الـDB كله (مفيش
        // CompanyId في الـfilter، لأن جهاز فعلي مش بيبعت CompanyId في بروتوكوله).
        builder.HasIndex(d => d.SerialNumber).IsUnique().HasFilter("[IsDeleted] = 0 AND [SerialNumber] IS NOT NULL");
    }
}

public class AttendanceDeviceLogConfiguration : IEntityTypeConfiguration<AttendanceDeviceLog>
{
    public void Configure(EntityTypeBuilder<AttendanceDeviceLog> builder)
    {
        builder.ToTable("AttendanceDeviceLogs");
        builder.Property(l => l.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(l => l.AttendanceDevice).WithMany().HasForeignKey(l => l.AttendanceDeviceId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.AttendanceDeviceId, l.StartedAtUtc });
    }
}

public class EmployeeDeviceMappingConfiguration : IEntityTypeConfiguration<EmployeeDeviceMapping>
{
    public void Configure(EntityTypeBuilder<EmployeeDeviceMapping> builder)
    {
        builder.ToTable("EmployeeDeviceMappings");
        builder.Property(m => m.DeviceUserId).IsRequired().HasMaxLength(50);

        builder.HasOne(m => m.Employee).WithMany().HasForeignKey(m => m.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.AttendanceDevice).WithMany().HasForeignKey(m => m.AttendanceDeviceId).OnDelete(DeleteBehavior.Restrict);

        // §3.0 — نفس رقم المستخدم على نفس الجهاز ميتكررش لموظفين مختلفين.
        builder.HasIndex(m => new { m.AttendanceDeviceId, m.DeviceUserId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(m => m.EmployeeId);
    }
}

public class RawPunchConfiguration : IEntityTypeConfiguration<RawPunch>
{
    public void Configure(EntityTypeBuilder<RawPunch> builder)
    {
        builder.ToTable("RawPunches");
        builder.Property(p => p.DeviceUserId).IsRequired().HasMaxLength(50);

        builder.HasOne(p => p.AttendanceDevice).WithMany().HasForeignKey(p => p.AttendanceDeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.ResultTimeEntry).WithMany().HasForeignKey(p => p.ResultTimeEntryId).OnDelete(DeleteBehavior.Restrict);

        // §3.1 — Deduplication عند الإدخال نفسه، مش بعد المعالجة.
        builder.HasIndex(p => new { p.AttendanceDeviceId, p.DeviceUserId, p.PunchTimestampUtc }).IsUnique().HasFilter("[IsDeleted] = 0");
        // AttendanceDeviceJobs بيجيب كل صف Pending مرتب بالوقت (§4 بند 1).
        builder.HasIndex(p => p.ProcessingStatus);
    }
}
