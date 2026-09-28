using Habbak.ERP.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Sales;

public class SalesCycleSettingsConfiguration : IEntityTypeConfiguration<SalesCycleSettings>
{
    public void Configure(EntityTypeBuilder<SalesCycleSettings> builder)
    {
        builder.ToTable("SalesCycleSettings");

        builder.HasIndex(s => s.CompanyId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
