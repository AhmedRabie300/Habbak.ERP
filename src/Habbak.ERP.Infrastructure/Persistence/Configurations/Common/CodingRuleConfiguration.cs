using Habbak.ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Common;

public class CodingRuleConfiguration : IEntityTypeConfiguration<CodingRule>
{
    public void Configure(EntityTypeBuilder<CodingRule> builder)
    {
        builder.ToTable("CodingRules");

        builder.Property(r => r.ScreenCode).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Prefix).HasMaxLength(20);

        builder.HasIndex(r => new { r.CompanyId, r.ScreenCode }).IsUnique();
    }
}
