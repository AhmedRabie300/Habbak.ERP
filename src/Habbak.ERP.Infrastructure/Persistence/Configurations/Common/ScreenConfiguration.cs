using Habbak.ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Common;

public class ScreenConfiguration : IEntityTypeConfiguration<Screen>
{
    public void Configure(EntityTypeBuilder<Screen> builder)
    {
        builder.ToTable("Screens");

        builder.Property(s => s.Code).IsRequired().HasMaxLength(100);
        builder.Property(s => s.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(s => s.NameEn).IsRequired().HasMaxLength(200);
        builder.Property(s => s.ModuleCode).IsRequired().HasMaxLength(50);

        builder.HasIndex(s => s.Code).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(s => s.ModuleCode);
    }
}
