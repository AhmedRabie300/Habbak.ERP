using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Settings;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.Property(r => r.CompanyId).IsRequired();
        builder.Property(r => r.Code).IsRequired().HasMaxLength(50);
        builder.Property(r => r.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(r => r.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(r => r.Description).HasMaxLength(500);

        builder.HasIndex(r => new { r.CompanyId, r.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
