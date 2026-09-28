using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Habbak.ERP.Infrastructure.Persistence.Configurations.Inventory;

public class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("Recipes");

        builder.Property(r => r.RecipeFamilyCode).IsRequired().HasMaxLength(50);
        builder.Property(r => r.OutputQuantity).HasPrecision(18, 4);
        builder.Property(r => r.WastePercentage).HasPrecision(9, 4);

        builder.HasOne(r => r.OutputItem)
            .WithMany()
            .HasForeignKey(r => r.OutputItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.PreviousVersion)
            .WithMany()
            .HasForeignKey(r => r.PreviousVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.RecipeFamilyCode, r.VersionNumber }).IsUnique().HasFilter("[IsDeleted] = 0");

        // A List screen scoped to current versions only (GetRecipesListQuery) hits this constantly.
        builder.HasIndex(r => new { r.CompanyId, r.IsCurrentVersion });
    }
}

public class RecipeLineConfiguration : IEntityTypeConfiguration<RecipeLine>
{
    public void Configure(EntityTypeBuilder<RecipeLine> builder)
    {
        builder.ToTable("RecipeLines");

        builder.Property(l => l.Quantity).HasPrecision(18, 4);
        builder.Property(l => l.UnitFactor).HasPrecision(18, 6);
        builder.HasOne(l => l.Unit).WithMany().HasForeignKey(l => l.UnitId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.Recipe)
            .WithMany(r => r.Lines)
            .HasForeignKey(l => l.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.ComponentItem)
            .WithMany()
            .HasForeignKey(l => l.ComponentItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
