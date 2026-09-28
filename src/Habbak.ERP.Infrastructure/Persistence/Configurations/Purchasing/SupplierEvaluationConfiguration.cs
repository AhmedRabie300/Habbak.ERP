using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class SupplierEvaluationConfiguration : IEntityTypeConfiguration<SupplierEvaluation>
{
    public void Configure(EntityTypeBuilder<SupplierEvaluation> builder)
    {
        builder.ToTable("SupplierEvaluations");

        builder.Property(e => e.QualityScore).HasPrecision(5, 2);
        builder.Property(e => e.DeliveryTimeScore).HasPrecision(5, 2);
        builder.Property(e => e.QuantityComplianceScore).HasPrecision(5, 2);
        builder.Property(e => e.OverallScore).HasPrecision(5, 2);
        builder.Property(e => e.Notes).HasMaxLength(1000);

        builder.HasOne(e => e.Supplier)
            .WithMany()
            .HasForeignKey(e => e.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.SupplierId, e.EvaluationDate });
    }
}
