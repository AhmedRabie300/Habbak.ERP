using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Settings;

public class ScreenPermissionConfiguration : IEntityTypeConfiguration<ScreenPermission>
{
    public void Configure(EntityTypeBuilder<ScreenPermission> builder)
    {
        builder.ToTable("ScreenPermissions");

        builder.Property(p => p.CompanyId).IsRequired();
        builder.Property(p => p.ScreenCode).IsRequired().HasMaxLength(100);

        builder.HasOne(p => p.Role)
            .WithMany(r => r.ScreenPermissions)
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.RoleId, p.ScreenCode }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
