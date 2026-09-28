using Habbak.ERP.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Sales;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.Property(c => c.Code).IsRequired().HasMaxLength(50);
        builder.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Phone).HasMaxLength(50);
        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.Address).HasMaxLength(500);
        builder.Property(c => c.CreditLimit).HasPrecision(18, 4);
        builder.Property(c => c.LoyaltyPointsBalance).HasPrecision(18, 4);

        builder.HasOne(c => c.ReceivableAccount)
            .WithMany()
            .HasForeignKey(c => c.ReceivableAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.LoyaltyTier)
            .WithMany()
            .HasForeignKey(c => c.LoyaltyTierId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
