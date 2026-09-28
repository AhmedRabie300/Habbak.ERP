using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Infrastructure.Persistence;
using Habbak.ERP.Infrastructure.Persistence.Converters;
using Habbak.ERP.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests.Persistence;

/// <summary>
/// Docs/Implementation/HR-Core-Plan.md §0.3 — EncryptedStringConverter against a throwaway entity
/// added to a subclassed AppDbContext (same technique as EmployeeScopedEntityTests, 0.1): the real,
/// unmodified AppDbContext.OnModelCreating pipeline runs, but no real Domain entity is touched. Not
/// yet applied to EmployeePersonalData — that wiring is Phase 1's job.
/// </summary>
public sealed class EncryptedStringConverterTests : IAsyncLifetime
{
    private sealed class EncryptedTestEntity
    {
        public long Id { get; set; }
        public string? PlainField { get; set; }
        public string? SecretField { get; set; }
    }

    private sealed class EncryptedStringTestDbContext(DbContextOptions<AppDbContext> options, ISecretProtector protector)
        : AppDbContext(options)
    {
        public DbSet<EncryptedTestEntity> EncryptedTestEntities => Set<EncryptedTestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EncryptedTestEntity>(b =>
            {
                b.Property(e => e.SecretField).HasConversion((Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter)new EncryptedStringConverter(protector));
            });
            base.OnModelCreating(modelBuilder);
        }
    }

    private readonly string _databaseName = $"HabbakErpTests_EncryptedString_{Guid.NewGuid():N}";
    private readonly string _keysPath = Path.Combine(Path.GetTempPath(), $"habbak-dp-test-{Guid.NewGuid():N}");
    private ISecretProtector _protector = null!;

    private string ConnectionString =>
        $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;";

    private EncryptedStringTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options;
        return new EncryptedStringTestDbContext(options, _protector);
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_keysPath);
        var provider = DataProtectionProvider.Create(new DirectoryInfo(_keysPath));
        _protector = new PiiSecretProtector(provider);

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        Directory.Delete(_keysPath, recursive: true);
    }

    [Fact]
    public async Task Round_trips_the_value_through_EF_Core()
    {
        long id;
        await using (var context = CreateContext())
        {
            var entity = new EncryptedTestEntity { PlainField = "visible", SecretField = "29001010112345" };
            context.EncryptedTestEntities.Add(entity);
            await context.SaveChangesAsync();
            id = entity.Id;
        }

        await using (var context = CreateContext())
        {
            var reloaded = await context.EncryptedTestEntities.SingleAsync(e => e.Id == id);
            Assert.Equal("29001010112345", reloaded.SecretField);
            Assert.Equal("visible", reloaded.PlainField);
        }
    }

    [Fact]
    public async Task The_stored_column_value_is_not_plain_text()
    {
        long id;
        await using (var context = CreateContext())
        {
            var entity = new EncryptedTestEntity { SecretField = "29001010112345" };
            context.EncryptedTestEntities.Add(entity);
            await context.SaveChangesAsync();
            id = entity.Id;
        }

        await using var raw = CreateContext();
        var rawValue = await raw.Database
            .SqlQuery<string>($"SELECT SecretField AS [Value] FROM EncryptedTestEntities WHERE Id = {id}")
            .SingleAsync();

        Assert.NotEqual("29001010112345", rawValue);
        Assert.DoesNotContain("29001010112345", rawValue);
    }

    [Fact]
    public async Task A_null_encrypted_field_round_trips_as_null_without_touching_the_protector()
    {
        long id;
        await using (var context = CreateContext())
        {
            var entity = new EncryptedTestEntity { PlainField = "no secret here", SecretField = null };
            context.EncryptedTestEntities.Add(entity);
            await context.SaveChangesAsync();
            id = entity.Id;
        }

        await using (var context = CreateContext())
        {
            var reloaded = await context.EncryptedTestEntities.SingleAsync(e => e.Id == id);
            Assert.Null(reloaded.SecretField);
            Assert.Equal("no secret here", reloaded.PlainField);
        }
    }

    [Fact]
    public async Task Decryption_failure_throws_a_clear_exception()
    {
        long id;
        await using (var context = CreateContext())
        {
            var entity = new EncryptedTestEntity { SecretField = "29001010112345" };
            context.EncryptedTestEntities.Add(entity);
            await context.SaveChangesAsync();
            id = entity.Id;
        }

        // Corrupt the stored ciphertext directly, bypassing the converter, to force a decrypt failure.
        await using (var raw = CreateContext())
        {
            await raw.Database.ExecuteSqlAsync(
                $"UPDATE EncryptedTestEntities SET SecretField = 'not-a-real-ciphertext' WHERE Id = {id}");
        }

        await using var context2 = CreateContext();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context2.EncryptedTestEntities.SingleAsync(e => e.Id == id));
        Assert.Contains("decrypt", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
