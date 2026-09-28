using Habbak.ERP.Domain.Approvals;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Approvals;

public class ApprovalInstanceConfiguration : IEntityTypeConfiguration<ApprovalInstance>
{
    public void Configure(EntityTypeBuilder<ApprovalInstance> builder)
    {
        builder.ToTable("ApprovalInstances");

        builder.Property(i => i.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(i => i.Amount).HasPrecision(18, 2);

        builder.HasOne(i => i.ApprovalWorkflow)
            .WithMany()
            .HasForeignKey(i => i.ApprovalWorkflowId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.EntityType, i.EntityId });
        builder.HasIndex(i => new { i.CompanyId, i.Status });
        builder.HasIndex(i => i.RequestedByUserId);
    }
}

public class ApprovalActionConfiguration : IEntityTypeConfiguration<ApprovalAction>
{
    public void Configure(EntityTypeBuilder<ApprovalAction> builder)
    {
        builder.ToTable("ApprovalActions");

        builder.Property(a => a.Reason).HasMaxLength(1000);

        builder.HasOne(a => a.ApprovalInstance)
            .WithMany(i => i.Actions)
            .HasForeignKey(a => a.ApprovalInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ApprovalInstanceId);
    }
}
