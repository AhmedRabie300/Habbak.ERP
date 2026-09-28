using System.Net.Http.Json;
using Habbak.ERP.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Step 3 of Docs/End-to-End-Cycle-Test.md — branch request, submit, approve. Approval is the
/// interesting part: it is what mints the transfer order, so the branch never transfers stock to
/// itself without the main warehouse agreeing to the quantities first.
/// </summary>
public class BranchRequestFlowTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    internal static async Task<(long RequestId, long TransferOrderId)> CreateApprovedRequestAsync(
        AccountingApiFactory factory, HttpClient client, CycleIds ids, decimal espressoQty, decimal milkQty)
    {
        var create = await client.PostAsJsonAsync("/api/v1/inventory/branch-requests", new
        {
            branchId = ids.BranchId,
            requestDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            lines = new[]
            {
                new { itemId = ids.EspressoItemId, requestedQuantity = espressoQty },
                new { itemId = ids.MilkItemId,     requestedQuantity = milkQty }
            }
        });
        create.EnsureSuccessStatusCode();
        var requestId = (await create.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;

        (await client.PostAsync($"/api/v1/inventory/branch-requests/{requestId}/submit", null)).EnsureSuccessStatusCode();

        // Approval addresses lines by their own ids, so read them back rather than assuming order.
        long[] lineIds;
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            lineIds = await db.BranchRequestLines
                .Where(l => l.BranchRequestId == requestId)
                .OrderBy(l => l.Id)
                .Select(l => l.Id)
                .ToArrayAsync();
        }

        var approve = await client.PostAsJsonAsync($"/api/v1/inventory/branch-requests/{requestId}/approve", new
        {
            id = requestId,
            sourceWarehouseId = ids.MainWarehouseId,
            custodyOfficerId = ids.CustodyOfficerId,
            transferDocumentDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            lines = new[]
            {
                new { lineId = lineIds[0], approvedQuantity = espressoQty },
                new { lineId = lineIds[1], approvedQuantity = milkQty }
            }
        });
        approve.EnsureSuccessStatusCode();
        var transferOrderId = (await approve.Content.ReadFromJsonAsync<ApproveResponse>())!.TransferOrderId;

        return (requestId, transferOrderId);
    }

    [Fact]
    public async Task Approving_a_branch_request_creates_a_draft_transfer_order()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId, 5000m);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.MainWarehouseId, ids.MilkItemId, 20000m);
        var client = CreateClient(companyId);

        var (requestId, transferOrderId) = await CreateApprovedRequestAsync(factory, client, ids, 2000m, 8000m);

        await using var db = factory.CreateDirectDbContext(companyId);

        var request = await db.BranchRequests.FirstAsync(r => r.Id == requestId);
        Assert.Equal(BranchRequestStatus.Approved, request.Status);

        var order = await db.WarehouseDocuments.Include(d => d.Lines).FirstAsync(d => d.Id == transferOrderId);
        Assert.Equal(WarehouseDocumentType.TransferOrder, order.DocumentType);
        Assert.Equal(WarehouseDocumentStatus.Draft, order.Status);
        Assert.Equal(ids.MainWarehouseId, order.SourceWarehouseId);
        Assert.Equal(ids.CustodyOfficerId, order.CustodyOfficerId);
        Assert.Equal(2, order.Lines.Count);

        // Creating and approving the request must not move anything on its own - only posting does.
        Assert.Equal(5000m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId));
    }

    [Fact]
    public async Task Branch_item_limit_blocks_submitting_an_oversized_request()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);

        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            db.BranchItemLimits.Add(new BranchItemLimit
            {
                BranchId = ids.BranchId, ItemId = ids.EspressoItemId, MaxRequestQuantity = 100m
            });
            await db.SaveChangesAsync();
        }

        var create = await client.PostAsJsonAsync("/api/v1/inventory/branch-requests", new
        {
            branchId = ids.BranchId,
            requestDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            lines = new[] { new { itemId = ids.EspressoItemId, requestedQuantity = 2000m } }
        });
        create.EnsureSuccessStatusCode();
        var requestId = (await create.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;

        // The limit is enforced on submit, not on create - creating an over-limit draft is allowed.
        var submit = await client.PostAsync($"/api/v1/inventory/branch-requests/{requestId}/submit", null);

        Assert.False(submit.IsSuccessStatusCode);
        Assert.Contains("INV-R4-EXCEEDS-MAX-REQUEST", await submit.Content.ReadAsStringAsync());
    }

    private sealed record CreatedResponse(long Id);
    private sealed record ApproveResponse(long TransferOrderId);
}
