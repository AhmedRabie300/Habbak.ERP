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
/// for API contract tests) against a fresh real SQL Server — LocalDB on Windows, a
/// Testcontainers-managed container elsewhere (<see cref="TestSqlServer"/>,
/// Docs/Setup/Testing.md) — with the Test auth scheme wired in as the default so requests
/// authenticate via TestAuthHandler's headers instead of a real JWT.
/// </summary>
public class AccountingApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"HabbakErpApiTests_{Guid.NewGuid():N}";

    /// <summary>
    /// Resolved in InitializeAsync, before this factory's host is ever built — ConfigureWebHost
    /// (below) reads it synchronously the first time `Services`/`CreateClient()` is touched, which
    /// InitializeAsync only does after this field is set.
    /// </summary>
    private string _connectionString = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
                // The daily maintenance job has nothing to do in a test host.
                ["Maintenance:Enabled"] = "false",
                // Same reasoning — the attendance-device background job has no devices to poll here.
                ["AttendanceDeviceJob:Enabled"] = "false"
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
        _connectionString = await TestSqlServer.GetConnectionStringAsync(_databaseName, commandTimeoutSeconds: 300);
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
            .UseSqlServer(_connectionString)
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
