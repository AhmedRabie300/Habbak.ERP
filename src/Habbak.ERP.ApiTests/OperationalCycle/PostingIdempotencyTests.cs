using System.Net.Http.Json;
using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Gap G-8: the idempotency pipeline existed but only POS commands opted into it, so a retried
/// warehouse posting moved stock twice. That got worse once costs arrived — a double receipt does
/// not just inflate quantity, it permanently skews the warehouse's weighted average.
///
/// The key travels in an <c>Idempotency-Key</c> header because these endpoints carry no body.
/// Omitting it keeps the old behaviour exactly, which is what makes this safe to add everywhere.
/// </summary>
public class PostingIdempotencyTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private static async Task<HttpResponseMessage> PostWithKeyAsync(HttpClient client, string url, Guid? key)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, url);
        if (key is { } value)
        {
            message.Headers.Add(IdempotencyHeaderName, value.ToString());
        }
        return await client.SendAsync(message);
    }

    private const string IdempotencyHeaderName = "Idempotency-Key";

    private async Task<(CycleIds Ids, long CompanyId, HttpClient Client, long StockInId)> ArrangeStockInAsync()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);

        var create = await client.PostAsJsonAsync("/api/v1/inventory/stock-in", new
        {
            documentDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            destinationWarehouseId = ids.MainWarehouseId,
            lines = new[] { new { itemId = ids.EspressoItemId, quantity = 1000m, unitCost = 0.50m } }
        });
        create.EnsureSuccessStatusCode();
        var stockInId = (await create.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;

        return (ids, companyId, client, stockInId);
    }

    [Fact]
    public async Task Replaying_a_post_with_the_same_key_moves_stock_only_once()
    {
        var (ids, companyId, client, stockInId) = await ArrangeStockInAsync();
        var key = Guid.NewGuid();

        (await PostWithKeyAsync(client, $"/api/v1/inventory/stock-in/{stockInId}/post", key)).EnsureSuccessStatusCode();

        // The same request arriving again — a retry after a timeout, or an offline queue flushing.
        var replay = await PostWithKeyAsync(client, $"/api/v1/inventory/stock-in/{stockInId}/post", key);
        replay.EnsureSuccessStatusCode();

        Assert.Equal(1000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId));

        await using var db = factory.CreateDirectDbContext(companyId);
        Assert.Equal(1, await db.StockTransactions.CountAsync());
    }

    [Fact]
    public async Task A_replayed_receipt_does_not_skew_the_weighted_average()
    {
        var (ids, companyId, client, stockInId) = await ArrangeStockInAsync();
        var key = Guid.NewGuid();

        (await PostWithKeyAsync(client, $"/api/v1/inventory/stock-in/{stockInId}/post", key)).EnsureSuccessStatusCode();
        (await PostWithKeyAsync(client, $"/api/v1/inventory/stock-in/{stockInId}/post", key)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        var balance = await db.StockBalances.FirstAsync(b => b.WarehouseId == ids.MainWarehouseId && b.ItemId == ids.EspressoItemId);

        // Cost is the reason this matters more than it used to: a second application would fold
        // the same purchase into the average again and the figure would never recover.
        Assert.Equal(0.50m, balance.AverageCost);
    }

    [Fact]
    public async Task A_different_key_is_a_different_request_and_is_not_short_circuited()
    {
        var (_, _, client, stockInId) = await ArrangeStockInAsync();

        (await PostWithKeyAsync(client, $"/api/v1/inventory/stock-in/{stockInId}/post", Guid.NewGuid())).EnsureSuccessStatusCode();

        // Genuinely new key, so the handler runs — and the document's own Draft-only guard is what
        // stops it, not idempotency. Idempotency deduplicates retries; it is not a status check.
        var second = await PostWithKeyAsync(client, $"/api/v1/inventory/stock-in/{stockInId}/post", Guid.NewGuid());

        Assert.False(second.IsSuccessStatusCode);
        Assert.Contains("INV-WHDOC-NOT-DRAFT", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Without_a_key_the_endpoint_behaves_exactly_as_before()
    {
        var (ids, companyId, client, stockInId) = await ArrangeStockInAsync();

        (await PostWithKeyAsync(client, $"/api/v1/inventory/stock-in/{stockInId}/post", key: null)).EnsureSuccessStatusCode();

        Assert.Equal(1000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId));

        await using var db = factory.CreateDirectDbContext(companyId);
        Assert.Equal(1, await db.StockTransactions.CountAsync());
    }

    private sealed record CreatedResponse(long Id);
}
