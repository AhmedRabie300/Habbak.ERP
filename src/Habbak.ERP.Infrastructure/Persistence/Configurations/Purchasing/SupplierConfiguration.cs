using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.Property(s => s.Code).IsRequired().HasMaxLength(50);
        builder.Property(s => s.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(s => s.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(s => s.TaxNumber).HasMaxLength(50);
        builder.Property(s => s.Phone).HasMaxLength(50);
        builder.Property(s => s.Email).HasMaxLength(200);
        builder.Property(s => s.Address).HasMaxLength(500);
        builder.Property(s => s.CreditLimit).HasPrecision(18, 4);
        builder.Property(s => s.CurrencyCode).IsRequired().HasMaxLength(3);

        builder.HasOne(s => s.DefaultWarehouse)
            .WithMany()
            .HasForeignKey(s => s.DefaultWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.PayableAccount)
            .WithMany()
            .HasForeignKey(s => s.PayableAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ExpenseAccount)
            .WithMany()
            .HasForeignKey(s => s.ExpenseAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered: soft-delete (AuditSaveChangesInterceptor) turns a Remove into
        // IsDeleted = true rather than a physical DELETE.
        builder.HasIndex(s => new { s.CompanyId, s.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
