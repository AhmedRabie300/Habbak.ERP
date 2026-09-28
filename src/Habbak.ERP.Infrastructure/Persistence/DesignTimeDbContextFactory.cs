using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Habbak.ERP.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` build the model without a running API host or a real
/// connection — ICurrentCompanyContext is irrelevant at design time (AppDbContext defaults it
/// to null, and Global Query Filters are never evaluated while generating a migration).
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=HabbakErp;Trusted_Connection=True;");
        return new AppDbContext(optionsBuilder.Options);
    }
}
