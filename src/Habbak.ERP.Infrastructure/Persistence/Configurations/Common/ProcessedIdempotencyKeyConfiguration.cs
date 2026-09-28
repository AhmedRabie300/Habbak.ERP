using Habbak.ERP.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Common;

public class ProcessedIdempotencyKeyConfiguration : IEntityTypeConfiguration<ProcessedIdempotencyKey>
{
    public void Configure(EntityTypeBuilder<ProcessedIdempotencyKey> builder)
    {
        builder.ToTable("ProcessedIdempotencyKeys");

        builder.Property(k => k.OperationType).IsRequired().HasMaxLength(200);
        builder.Property(k => k.ResultJson).IsRequired();

        builder.HasIndex(k => k.IdempotencyKey).IsUnique();
    }
}
