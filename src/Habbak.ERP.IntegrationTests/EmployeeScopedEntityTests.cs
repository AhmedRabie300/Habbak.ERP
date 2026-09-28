using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Common;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §0.1 — the DataScope.Self Global Query Filter for
/// IEmployeeScopedEntity, built the same way as the existing IBranchScopedEntity filter
/// (AppDbContext.BuildBranchFilters). No real Domain entity implements the interface yet (the HR
/// module's own entities will, in Phase 1), so this proves the mechanism itself against a
/// throwaway entity added to a subclassed AppDbContext — the exact same OnModelCreating pipeline
/// production entities go through, not a re-implementation of it.
/// </summary>
public sealed class EmployeeScopedEntityTests : IAsyncLifetime
{
    private sealed class TestEmployeeScopedEntity : IEmployeeScopedEntity
    {
        public long Id { get; set; }
        public long EmployeeId { get; set; }
        public string Name { get; set; } = "";
    }

    /// <summary>Adds one throwaway entity to the real AppDbContext model, then reuses its unmodified OnModelCreating.</summary>
    private sealed class EmployeeScopeTestDbContext(DbContextOptions<AppDbContext> options, ICurrentCompanyContext? currentCompanyContext)
        : AppDbContext(options, currentCompanyContext)
    {
        public DbSet<TestEmployeeScopedEntity> TestEmployeeScopedEntities => Set<TestEmployeeScopedEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEmployeeScopedEntity>();
            base.OnModelCreating(modelBuilder);
        }
    }

    private readonly string _databaseName = $"HabbakErpTests_EmployeeScope_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    private EmployeeScopeTestDbContext CreateContext(ICurrentCompanyContext? currentCompanyContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        return new EmployeeScopeTestDbContext(options, currentCompanyContext);
    }

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    private async Task SeedAsync()
    {
        await using var context = CreateContext();
        context.TestEmployeeScopedEntities.AddRange(
            new TestEmployeeScopedEntity { EmployeeId = 1, Name = "Employee 1's row" },
            new TestEmployeeScopedEntity { EmployeeId = 2, Name = "Employee 2's row" });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task A_session_with_no_EmployeeId_sees_every_row()
    {
        await SeedAsync();
        await using var context = CreateContext(new TestCurrentCompanyContext(companyId: 1));

        var rows = await context.TestEmployeeScopedEntities.ToListAsync();

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task A_session_scoped_to_one_employee_sees_only_that_employees_rows()
    {
        await SeedAsync();
        await using var context = CreateContext(new TestCurrentCompanyContext(companyId: 1, employeeId: 1));

        var rows = await context.TestEmployeeScopedEntities.ToListAsync();

        var row = Assert.Single(rows);
        Assert.Equal("Employee 1's row", row.Name);
    }

    [Fact]
    public async Task A_session_scoped_to_an_employee_with_no_rows_sees_nothing()
    {
        await SeedAsync();
        await using var context = CreateContext(new TestCurrentCompanyContext(companyId: 1, employeeId: 999));

        Assert.Empty(await context.TestEmployeeScopedEntities.ToListAsync());
    }
}
