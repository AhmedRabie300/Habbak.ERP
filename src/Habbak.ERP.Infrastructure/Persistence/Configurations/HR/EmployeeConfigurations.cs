using Habbak.ERP.Domain.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.HR;

// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B2. EncryptedStringConverter for
// NationalIdEncrypted/BankIbanEncrypted is wired in AppDbContext.OnModelCreating directly (needs the
// keyed "HR.PII" ISecretProtector instance), not here — IEntityTypeConfiguration classes are
// instantiated by assembly-scanning reflection and have no constructor-injection path.

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.Property(e => e.Code).IsRequired().HasMaxLength(50);
        builder.Property(e => e.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(e => e.NameEn).IsRequired().HasMaxLength(200);

        // Required at the DB level despite the nullable C# type (IBranchScopedEntity compliance,
        // same convention as Shift/POSTerminal — Phase-1.1-Research.md §2.4).
        builder.Property(e => e.BranchId).IsRequired();

        builder.HasOne(e => e.OrgUnit).WithMany().HasForeignKey(e => e.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.JobPosition).WithMany().HasForeignKey(e => e.JobPositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.JobGrade).WithMany().HasForeignKey(e => e.JobGradeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Manager).WithMany().HasForeignKey(e => e.ManagerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.CompanyId, e.Code }).IsUnique().HasFilter("[IsDeleted] = 0");

        // Rule 1 (10-Module-HR-Payroll.md §3.1): the official User<->Employee link, unique per
        // company. UserId IS NOT NULL is required in the filter — unlike some other databases, SQL
        // Server's unique index treats multiple NULLs in the same key as duplicates, not as distinct
        // (confirmed the hard way: most employees have no linked user at all, so without this the
        // second employee ever created without one would fail to save).
        builder.HasIndex(e => new { e.CompanyId, e.UserId }).IsUnique().HasFilter("[IsDeleted] = 0 AND [UserId] IS NOT NULL");
    }
}

public class EmployeePersonalDataConfiguration : IEntityTypeConfiguration<EmployeePersonalData>
{
    public void Configure(EntityTypeBuilder<EmployeePersonalData> builder)
    {
        builder.ToTable("EmployeePersonalData");

        builder.Property(d => d.NationalIdEncrypted).IsRequired();
        builder.Property(d => d.NationalIdHash).IsRequired().HasMaxLength(64); // hex SHA-256
        builder.Property(d => d.NationalIdLast4).IsRequired().HasMaxLength(4);
        builder.Property(d => d.BankIbanLast4).HasMaxLength(4);
        builder.Property(d => d.BankName).HasMaxLength(200);
        builder.Property(d => d.Address).HasMaxLength(500);
        builder.Property(d => d.PhoneNumber).HasMaxLength(30);
        builder.Property(d => d.PersonalEmail).HasMaxLength(200);
        builder.Property(d => d.EmergencyContactName).HasMaxLength(200);
        builder.Property(d => d.EmergencyContactPhone).HasMaxLength(30);

        builder.HasOne(d => d.Employee).WithOne(e => e.PersonalData).HasForeignKey<EmployeePersonalData>(d => d.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Bank).WithMany().HasForeignKey(d => d.BankId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Nationality).WithMany().HasForeignKey(d => d.NationalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.City).WithMany().HasForeignKey(d => d.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.MilitaryStatus).WithMany().HasForeignKey(d => d.MilitaryStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.QualificationType).WithMany().HasForeignKey(d => d.QualificationTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.EmergencyContactRelationshipType).WithMany().HasForeignKey(d => d.EmergencyContactRelationshipTypeId).OnDelete(DeleteBehavior.Restrict);

        // 1:1 (BranchPOSSettings pattern, not a shared primary key — Phase-1.1-Research.md §2.3).
        builder.HasIndex(d => d.EmployeeId).IsUnique().HasFilter("[IsDeleted] = 0");

        // Rule 4 (10-Module-HR-Payroll.md §3.1): National ID unique within the company, via the hash — never the plaintext.
        builder.HasIndex(d => new { d.CompanyId, d.NationalIdHash }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
