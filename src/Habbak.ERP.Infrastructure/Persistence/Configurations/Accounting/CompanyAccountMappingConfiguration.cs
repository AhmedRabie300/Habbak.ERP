using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Accounting;

public class CompanyAccountMappingConfiguration : IEntityTypeConfiguration<CompanyAccountMapping>
{
    public void Configure(EntityTypeBuilder<CompanyAccountMapping> builder)
    {
        builder.ToTable("CompanyAccountMappings");

        builder.HasOne(m => m.Account)
            .WithMany()
            .HasForeignKey(m => m.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // One account per role per company; filtered so clearing a mapping (soft delete) and
        // setting it again does not collide with the deleted row.
        builder.HasIndex(m => new { m.CompanyId, m.Role }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
