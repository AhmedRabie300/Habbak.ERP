using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Purchasing;

public class PurchaseCycleSettingsConfiguration : IEntityTypeConfiguration<PurchaseCycleSettings>
{
    public void Configure(EntityTypeBuilder<PurchaseCycleSettings> builder)
    {
        builder.ToTable("PurchaseCycleSettings");

        builder.HasIndex(s => s.CompanyId).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
