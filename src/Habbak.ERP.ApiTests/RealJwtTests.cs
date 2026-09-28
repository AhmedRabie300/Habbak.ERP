using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Habbak.ERP.Application.Settings;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// The API host with its real JWT Bearer validation — no test auth scheme. Everything else in the
/// suite authenticates through TestAuthHandler, which would not notice the token's claims being
/// renamed on the way in (the bug this class was written for: JwtBearer maps "role" to the long
/// ClaimTypes.Role URI unless told not to, and every signed-in user ended up with no roles).
/// </summary>
public sealed class RealJwtApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"HabbakErpJwtTests_{Guid.NewGuid():N}";
    private string _connectionString = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
                ["Maintenance:Enabled"] = "false",
                ["AttendanceDeviceJob:Enabled"] = "false"
            }));

    public AppDbContext CreateDirectDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_connectionString).Options);

    public async Task InitializeAsync()
    {
        _connectionString = await TestSqlServer.GetConnectionStringAsync(_databaseName);
        await using var db = CreateDirectDbContext();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using var db = CreateDirectDbContext();
        await db.Database.EnsureDeletedAsync();
        await base.DisposeAsync();
    }
}

public class RealJwtTests(RealJwtApiFactory factory) : IClassFixture<RealJwtApiFactory>
{
    [Fact]
    public async Task The_seeded_admin_signs_in_to_a_fresh_installation_and_the_real_token_carries_full_access()
    {
        // A fresh installation: the migration seeded "admin" but no company existed to make it
        // super admin of — the first company does (CompanySecurityDefaults' bootstrap rule).
        long companyId;
        await using (var db = factory.CreateDirectDbContext())
        {
            var currency = new Currency { Code = "EGP", NameAr = "جنيه مصري", NameEn = "Egyptian Pound", IsActive = true };
            db.Currencies.Add(currency);
            await db.SaveChangesAsync();
            var company = new Company { Code = "MAIN", NameAr = "الشركة", NameEn = "Company", BaseCurrencyId = currency.Id, IsActive = true };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            await CompanySecurityDefaults.AddAsync(db, company.Id, DateTime.UtcNow, 0, default);
            await db.SaveChangesAsync();
            companyId = company.Id;
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = "Admin@123" });
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Equal(companyId, me.GetProperty("companyId").GetInt64());
        Assert.Contains("SUPER_ADMIN", me.GetProperty("roleCodes").EnumerateArray().Select(r => r.GetString()));
        Assert.True(me.GetProperty("isFullAccess").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/accounting/journal-entries")).StatusCode);

        // No token at all: 401, not a screen-permission answer.
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/accounting/journal-entries")).StatusCode);
    }
}
