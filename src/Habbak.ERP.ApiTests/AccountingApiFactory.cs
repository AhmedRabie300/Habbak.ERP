using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// Boots the real API host (00-Project-Overview.md, section 25: "xUnit + WebApplicationFactory"
/// for API contract tests) against a fresh SQL Server LocalDB, with the Test auth scheme wired
/// in as the default so requests authenticate via TestAuthHandler's headers instead of a real JWT.
/// </summary>
public class AccountingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"HabbakErpApiTests_{Guid.NewGuid():N}";

    /// <summary>
    /// Every test class builds its own database and runs the whole migration chain; a dozen classes
    /// doing that at once on one LocalDB instance goes well past the 30-second default, so the tests
    /// wait rather than fail on a timeout that says nothing about the code.
    /// </summary>
    private string ConnectionString =>
        $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;Command Timeout=300;";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                // The daily maintenance job has nothing to do in a test host.
                ["Maintenance:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    /// <summary>
    /// A raw AppDbContext outside the DI container/HTTP pipeline, for test "arrange" steps
    /// (seeding reference data) that have no HTTP request to derive ICurrentCompanyContext from
    /// — resolving AppDbContext via Services.CreateScope() instead would hit the exact same
    /// "no active HTTP request" guard the real CurrentCompanyContext enforces in production.
    /// </summary>
    public AppDbContext CreateDirectDbContext(long companyId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new AppDbContext(options, new SeedCurrentCompanyContext(companyId));
    }

    private sealed class SeedCurrentCompanyContext(long companyId) : ICurrentCompanyContext
    {
        public long CompanyId { get; } = companyId;
        public long? BranchId => null;
        public long UserId => 0;
        public long? EmployeeId => null;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }
}
