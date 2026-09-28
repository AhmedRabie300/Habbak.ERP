using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.POS;

public class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> builder)
    {
        builder.ToTable("ShiftAssignments");

        builder.HasOne(a => a.POSTerminal)
            .WithMany()
            .HasForeignKey(a => a.POSTerminalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.POSTerminalId, a.UserId, a.AssignedDate }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
