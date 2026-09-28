using Habbak.ERP.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Settings;

public class UserScopeConfiguration : IEntityTypeConfiguration<UserScope>
{
    public void Configure(EntityTypeBuilder<UserScope> builder)
    {
        builder.ToTable("UserScopes");

        builder.Property(s => s.CompanyId).IsRequired();
        builder.Property(s => s.RoleInScope).IsRequired().HasMaxLength(50);

        builder.HasOne(s => s.User)
            .WithMany(u => u.UserScopes)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // A NULL BranchId (every branch) is one value to a SQL Server unique index, so a user has
        // at most one company-wide scope per company.
        builder.HasIndex(s => new { s.UserId, s.CompanyId, s.BranchId }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
