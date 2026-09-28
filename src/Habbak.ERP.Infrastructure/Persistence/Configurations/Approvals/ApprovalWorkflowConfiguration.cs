using Habbak.ERP.Domain.Approvals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Approvals;

public class ApprovalWorkflowConfiguration : IEntityTypeConfiguration<ApprovalWorkflow>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflow> builder)
    {
        builder.ToTable("ApprovalWorkflows");

        builder.Property(w => w.Code).IsRequired().HasMaxLength(100);
        builder.Property(w => w.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(w => w.NameEn).IsRequired().HasMaxLength(200);

        // One current version per (company, family) — same pattern as PostingTemplate.
        builder.HasIndex(w => new { w.CompanyId, w.FamilyId })
            .IsUnique()
            .HasFilter("[IsCurrentVersion] = 1 AND [IsDeleted] = 0");
        builder.HasIndex(w => new { w.CompanyId, w.Code });
    }
}

public class ApprovalWorkflowStepConfiguration : IEntityTypeConfiguration<ApprovalWorkflowStep>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflowStep> builder)
    {
        builder.ToTable("ApprovalWorkflowSteps");

        builder.HasOne(s => s.ApprovalWorkflow)
            .WithMany(w => w.Steps)
            .HasForeignKey(s => s.ApprovalWorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.ApprovalWorkflowId, s.StepOrder }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public class ApprovalStepApproverConfiguration : IEntityTypeConfiguration<ApprovalStepApprover>
{
    public void Configure(EntityTypeBuilder<ApprovalStepApprover> builder)
    {
        builder.ToTable("ApprovalStepApprovers");

        builder.HasOne(a => a.ApprovalWorkflowStep)
            .WithMany(s => s.Approvers)
            .HasForeignKey(a => a.ApprovalWorkflowStepId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ApprovalWorkflowAssignmentConfiguration : IEntityTypeConfiguration<ApprovalWorkflowAssignment>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflowAssignment> builder)
    {
        builder.ToTable("ApprovalWorkflowAssignments");

        builder.Property(a => a.MinAmount).HasPrecision(18, 2);

        builder.HasOne(a => a.Screen)
            .WithMany()
            .HasForeignKey(a => a.ScreenId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ApprovalWorkflow)
            .WithMany()
            .HasForeignKey(a => a.ApprovalWorkflowId)
            .OnDelete(DeleteBehavior.Restrict);

        // One active assignment per screen per company — the screen's workflow is unambiguous.
        builder.HasIndex(a => new { a.CompanyId, a.ScreenId })
            .IsUnique()
            .HasFilter("[IsActive] = 1 AND [IsDeleted] = 0");
    }
}
