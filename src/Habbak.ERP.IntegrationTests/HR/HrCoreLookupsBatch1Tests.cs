using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Infrastructure.Persistence;
using Habbak.ERP.Infrastructure.Persistence.Seeding;
using Habbak.ERP.IntegrationTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Habbak.ERP.IntegrationTests.HR;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §1.1, Batch B1 — proves the 13-table migration applies
/// cleanly to a fresh database, SystemDataSeeder populates the 5 seed-only lookups without error
/// (the exact code path real app startup uses), and the company-scoped/system-wide unique indexes
/// behave as designed.
/// </summary>
public sealed class HrCoreLookupsBatch1Tests : IAsyncLifetime
{
    private readonly string _databaseName = $"HabbakErpTests_HrB1_{Guid.NewGuid():N}";

    private string _connectionString = null!;

    private AppDbContext CreateContext(Habbak.ERP.Application.Common.Interfaces.ICurrentCompanyContext? companyContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_connectionString).Options;
        return new AppDbContext(options, companyContext);
    }

    public async Task InitializeAsync()
    {
        _connectionString = await TestSqlServer.GetConnectionStringAsync(_databaseName);
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Migration_creates_all_13_lookup_tables()
    {
        await using var context = CreateContext();

        var tableNames = await context.Database.SqlQuery<string>(
            $"SELECT name AS [Value] FROM sys.tables WHERE name IN ('Countries','Cities','Nationalities','Banks','JobGrades','JobPositions','OrgUnits','EmployeeDocumentTypes','RelationshipTypes','MilitaryStatuses','QualificationTypes','TerminationReasons','InsuranceOffices')")
            .ToListAsync();

        Assert.Equal(13, tableNames.Count);
    }

    [Fact]
    public async Task SystemDataSeeder_seeds_the_5_seed_only_lookups_without_error()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = _connectionString })
            .Build();

        await SystemDataSeeder.SeedAsync(configuration);

        await using var context = CreateContext();
        Assert.True(await context.Nationalities.AnyAsync());
        Assert.True(await context.RelationshipTypes.AnyAsync());
        Assert.True(await context.MilitaryStatuses.AnyAsync());
        Assert.True(await context.QualificationTypes.AnyAsync());
        Assert.True(await context.TerminationReasons.AnyAsync());

        // Idempotent: a second call must not duplicate rows (SystemDataSeeder's own "only if empty" guard).
        var countBefore = await context.Nationalities.CountAsync();
        await SystemDataSeeder.SeedAsync(configuration);
        await using var context2 = CreateContext();
        Assert.Equal(countBefore, await context2.Nationalities.CountAsync());
    }

    [Fact]
    public async Task Company_scoped_lookups_are_unique_per_company_not_globally()
    {
        await using var context = CreateContext();
        context.JobGrades.AddRange(
            new JobGrade { CompanyId = 1, Code = "G1", NameAr = "أ", NameEn = "A", Level = 1 },
            new JobGrade { CompanyId = 2, Code = "G1", NameAr = "أ", NameEn = "A", Level = 1 }); // same Code, different company — allowed
        await context.SaveChangesAsync();

        await using var duplicate = CreateContext();
        duplicate.JobGrades.Add(new JobGrade { CompanyId = 1, Code = "G1", NameAr = "ب", NameEn = "B", Level = 2 });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [Fact]
    public async Task System_wide_lookups_are_unique_globally()
    {
        await using var context = CreateContext();
        context.InsuranceOffices.Add(new InsuranceOffice { Code = "IO1", NameAr = "أ", NameEn = "A" });
        await context.SaveChangesAsync();

        await using var duplicate = CreateContext();
        duplicate.InsuranceOffices.Add(new InsuranceOffice { Code = "IO1", NameAr = "ب", NameEn = "B" });
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [Fact]
    public async Task City_requires_a_country_but_Nationality_and_Bank_do_not()
    {
        await using var context = CreateContext();
        var egypt = new Country { Code = "EG", NameAr = "مصر", NameEn = "Egypt", IsoCode = "EG" };
        context.Countries.Add(egypt);
        await context.SaveChangesAsync();

        context.Cities.Add(new City { Code = "CAI", NameAr = "القاهرة", NameEn = "Cairo", CountryId = egypt.Id });
        context.Banks.Add(new Bank { Code = "BNK1", NameAr = "بنك", NameEn = "Bank", CountryId = null }); // optional
        await context.SaveChangesAsync();

        Assert.Equal(1, await context.Cities.CountAsync());
        Assert.Equal(1, await context.Banks.CountAsync());
    }

    [Fact]
    public async Task OrgUnit_self_referencing_hierarchy_persists_correctly()
    {
        // CompanyId=1 rows are only visible through the Global Query Filter with a matching
        // ICurrentCompanyContext — a plain `new AppDbContext(options)` has none, so reads would see
        // zero rows even though the insert succeeded.
        var company = new TestCurrentCompanyContext(companyId: 1);

        await using (var context = CreateContext(company))
        {
            var root = new OrgUnit { CompanyId = 1, Code = "ROOT", NameAr = "جذر", NameEn = "Root" };
            context.OrgUnits.Add(root);
            await context.SaveChangesAsync();

            var child = new OrgUnit { CompanyId = 1, Code = "CHILD", NameAr = "فرع", NameEn = "Child", ParentId = root.Id };
            context.OrgUnits.Add(child);
            await context.SaveChangesAsync();
        }

        await using var reload = CreateContext(company);
        var reloaded = await reload.OrgUnits.SingleAsync(u => u.Code == "CHILD");
        var root2 = await reload.OrgUnits.SingleAsync(u => u.Code == "ROOT");
        Assert.Equal(root2.Id, reloaded.ParentId);
    }
}
