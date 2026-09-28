using Habbak.ERP.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Notifications;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.Property(n => n.TitleAr).IsRequired().HasMaxLength(200);
        builder.Property(n => n.TitleEn).IsRequired().HasMaxLength(200);
        builder.Property(n => n.BodyAr).IsRequired().HasMaxLength(1000);
        builder.Property(n => n.BodyEn).IsRequired().HasMaxLength(1000);
        builder.Property(n => n.RelatedEntityType).HasMaxLength(100);

        // The bell's own worklist: "give me this user's notifications, newest first" and the
        // pending-action/unread counts are both filtered on (RecipientUserId, ...) first.
        builder.HasIndex(n => new { n.RecipientUserId, n.CreatedAtUtc });
        builder.HasIndex(n => new { n.RecipientUserId, n.RequiresAction, n.IsRead });
        builder.HasIndex(n => new { n.RelatedEntityType, n.RelatedEntityId });
    }
}
