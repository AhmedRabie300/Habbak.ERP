using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests;

/// <summary>
/// Real SQL Server per test class — LocalDB on Windows, a Testcontainers-managed container
/// elsewhere (<see cref="TestSqlServer"/>, Docs/Setup/Testing.md). 00-Project-Overview.md, section
/// 25 — the posting engine's behavior must be verified against a real database, not an in-memory
/// substitute, since it relies on SQL Server-specific features: RowVersion concurrency tokens, the
/// BankReconciliationLine check constraint, filtered unique indexes...).
/// </summary>
public sealed class PostingServiceFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"HabbakErpTests_{Guid.NewGuid():N}";
    private string _connectionString = null!;

    public AppDbContext CreateContext(ICurrentCompanyContext? currentCompanyContext = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        return new AppDbContext(options, currentCompanyContext);
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
}

public sealed class TestCurrentCompanyContext(long companyId, long? branchId = null, long userId = 1, long? employeeId = null) : ICurrentCompanyContext
{
    public long CompanyId { get; } = companyId;
    public long? BranchId { get; } = branchId;
    public long UserId { get; } = userId;
    public long? EmployeeId { get; } = employeeId;
}
