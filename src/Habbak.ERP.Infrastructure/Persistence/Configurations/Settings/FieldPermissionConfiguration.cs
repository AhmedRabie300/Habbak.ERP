using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Settings;

public class FieldPermissionConfiguration : IEntityTypeConfiguration<FieldPermission>
{
    public void Configure(EntityTypeBuilder<FieldPermission> builder)
    {
        builder.ToTable("FieldPermissions");

        builder.Property(p => p.CompanyId).IsRequired();
        builder.Property(p => p.ScreenCode).IsRequired().HasMaxLength(100);
        builder.Property(p => p.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(p => p.FieldName).IsRequired().HasMaxLength(100);

        builder.HasOne(p => p.Role)
            .WithMany(r => r.FieldPermissions)
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.RoleId, p.ScreenCode, p.EntityType, p.FieldName }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
