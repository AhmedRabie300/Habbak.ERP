using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Settings;

public class ButtonPermissionConfiguration : IEntityTypeConfiguration<ButtonPermission>
{
    public void Configure(EntityTypeBuilder<ButtonPermission> builder)
    {
        builder.ToTable("ButtonPermissions");

        builder.Property(p => p.CompanyId).IsRequired();
        builder.Property(p => p.ScreenCode).IsRequired().HasMaxLength(100);
        builder.Property(p => p.ButtonCode).IsRequired().HasMaxLength(100);

        builder.HasOne(p => p.Role)
            .WithMany(r => r.ButtonPermissions)
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.RoleId, p.ScreenCode, p.ButtonCode }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
