using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// One real SQL Server per test process, picked by OS (Docs/Setup/Testing.md):
/// <list type="bullet">
/// <item>Windows (the usual local dev machine) — SQL Server LocalDB, exactly as before this class
/// existed. Nothing changes for that workflow.</item>
/// <item>Anywhere else (Linux/macOS — CI, or a cloud/container session with no LocalDB, which is a
/// Windows-only technology and cannot run elsewhere) — a real SQL Server 2022 container via
/// Testcontainers, started once and shared by every test class in the run, the same way LocalDB is
/// one shared instance per machine rather than one per test class; each caller still gets its own
/// uniquely-named database inside it (matches AccountingApiFactory's own comment on why).
/// Testcontainers' Ryuk companion container reaps it when the test process exits even without an
/// explicit Dispose, so no factory needs its own container-teardown code.
/// </item>
/// </list>
/// Same class as Habbak.ERP.IntegrationTests.TestSqlServer — duplicated rather than shared, since
/// the two test projects have no common project to hold it in and this is the only piece either
/// needs from the other.
/// </summary>
internal static class TestSqlServer
{
    private static readonly Lazy<Task<MsSqlContainer>> Container = new(StartAsync);

    /// <param name="commandTimeoutSeconds">A dozen test classes migrating against one shared
    /// server (LocalDB, or the one Testcontainers instance below) goes well past the 30-second
    /// default — callers doing that raise this instead of failing on a timeout that says nothing
    /// about the code.</param>
    public static async Task<string> GetConnectionStringAsync(string databaseName, int? commandTimeoutSeconds = null)
    {
        SqlConnectionStringBuilder builder;
        if (OperatingSystem.IsWindows())
        {
            builder = new SqlConnectionStringBuilder($"Server=(localdb)\\mssqllocaldb;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;");
        }
        else
        {
            var container = await Container.Value;
            builder = new SqlConnectionStringBuilder(container.GetConnectionString()) { InitialCatalog = databaseName };
        }

        if (commandTimeoutSeconds is { } timeout)
        {
            // Appending "Command Timeout=N;" as raw text onto an already-built ConnectionString
            // (the first version of this method did that) silently corrupts it when the builder's
            // own output has no trailing separator — go through the builder's own property instead.
            builder.CommandTimeout = timeout;
        }

        return builder.ConnectionString;
    }

    private static async Task<MsSqlContainer> StartAsync()
    {
        var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await container.StartAsync();
        return container;
    }
}
