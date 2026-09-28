using Habbak.ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Common;

public class FieldLabelConfiguration : IEntityTypeConfiguration<FieldLabel>
{
    public void Configure(EntityTypeBuilder<FieldLabel> builder)
    {
        builder.ToTable("FieldLabels");

        builder.Property(f => f.ScreenCode).IsRequired().HasMaxLength(100);
        builder.Property(f => f.FieldCode).IsRequired().HasMaxLength(100);
        builder.Property(f => f.NameAr).IsRequired().HasMaxLength(200);
        builder.Property(f => f.NameEn).IsRequired().HasMaxLength(200);

        builder.HasIndex(f => new { f.ScreenCode, f.FieldCode }).IsUnique();
    }
}
