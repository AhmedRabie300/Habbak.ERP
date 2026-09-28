using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Settings;

public class AuditLogArchiveConfiguration : IEntityTypeConfiguration<AuditLogArchive>
{
    public void Configure(EntityTypeBuilder<AuditLogArchive> builder)
    {
        builder.ToTable("AuditLogsArchive");

        // The Id it had in AuditLogs, so a reference to an entry still finds it after archiving.
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.FieldName).HasMaxLength(100);
        builder.Property(a => a.IpAddress).HasMaxLength(64);
        builder.Property(a => a.UserAgent).HasMaxLength(500);

        builder.HasIndex(a => new { a.CompanyId, a.OccurredAtUtc });
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
