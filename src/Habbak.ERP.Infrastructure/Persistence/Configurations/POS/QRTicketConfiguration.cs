using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class QRTicketConfiguration : IEntityTypeConfiguration<QRTicket>
{
    public void Configure(EntityTypeBuilder<QRTicket> builder)
    {
        builder.ToTable("QRTickets");

        builder.HasOne(t => t.RedeemedByCheck)
            .WithMany()
            .HasForeignKey(t => t.RedeemedByCheckId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.IdempotencyKey).IsUnique();
    }
}

public class QRTicketLineConfiguration : IEntityTypeConfiguration<QRTicketLine>
{
    public void Configure(EntityTypeBuilder<QRTicketLine> builder)
    {
        builder.ToTable("QRTicketLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitPrice).HasPrecision(18, 4);

        builder.HasOne(l => l.QRTicket)
            .WithMany(t => t.Lines)
            .HasForeignKey(l => l.QRTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Inventory.Item>()
            .WithMany()
            .HasForeignKey(l => l.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.QRTicketId, l.LineNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
