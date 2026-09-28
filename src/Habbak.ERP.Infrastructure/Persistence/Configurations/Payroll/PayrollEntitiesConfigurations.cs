using Habbak.ERP.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.3 — one file, same convention as
// LegalTablesConfigurations.cs/AttendanceConfigurations.cs.

public class SalaryComponentConfiguration : IEntityTypeConfiguration<SalaryComponent>
{
    public void Configure(EntityTypeBuilder<SalaryComponent> builder)
    {
        builder.ToTable("SalaryComponents");
        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(200);

        builder.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class SalaryStructureConfiguration : IEntityTypeConfiguration<SalaryStructure>
{
    public void Configure(EntityTypeBuilder<SalaryStructure> builder)
    {
        builder.ToTable("SalaryStructures");
        builder.Property(s => s.Code).IsRequired().HasMaxLength(50);
        builder.Property(s => s.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(s => s.NameEn).IsRequired().HasMaxLength(200);

        builder.HasIndex(s => new { s.CompanyId, s.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class SalaryStructureLineConfiguration : IEntityTypeConfiguration<SalaryStructureLine>
{
    public void Configure(EntityTypeBuilder<SalaryStructureLine> builder)
    {
        builder.ToTable("SalaryStructureLines");
        builder.Property(l => l.Amount).HasPrecision(18, 4);
        builder.Property(l => l.Percentage).HasPrecision(9, 4);

        builder.HasOne(l => l.SalaryStructure)
            .WithMany(s => s.Lines)
            .HasForeignKey(l => l.SalaryStructureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.SalaryComponent).WithMany().HasForeignKey(l => l.SalaryComponentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class EmployeeSalaryConfiguration : IEntityTypeConfiguration<EmployeeSalary>
{
    public void Configure(EntityTypeBuilder<EmployeeSalary> builder)
    {
        builder.ToTable("EmployeeSalaries");
        builder.Property(s => s.Amount).HasPrecision(18, 4);

        builder.HasOne(s => s.Employee).WithMany().HasForeignKey(s => s.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.SalaryComponent).WithMany().HasForeignKey(s => s.SalaryComponentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.EmployeeId, s.SalaryComponentId, s.EffectiveFrom });
    }
}

public class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> builder)
    {
        builder.ToTable("PayrollPeriods");

        builder.HasIndex(p => new { p.CompanyId, p.Year, p.Month }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.ToTable("PayrollRuns");
        builder.Property(r => r.TotalGross).HasPrecision(18, 4);
        builder.Property(r => r.TotalDeductions).HasPrecision(18, 4);
        builder.Property(r => r.TotalNet).HasPrecision(18, 4);
        builder.Property(r => r.TotalEmployerCost).HasPrecision(18, 4);

        builder.HasOne(r => r.PayrollPeriod).WithMany().HasForeignKey(r => r.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);

        // Rule 30 (Docs/Modules/10-Module-HR-Payroll.md) — a Reversed/Rejected run frees its key for a
        // new attempt; PayrollRunStatus.Reversed = 7, Rejected = 8 (Domain/Payroll/Enums.cs).
        builder.HasIndex(r => r.IdempotencyKey).IsUnique().HasFilter("[Status] NOT IN (7,8) AND [IsDeleted] = 0");
    }
}

public class PayrollLineConfiguration : IEntityTypeConfiguration<PayrollLine>
{
    public void Configure(EntityTypeBuilder<PayrollLine> builder)
    {
        builder.ToTable("PayrollLines");
        builder.Property(l => l.Amount).HasPrecision(18, 4);
        builder.Property(l => l.Quantity).HasPrecision(9, 4);

        builder.HasOne(l => l.PayrollRun)
            .WithMany()
            .HasForeignKey(l => l.PayrollRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Employee).WithMany().HasForeignKey(l => l.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.SalaryComponent).WithMany().HasForeignKey(l => l.SalaryComponentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.PayrollRunId, l.EmployeeId });
    }
}

public class PayslipConfiguration : IEntityTypeConfiguration<Payslip>
{
    public void Configure(EntityTypeBuilder<Payslip> builder)
    {
        builder.ToTable("Payslips");
        builder.Property(p => p.Gross).HasPrecision(18, 4);
        builder.Property(p => p.TotalDeductions).HasPrecision(18, 4);
        builder.Property(p => p.Net).HasPrecision(18, 4);
        builder.Property(p => p.PdfPath).HasMaxLength(500);

        builder.HasOne(p => p.PayrollRun).WithMany().HasForeignKey(p => p.PayrollRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Employee).WithMany().HasForeignKey(p => p.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.PayrollRunId, p.EmployeeId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class EmployeeTaxProfileConfiguration : IEntityTypeConfiguration<EmployeeTaxProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeTaxProfile> builder)
    {
        builder.ToTable("EmployeeTaxProfiles");
        builder.Property(p => p.ExemptionReason).HasMaxLength(500);
        builder.Property(p => p.YtdTaxableIncome).HasPrecision(18, 4);
        builder.Property(p => p.YtdTaxWithheld).HasPrecision(18, 4);

        builder.HasOne(p => p.Employee).WithMany().HasForeignKey(p => p.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.EmployeeId, p.TaxYear }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class TipsDistributionConfiguration : IEntityTypeConfiguration<TipsDistribution>
{
    public void Configure(EntityTypeBuilder<TipsDistribution> builder)
    {
        builder.ToTable("TipsDistributions");
        builder.Property(t => t.BranchId).IsRequired();
        builder.Property(t => t.TotalAmount).HasPrecision(18, 4);

        builder.HasOne(t => t.PayrollPeriod).WithMany().HasForeignKey(t => t.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.CompanyId, t.BranchId, t.PayrollPeriodId });
    }
}

public class TipsDistributionLineConfiguration : IEntityTypeConfiguration<TipsDistributionLine>
{
    public void Configure(EntityTypeBuilder<TipsDistributionLine> builder)
    {
        builder.ToTable("TipsDistributionLines");
        builder.Property(l => l.Share).HasPrecision(9, 4);
        builder.Property(l => l.Amount).HasPrecision(18, 4);

        builder.HasOne(l => l.TipsDistribution)
            .WithMany(t => t.Lines)
            .HasForeignKey(l => l.TipsDistributionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Employee).WithMany().HasForeignKey(l => l.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
