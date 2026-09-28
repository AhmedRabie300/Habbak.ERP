using Habbak.ERP.Domain.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.HR;

// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B1 — the 9 HR lookups. JobGrade/JobPosition/
// OrgUnit/EmployeeDocumentType are company-scoped (unique on CompanyId+Code, matching every other
// per-company lookup in the system); RelationshipType/MilitaryStatus/QualificationType/
// TerminationReason/InsuranceOffice are system-wide (unique on Code alone, matching Currency/Country)
// since they are objective/government-defined categories, not each company's own choices.

public class JobGradeConfiguration : IEntityTypeConfiguration<JobGrade>
{
    public void Configure(EntityTypeBuilder<JobGrade> builder)
    {
        builder.ToTable("JobGrades");
        builder.Property(g => g.Code).IsRequired().HasMaxLength(50);
        builder.Property(g => g.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(g => g.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(g => g.MinSalary).HasPrecision(18, 4);
        builder.Property(g => g.MaxSalary).HasPrecision(18, 4);

        builder.HasIndex(g => new { g.CompanyId, g.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class JobPositionConfiguration : IEntityTypeConfiguration<JobPosition>
{
    public void Configure(EntityTypeBuilder<JobPosition> builder)
    {
        builder.ToTable("JobPositions");
        builder.Property(p => p.Code).IsRequired().HasMaxLength(50);
        builder.Property(p => p.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(p => p.NameEn).IsRequired().HasMaxLength(200);

        builder.HasOne(p => p.OrgUnit).WithMany().HasForeignKey(p => p.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.DefaultJobGrade).WithMany().HasForeignKey(p => p.DefaultJobGradeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.CompanyId, p.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class OrgUnitConfiguration : IEntityTypeConfiguration<OrgUnit>
{
    public void Configure(EntityTypeBuilder<OrgUnit> builder)
    {
        builder.ToTable("OrgUnits");
        builder.Property(u => u.Code).IsRequired().HasMaxLength(50);
        builder.Property(u => u.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(u => u.NameEn).IsRequired().HasMaxLength(200);

        builder.HasOne(u => u.Parent).WithMany().HasForeignKey(u => u.ParentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(u => u.Branch).WithMany().HasForeignKey(u => u.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(u => u.CostCenterDimensionValue).WithMany().HasForeignKey(u => u.CostCenterDimensionValueId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => new { u.CompanyId, u.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class EmployeeDocumentTypeConfiguration : IEntityTypeConfiguration<EmployeeDocumentType>
{
    public void Configure(EntityTypeBuilder<EmployeeDocumentType> builder)
    {
        builder.ToTable("EmployeeDocumentTypes");
        builder.Property(t => t.Code).IsRequired().HasMaxLength(50);
        builder.Property(t => t.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(t => t.NameEn).IsRequired().HasMaxLength(200);

        builder.HasIndex(t => new { t.CompanyId, t.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class RelationshipTypeConfiguration : IEntityTypeConfiguration<RelationshipType>
{
    public void Configure(EntityTypeBuilder<RelationshipType> builder)
    {
        builder.ToTable("RelationshipTypes");
        builder.Property(r => r.Code).IsRequired().HasMaxLength(50);
        builder.Property(r => r.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(r => r.NameEn).IsRequired().HasMaxLength(200);

        builder.HasIndex(r => r.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class MilitaryStatusConfiguration : IEntityTypeConfiguration<MilitaryStatus>
{
    public void Configure(EntityTypeBuilder<MilitaryStatus> builder)
    {
        builder.ToTable("MilitaryStatuses");
        builder.Property(m => m.Code).IsRequired().HasMaxLength(50);
        builder.Property(m => m.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(m => m.NameEn).IsRequired().HasMaxLength(200);

        builder.HasIndex(m => m.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class QualificationTypeConfiguration : IEntityTypeConfiguration<QualificationType>
{
    public void Configure(EntityTypeBuilder<QualificationType> builder)
    {
        builder.ToTable("QualificationTypes");
        builder.Property(q => q.Code).IsRequired().HasMaxLength(50);
        builder.Property(q => q.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(q => q.NameEn).IsRequired().HasMaxLength(200);

        builder.HasIndex(q => q.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class TerminationReasonConfiguration : IEntityTypeConfiguration<TerminationReason>
{
    public void Configure(EntityTypeBuilder<TerminationReason> builder)
    {
        builder.ToTable("TerminationReasons");
        builder.Property(t => t.Code).IsRequired().HasMaxLength(50);
        builder.Property(t => t.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(t => t.NameEn).IsRequired().HasMaxLength(200);

        builder.HasIndex(t => t.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class InsuranceOfficeConfiguration : IEntityTypeConfiguration<InsuranceOffice>
{
    public void Configure(EntityTypeBuilder<InsuranceOffice> builder)
    {
        builder.ToTable("InsuranceOffices");
        builder.Property(o => o.Code).IsRequired().HasMaxLength(50);
        builder.Property(o => o.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(o => o.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(o => o.OfficialCode).HasMaxLength(50);
        builder.Property(o => o.Address).HasMaxLength(500);

        builder.HasIndex(o => o.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B3 — the three Employee "followers". All three are
// IEmployeeScopedEntity (first real use of that filter) and IBranchScopedEntity, same
// required-at-DB/nullable-in-C# BranchId convention as Employee itself. Every FK to Employee/its own
// lookups uses Restrict, matching every other HR configuration — a Contract/Document/Certification
// blocks deleting the Employee it belongs to, it doesn't cascade away silently.

public class EmploymentContractConfiguration : IEntityTypeConfiguration<EmploymentContract>
{
    public void Configure(EntityTypeBuilder<EmploymentContract> builder)
    {
        builder.ToTable("EmploymentContracts");
        builder.Property(c => c.BranchId).IsRequired();
        builder.Property(c => c.BasicSalary).HasPrecision(18, 4);
        builder.Property(c => c.InsurableWage).HasPrecision(18, 4);

        builder.HasOne(c => c.Employee).WithMany().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.PreviousContract).WithMany().HasForeignKey(c => c.PreviousContractId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Attachment).WithMany().HasForeignKey(c => c.AttachmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.EmployeeId);
    }
}

public class EmploymentContractLineConfiguration : IEntityTypeConfiguration<EmploymentContractLine>
{
    public void Configure(EntityTypeBuilder<EmploymentContractLine> builder)
    {
        builder.ToTable("EmploymentContractLines");

        builder.Property(l => l.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(l => l.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(l => l.Amount).HasPrecision(18, 4);

        builder.HasOne(l => l.EmploymentContract)
            .WithMany(c => c.Lines)
            .HasForeignKey(l => l.EmploymentContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
    {
        builder.ToTable("EmployeeDocuments");
        builder.Property(d => d.BranchId).IsRequired();
        builder.Property(d => d.DocumentNumber).HasMaxLength(100);

        builder.HasOne(d => d.Employee).WithMany().HasForeignKey(d => d.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.EmployeeDocumentType).WithMany().HasForeignKey(d => d.EmployeeDocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Attachment).WithMany().HasForeignKey(d => d.AttachmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.EmployeeId);
    }
}

public class EmployeeCertificationConfiguration : IEntityTypeConfiguration<EmployeeCertification>
{
    public void Configure(EntityTypeBuilder<EmployeeCertification> builder)
    {
        builder.ToTable("EmployeeCertifications");
        builder.Property(c => c.BranchId).IsRequired();
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Issuer).IsRequired().HasMaxLength(200);
        builder.Property(c => c.CertificateNumber).HasMaxLength(100);

        builder.HasOne(c => c.Employee).WithMany().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.Attachment).WithMany().HasForeignKey(c => c.AttachmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.EmployeeId);
    }
}
