using Habbak.ERP.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Payroll;

// Docs/Implementation/HR-MASTER-PLAN.md §Phase 4, Sub-Batch 4.2 — one file, same convention as
// AttendanceConfigurations.cs/HrConfigurations.cs. No unique DB index enforces non-overlapping date
// ranges (same reasoning as ShiftScheduleConfiguration, Attendance/AttendanceConfigurations.cs:35-37:
// a unique index can't express interval-overlap), so overlap validation is the Create/Update command's
// job (Sub-Batch 4.6) using Domain/Common/IEffectiveDatedEntity.EffectiveDateRules.Overlaps. Indexes
// below are plain (non-unique), sized for "which row applies on date X for this key" queries.

public class MinimumWageConfiguration : IEntityTypeConfiguration<MinimumWage>
{
    public void Configure(EntityTypeBuilder<MinimumWage> builder)
    {
        builder.ToTable("MinimumWages");
        builder.Property(m => m.Amount).HasPrecision(18, 4);
        builder.Property(m => m.Sector).HasMaxLength(100);

        builder.HasIndex(m => new { m.CompanyId, m.Sector, m.EffectiveFrom });
    }
}

public class SocialInsuranceRateConfiguration : IEntityTypeConfiguration<SocialInsuranceRate>
{
    public void Configure(EntityTypeBuilder<SocialInsuranceRate> builder)
    {
        builder.ToTable("SocialInsuranceRates");
        builder.Property(r => r.EmployeeRate).HasPrecision(9, 4);
        builder.Property(r => r.EmployerRate).HasPrecision(9, 4);

        builder.HasIndex(r => new { r.CompanyId, r.EffectiveFrom });
    }
}

public class InsurableWageLimitConfiguration : IEntityTypeConfiguration<InsurableWageLimit>
{
    public void Configure(EntityTypeBuilder<InsurableWageLimit> builder)
    {
        builder.ToTable("InsurableWageLimits");
        builder.Property(l => l.MinWage).HasPrecision(18, 4);
        builder.Property(l => l.MaxWage).HasPrecision(18, 4);

        builder.HasIndex(l => new { l.CompanyId, l.EffectiveFrom });
    }
}

public class PayrollTaxBracketSetConfiguration : IEntityTypeConfiguration<PayrollTaxBracketSet>
{
    public void Configure(EntityTypeBuilder<PayrollTaxBracketSet> builder)
    {
        builder.ToTable("PayrollTaxBracketSets");
        builder.Property(s => s.PersonalExemption).HasPrecision(18, 4);
        builder.Property(s => s.DisqualificationRulesDescription).HasMaxLength(1000);

        builder.HasIndex(s => new { s.CompanyId, s.EffectiveFrom });
    }
}

public class PayrollTaxBracketConfiguration : IEntityTypeConfiguration<PayrollTaxBracket>
{
    public void Configure(EntityTypeBuilder<PayrollTaxBracket> builder)
    {
        builder.ToTable("PayrollTaxBrackets");
        builder.Property(b => b.FromAmount).HasPrecision(18, 4);
        builder.Property(b => b.ToAmount).HasPrecision(18, 4);
        builder.Property(b => b.Rate).HasPrecision(9, 4);

        builder.HasOne(b => b.PayrollTaxBracketSet)
            .WithMany(s => s.Brackets)
            .HasForeignKey(b => b.PayrollTaxBracketSetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MartyrsFundRateConfiguration : IEntityTypeConfiguration<MartyrsFundRate>
{
    public void Configure(EntityTypeBuilder<MartyrsFundRate> builder)
    {
        builder.ToTable("MartyrsFundRates");
        builder.Property(r => r.Rate).HasPrecision(9, 4);

        builder.HasIndex(r => new { r.CompanyId, r.EffectiveFrom });
    }
}

public class OvertimeRateConfiguration : IEntityTypeConfiguration<OvertimeRate>
{
    public void Configure(EntityTypeBuilder<OvertimeRate> builder)
    {
        builder.ToTable("OvertimeRates");
        builder.Property(r => r.Multiplier).HasPrecision(9, 4);

        builder.HasIndex(r => new { r.CompanyId, r.OvertimeType, r.EffectiveFrom });
    }
}

public class LeaveEntitlementRuleConfiguration : IEntityTypeConfiguration<LeaveEntitlementRule>
{
    public void Configure(EntityTypeBuilder<LeaveEntitlementRule> builder)
    {
        builder.ToTable("LeaveEntitlementRules");
        builder.Property(r => r.Days).HasPrecision(9, 2);

        builder.HasOne(r => r.LeaveType).WithMany().HasForeignKey(r => r.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.CompanyId, r.LeaveTypeId, r.MinServiceYears, r.MinAge, r.EffectiveFrom });
    }
}

public class PenaltyDeductionCapConfiguration : IEntityTypeConfiguration<PenaltyDeductionCap>
{
    public void Configure(EntityTypeBuilder<PenaltyDeductionCap> builder)
    {
        builder.ToTable("PenaltyDeductionCaps");
        builder.Property(c => c.MaxDaysPerMonth).HasPrecision(9, 2);

        builder.HasIndex(c => new { c.CompanyId, c.EffectiveFrom });
    }
}

public class NoticePeriodRuleConfiguration : IEntityTypeConfiguration<NoticePeriodRule>
{
    public void Configure(EntityTypeBuilder<NoticePeriodRule> builder)
    {
        builder.ToTable("NoticePeriodRules");

        builder.HasIndex(r => new { r.CompanyId, r.MinServiceYears, r.EffectiveFrom });
    }
}

public class EndOfServicePolicyConfiguration : IEntityTypeConfiguration<EndOfServicePolicy>
{
    public void Configure(EntityTypeBuilder<EndOfServicePolicy> builder)
    {
        builder.ToTable("EndOfServicePolicies");
        builder.Property(p => p.DaysPerYear).HasPrecision(9, 2);
        builder.Property(p => p.FormulaDescription).HasMaxLength(1000);

        builder.HasIndex(p => new { p.CompanyId, p.EffectiveFrom });
    }
}
