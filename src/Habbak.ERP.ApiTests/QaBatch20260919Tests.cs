using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habbak.ERP.ApiTests.OperationalCycle;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// QA batch 2026-09-19 (Docs/My Remarks/Remarks3.md, report Docs/Modules/QA-Batch-2026-09-19.md):
/// the unit on inventory lines, branch scope of warehouse documents and branch requests, the
/// opening-balance line rules, the default currency and the item's tax code.
/// </summary>
public class QaBatch20260919Tests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private HttpClient Client(long companyId, long? branchId = null)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        if (branchId is not null) client.DefaultRequestHeaders.Add(TestAuthHandler.BranchIdHeader, branchId.ToString());
        return client;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()!;

    private static async Task<long> IdOf(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    /// <summary>A "bag" unit for espresso holding <paramref name="factor"/> grams.</summary>
    private async Task<long> AddBagAsync(CycleIds ids, decimal factor = 1000m)
    {
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var bag = new UnitOfMeasure
        {
            CompanyId = ids.CompanyId, Code = $"BAG-{ids.CompanyId % 100000000}", NameAr = "شكارة", NameEn = "Bag", Category = UnitCategory.Weight, IsActive = true
        };
        db.UnitsOfMeasure.Add(bag);
        await db.SaveChangesAsync();
        db.ItemUnitConversions.Add(new ItemUnitConversion { ItemId = ids.EspressoItemId, AlternateUnitOfMeasureId = bag.Id, ConversionFactor = factor });
        await db.SaveChangesAsync();
        return bag.Id;
    }

    private async Task<long> AddBranchAsync(long companyId, string code)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        var branch = new Branch { CompanyId = companyId, Code = $"{code}-{companyId % 100000000}", NameAr = "فرع تاني", NameEn = "Other", IsActive = true };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        return branch.Id;
    }

    private static object Line(long itemId, decimal quantity, decimal unitCost, long? unitId = null) =>
        new { itemId, quantity, unitCost, batchNumber = (string?)null, expiryDate = (DateOnly?)null, unitId };

    private static Task<HttpResponseMessage> CreateDocAsync(HttpClient client, string kind, long? source, long? destination, long? custodyOfficerId, params object[] lines) =>
        client.PostAsJsonAsync($"/api/v1/inventory/{kind}", new
        {
            branchId = (long?)null, documentDate = Today, sourceWarehouseId = source, destinationWarehouseId = destination,
            custodyOfficerId, notes = (string?)null, lines
        });

    private static async Task<HashSet<long>> ListIdsAsync(HttpClient client, string path)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"{path}?page=1&pageSize=200");
        return page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt64()).ToHashSet();
    }

    // ------------------------------------------------------------------------------- units

    [Fact]
    public async Task A_line_in_a_larger_unit_posts_base_units_at_a_cost_per_base_unit()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var bag = await AddBagAsync(ids);
        var client = Client(ids.CompanyId);

        // 2 bags of 1000 g at 500 a bag.
        var docId = await IdOf(await CreateDocAsync(client, "opening-balances", null, ids.BranchWarehouseId, null, Line(ids.EspressoItemId, 2, 500, bag)));
        var doc = await client.GetFromJsonAsync<JsonElement>($"/api/v1/inventory/opening-balances/{docId}");
        var line = doc.GetProperty("lines")[0];
        Assert.Equal(bag, line.GetProperty("unitId").GetInt64());
        Assert.Equal(1000m, line.GetProperty("unitFactor").GetDecimal());
        Assert.Equal(2m, line.GetProperty("quantity").GetDecimal());

        (await client.PostAsync($"/api/v1/inventory/opening-balances/{docId}/post", null)).EnsureSuccessStatusCode();

        Assert.Equal(2000m, await CycleSeed.GetBalanceAsync(factory, ids.CompanyId, ids.BranchWarehouseId, ids.EspressoItemId));
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var movement = await db.StockTransactions.SingleAsync(t => t.SourceDocumentType == "WarehouseDocument" && t.SourceDocumentId == docId);
        Assert.Equal(2000m, movement.Quantity);
        Assert.Equal(0.5m, movement.UnitCost);
    }

    [Fact]
    public async Task A_unit_the_item_does_not_have_is_refused_and_no_unit_means_the_base_unit()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var client = Client(ids.CompanyId);

        // Millilitres are milk's unit, not espresso's.
        var wrong = await CreateDocAsync(client, "stock-in", null, ids.BranchWarehouseId, null, Line(ids.EspressoItemId, 5, 1, ids.MilliUnitId));
        Assert.Equal("INV-UNIT-NOT-ALLOWED", await ErrorCodeAsync(wrong));

        var docId = await IdOf(await CreateDocAsync(client, "stock-in", null, ids.BranchWarehouseId, null, Line(ids.EspressoItemId, 5, 1)));
        var line = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/inventory/stock-in/{docId}")).GetProperty("lines")[0];
        Assert.Equal(ids.GramUnitId, line.GetProperty("unitId").GetInt64());
        Assert.Equal(1m, line.GetProperty("unitFactor").GetDecimal());
    }

    [Fact]
    public async Task Editing_with_a_refused_unit_leaves_the_document_as_it_was()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var client = Client(ids.CompanyId);
        var docId = await IdOf(await CreateDocAsync(client, "stock-in", null, ids.BranchWarehouseId, null, Line(ids.EspressoItemId, 5, 1)));
        var rowVersion = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/inventory/stock-in/{docId}")).GetProperty("rowVersion").GetString();

        var refused = await client.PutAsJsonAsync($"/api/v1/inventory/stock-in/{docId}", new
        {
            rowVersion, branchId = (long?)null, documentDate = Today, sourceWarehouseId = (long?)null, destinationWarehouseId = ids.BranchWarehouseId,
            custodyOfficerId = (long?)null, notes = (string?)null, lines = new[] { Line(ids.EspressoItemId, 7, 1, ids.MilliUnitId) }
        });

        Assert.Equal("INV-UNIT-NOT-ALLOWED", await ErrorCodeAsync(refused));
        var lines = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/inventory/stock-in/{docId}")).GetProperty("lines");
        Assert.Equal(1, lines.GetArrayLength());
        Assert.Equal(5m, lines[0].GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task A_transfer_receipt_line_is_in_the_unit_the_order_shipped_in()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var bag = await AddBagAsync(ids);
        await CycleSeed.GiveStockAsync(factory, ids.CompanyId, ids.MainWarehouseId, ids.EspressoItemId, 5000m, 0.5m);
        var client = Client(ids.CompanyId);

        var orderId = await IdOf(await CreateDocAsync(client, "transfer-order", ids.MainWarehouseId, null, ids.CustodyOfficerId, Line(ids.EspressoItemId, 2, 0, bag)));
        (await client.PostAsync($"/api/v1/inventory/transfer-order/{orderId}/post", null)).EnsureSuccessStatusCode();
        Assert.Equal(3000m, await CycleSeed.GetBalanceAsync(factory, ids.CompanyId, ids.MainWarehouseId, ids.EspressoItemId));

        var receiptId = await IdOf(await client.PostAsJsonAsync("/api/v1/inventory/transfer-receipt", new
        {
            relatedWarehouseDocumentId = orderId, branchId = (long?)null, documentDate = Today, destinationWarehouseId = ids.BranchWarehouseId,
            notes = (string?)null, lines = new[] { new { itemId = ids.EspressoItemId, quantity = 2m, batchNumber = (string?)null, expiryDate = (DateOnly?)null } }
        }));
        var receipt = await client.GetFromJsonAsync<JsonElement>($"/api/v1/inventory/transfer-receipt/{receiptId}");
        Assert.Equal(bag, receipt.GetProperty("lines")[0].GetProperty("unitId").GetInt64());
        Assert.Equal(ids.BranchId, receipt.GetProperty("branchId").GetInt64());   // the destination warehouse's branch

        (await client.PostAsync($"/api/v1/inventory/transfer-receipt/{receiptId}/post", null)).EnsureSuccessStatusCode();
        Assert.Equal(2000m, await CycleSeed.GetBalanceAsync(factory, ids.CompanyId, ids.BranchWarehouseId, ids.EspressoItemId));
    }

    [Fact]
    public async Task A_branch_request_is_limited_in_base_units_and_approved_into_the_same_unit()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var bag = await AddBagAsync(ids);
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            db.BranchItemLimits.Add(new BranchItemLimit { BranchId = ids.BranchId, ItemId = ids.EspressoItemId, MaxRequestQuantity = 1500m });
            await db.SaveChangesAsync();
        }

        var client = Client(ids.CompanyId);
        async Task<long> RequestAsync(decimal bags) => await IdOf(await client.PostAsJsonAsync("/api/v1/inventory/branch-requests", new
        {
            branchId = ids.BranchId, requestDate = Today, lines = new[] { new { itemId = ids.EspressoItemId, requestedQuantity = bags, unitId = (long?)bag } }
        }));

        var tooMuch = await RequestAsync(2);   // 2000 g > 1500 g
        Assert.Equal("INV-R4-EXCEEDS-MAX-REQUEST", await ErrorCodeAsync(await client.PostAsync($"/api/v1/inventory/branch-requests/{tooMuch}/submit", null)));

        var requestId = await RequestAsync(1);
        (await client.PostAsync($"/api/v1/inventory/branch-requests/{requestId}/submit", null)).EnsureSuccessStatusCode();
        var request = await client.GetFromJsonAsync<JsonElement>($"/api/v1/inventory/branch-requests/{requestId}");
        var line = request.GetProperty("lines")[0];
        Assert.Equal(bag, line.GetProperty("unitId").GetInt64());

        var approved = await client.PostAsJsonAsync($"/api/v1/inventory/branch-requests/{requestId}/approve", new
        {
            sourceWarehouseId = ids.MainWarehouseId, custodyOfficerId = ids.CustodyOfficerId, transferDocumentDate = Today,
            lines = new[] { new { lineId = line.GetProperty("id").GetInt64(), approvedQuantity = 1m } }
        });
        approved.EnsureSuccessStatusCode();
        var orderId = (await approved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("transferOrderId").GetInt64();
        var orderLine = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/inventory/transfer-order/{orderId}")).GetProperty("lines")[0];
        Assert.Equal(bag, orderLine.GetProperty("unitId").GetInt64());
        Assert.Equal(500m, orderLine.GetProperty("unitCost").GetDecimal());   // standard 0.50 a gram × 1000
    }

    [Fact]
    public async Task A_recipe_line_in_a_larger_unit_is_deducted_in_base_units_at_the_till()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var kilo = await AddBagAsync(ids, 1000m);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId, 2000m);
        await CycleSeed.GiveStockAsync(factory, companyId, ids.BranchWarehouseId, ids.MilkItemId, 8000m);
        await CycleSeed.SetProductionSalesModeAsync(factory, companyId, SettingScopeType.Company, null, ProductionSalesMode.RealTime);
        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            // 9 g of espresso written as 0.009 of a 1000 g unit.
            var line = await db.RecipeLines.SingleAsync(l => l.RecipeId == ids.RecipeId && l.ComponentItemId == ids.EspressoItemId);
            line.UnitId = kilo;
            line.UnitFactor = 1000m;
            line.Quantity = 0.009m;
            await db.SaveChangesAsync();
        }

        var client = Client(companyId);
        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);
        (await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 40m)).EnsureSuccessStatusCode();

        Assert.Equal(2000m - 9m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.BranchWarehouseId, ids.EspressoItemId));
    }

    [Fact]
    public async Task A_recipe_costs_its_lines_in_base_units()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var kilo = await AddBagAsync(ids, 1000m);
        var client = Client(ids.CompanyId);

        var recipeId = await IdOf(await client.PostAsJsonAsync("/api/v1/inventory/recipes", new
        {
            outputItemId = ids.LatteItemId, outputQuantity = 1m, wastePercentage = 0m, effectiveFromDate = Today,
            lines = new[] { new { componentItemId = ids.EspressoItemId, quantity = 0.01m, unitId = (long?)kilo } }
        }));
        var recipe = await client.GetFromJsonAsync<JsonElement>($"/api/v1/inventory/recipes/{recipeId}");

        Assert.Equal(5m, recipe.GetProperty("estimatedComponentCost").GetDecimal());   // 0.01 × 1000 g × 0.50
        Assert.Equal(kilo, recipe.GetProperty("lines")[0].GetProperty("unitId").GetInt64());
    }

    [Fact]
    public async Task A_count_line_counted_in_a_larger_unit_gives_its_variance_in_base_units()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var bag = await AddBagAsync(ids);
        await CycleSeed.GiveStockAsync(factory, ids.CompanyId, ids.BranchWarehouseId, ids.EspressoItemId, 2000m);
        var client = Client(ids.CompanyId);

        var countId = await IdOf(await client.PostAsJsonAsync("/api/v1/inventory/inventory-counts", new
        {
            warehouseId = ids.BranchWarehouseId, countDate = Today, countType = "Partial", itemIds = new[] { ids.EspressoItemId }
        }));
        (await client.PostAsync($"/api/v1/inventory/inventory-counts/{countId}/start", null)).EnsureSuccessStatusCode();
        var lineId = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/inventory/inventory-counts/{countId}")).GetProperty("lines")[0].GetProperty("id").GetInt64();

        (await client.PostAsJsonAsync($"/api/v1/inventory/inventory-counts/{countId}/counted-quantities", new
        {
            lines = new[] { new { lineId, countedQuantity = 1.5m, unitId = (long?)bag } }
        })).EnsureSuccessStatusCode();

        var line = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/inventory/inventory-counts/{countId}")).GetProperty("lines")[0];
        Assert.Equal(1.5m, line.GetProperty("countedQuantity").GetDecimal());
        Assert.Equal(-500m, line.GetProperty("varianceQuantity").GetDecimal());   // 1500 g counted against 2000 g
        Assert.Equal(bag, line.GetProperty("unitId").GetInt64());
    }

    [Fact]
    public async Task The_items_list_carries_each_items_units_and_tax_code()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var bag = await AddBagAsync(ids, 250m);
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            (await db.Items.SingleAsync(i => i.Id == ids.EspressoItemId)).TaxCode = "EG-EGS-123";
            await db.SaveChangesAsync();
        }

        var items = await Client(ids.CompanyId).GetFromJsonAsync<JsonElement>("/api/v1/inventory/items");
        var espresso = items.EnumerateArray().Single(i => i.GetProperty("id").GetInt64() == ids.EspressoItemId);
        var units = espresso.GetProperty("units").EnumerateArray().ToList();

        Assert.Equal("EG-EGS-123", espresso.GetProperty("taxCode").GetString());
        Assert.Equal(ids.GramUnitId, units[0].GetProperty("unitId").GetInt64());
        Assert.Equal(1m, units[0].GetProperty("factor").GetDecimal());
        Assert.Equal(bag, units[1].GetProperty("unitId").GetInt64());
        Assert.Equal(250m, units[1].GetProperty("factor").GetDecimal());

        var detail = await Client(ids.CompanyId).GetFromJsonAsync<JsonElement>($"/api/v1/inventory/items/{ids.EspressoItemId}");
        Assert.Equal("EG-EGS-123", detail.GetProperty("taxCode").GetString());
    }

    // ---------------------------------------------------------------- opening balance lines

    [Fact]
    public async Task An_opening_balance_needs_lines_with_a_quantity_above_zero()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var client = Client(ids.CompanyId);

        Assert.Equal(HttpStatusCode.BadRequest, (await CreateDocAsync(client, "opening-balances", null, ids.BranchWarehouseId, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateDocAsync(client, "opening-balances", null, ids.BranchWarehouseId, null, Line(ids.EspressoItemId, 0, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CreateDocAsync(client, "opening-balances", null, ids.BranchWarehouseId, null, Line(ids.EspressoItemId, -3, 1))).StatusCode);
    }

    // ------------------------------------------------------------------------ branch scope

    [Fact]
    public async Task Warehouse_documents_belong_to_their_warehouses_branch_and_other_branches_do_not_see_them()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var otherBranch = await AddBranchAsync(ids.CompanyId, "OTHER");
        var admin = Client(ids.CompanyId);

        var branchDoc = await IdOf(await CreateDocAsync(admin, "stock-in", null, ids.BranchWarehouseId, null, Line(ids.EspressoItemId, 5, 1)));
        var mainDoc = await IdOf(await CreateDocAsync(admin, "stock-in", null, ids.MainWarehouseId, null, Line(ids.EspressoItemId, 5, 1)));

        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            Assert.Equal(ids.BranchId, (await db.WarehouseDocuments.SingleAsync(d => d.Id == branchDoc)).BranchId);
            Assert.Null((await db.WarehouseDocuments.SingleAsync(d => d.Id == mainDoc)).BranchId);   // main warehouse: company-level
        }

        var ownBranch = await ListIdsAsync(Client(ids.CompanyId, ids.BranchId), "/api/v1/inventory/stock-in");
        var other = await ListIdsAsync(Client(ids.CompanyId, otherBranch), "/api/v1/inventory/stock-in");
        Assert.Contains(branchDoc, ownBranch);
        Assert.Contains(mainDoc, ownBranch);
        Assert.DoesNotContain(branchDoc, other);
        Assert.Contains(mainDoc, other);
    }

    [Fact]
    public async Task Transfer_orders_and_receipts_are_seen_by_their_branch_only()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var otherBranch = await AddBranchAsync(ids.CompanyId, "OTHER");
        await CycleSeed.GiveStockAsync(factory, ids.CompanyId, ids.BranchWarehouseId, ids.EspressoItemId, 1000m);
        await CycleSeed.GiveStockAsync(factory, ids.CompanyId, ids.MainWarehouseId, ids.EspressoItemId, 1000m);
        var admin = Client(ids.CompanyId);

        // Out of the branch warehouse: the branch's own transfer.
        var branchOrder = await IdOf(await CreateDocAsync(admin, "transfer-order", ids.BranchWarehouseId, null, ids.CustodyOfficerId, Line(ids.EspressoItemId, 10, 0)));
        // From the main warehouse into the branch: the receipt is the branch's.
        var mainOrder = await IdOf(await CreateDocAsync(admin, "transfer-order", ids.MainWarehouseId, null, ids.CustodyOfficerId, Line(ids.EspressoItemId, 10, 0)));
        (await admin.PostAsync($"/api/v1/inventory/transfer-order/{mainOrder}/post", null)).EnsureSuccessStatusCode();
        var receipt = await IdOf(await admin.PostAsJsonAsync("/api/v1/inventory/transfer-receipt", new
        {
            relatedWarehouseDocumentId = mainOrder, branchId = (long?)null, documentDate = Today, destinationWarehouseId = ids.BranchWarehouseId,
            notes = (string?)null, lines = new[] { new { itemId = ids.EspressoItemId, quantity = 10m, batchNumber = (string?)null, expiryDate = (DateOnly?)null } }
        }));

        var otherClient = Client(ids.CompanyId, otherBranch);
        var branchClient = Client(ids.CompanyId, ids.BranchId);
        Assert.Contains(branchOrder, await ListIdsAsync(branchClient, "/api/v1/inventory/transfer-order"));
        Assert.DoesNotContain(branchOrder, await ListIdsAsync(otherClient, "/api/v1/inventory/transfer-order"));
        Assert.Contains(receipt, await ListIdsAsync(branchClient, "/api/v1/inventory/transfer-receipt"));
        Assert.DoesNotContain(receipt, await ListIdsAsync(otherClient, "/api/v1/inventory/transfer-receipt"));
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/v1/inventory/transfer-receipt/{receipt}")).StatusCode);
    }

    [Fact]
    public async Task Branch_requests_and_the_branch_list_follow_the_users_branch()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var otherBranch = await AddBranchAsync(ids.CompanyId, "OTHER");
        var admin = Client(ids.CompanyId);

        var request = await IdOf(await admin.PostAsJsonAsync("/api/v1/inventory/branch-requests", new
        {
            branchId = ids.BranchId, requestDate = Today, lines = new[] { new { itemId = ids.EspressoItemId, requestedQuantity = 5m } }
        }));

        Assert.Contains(request, await ListIdsAsync(Client(ids.CompanyId, ids.BranchId), "/api/v1/inventory/branch-requests"));
        Assert.DoesNotContain(request, await ListIdsAsync(Client(ids.CompanyId, otherBranch), "/api/v1/inventory/branch-requests"));

        // The branch picker of a branch-restricted user holds that branch only.
        var branches = await Client(ids.CompanyId, otherBranch).GetFromJsonAsync<JsonElement>("/api/v1/organization/branches");
        Assert.Equal([otherBranch], branches.EnumerateArray().Select(b => b.GetProperty("id").GetInt64()).ToArray());

        // And it cannot raise a request for another branch.
        var foreign = await Client(ids.CompanyId, otherBranch).PostAsJsonAsync("/api/v1/inventory/branch-requests", new
        {
            branchId = ids.BranchId, requestDate = Today, lines = new[] { new { itemId = ids.EspressoItemId, requestedQuantity = 5m } }
        });
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    // --------------------------------------------------------------------- default currency

    [Fact]
    public async Task There_is_always_exactly_one_default_currency()
    {
        var admin = Client(NewCompanyId());
        string Code() => "Q" + (char)('A' + Random.Shared.Next(26)) + (char)('A' + Random.Shared.Next(26));
        var first = await IdOf(await admin.PostAsJsonAsync("/api/v1/organization/currencies", new { code = Code(), nameAr = "أ", nameEn = "A" }));
        var second = await IdOf(await admin.PostAsJsonAsync("/api/v1/organization/currencies", new { code = Code(), nameAr = "ب", nameEn = "B", isDefault = true }));
        long? previousDefault;
        await using (var db = factory.CreateDirectDbContext(0))
        {
            previousDefault = await db.Currencies.Where(c => c.IsDefault).Select(c => (long?)c.Id).SingleOrDefaultAsync();
        }

        try
        {
            Assert.Equal(second, previousDefault);

            // Moving the default takes it off the old one.
            (await admin.PutAsJsonAsync($"/api/v1/organization/currencies/{first}", new { nameAr = "أ", nameEn = "A", isActive = true, isDefault = true })).EnsureSuccessStatusCode();
            await using (var db = factory.CreateDirectDbContext(0))
            {
                Assert.Equal([first], await db.Currencies.Where(c => c.IsDefault).Select(c => c.Id).ToListAsync());
            }

            // The default cannot be switched off, deactivated or deleted.
            Assert.Equal("ORG-CURRENCY-DEFAULT-REQUIRED", await ErrorCodeAsync(
                await admin.PutAsJsonAsync($"/api/v1/organization/currencies/{first}", new { nameAr = "أ", nameEn = "A", isActive = true, isDefault = false })));
            Assert.Equal("ORG-CURRENCY-DEFAULT-INACTIVE", await ErrorCodeAsync(
                await admin.PutAsJsonAsync($"/api/v1/organization/currencies/{first}", new { nameAr = "أ", nameEn = "A", isActive = false })));
            Assert.Equal("ORG-CURRENCY-DEFAULT-REQUIRED", await ErrorCodeAsync(await admin.DeleteAsync($"/api/v1/organization/currencies/{first}")));

            var list = await admin.GetFromJsonAsync<JsonElement>("/api/v1/organization/currencies");
            Assert.True(list.EnumerateArray().Single(c => c.GetProperty("id").GetInt64() == first).GetProperty("isDefault").GetBoolean());

            // Two defaults at once are impossible in the database itself.
            await using var direct = factory.CreateDirectDbContext(0);
            (await direct.Currencies.SingleAsync(c => c.Id == second)).IsDefault = true;
            await Assert.ThrowsAsync<DbUpdateException>(() => direct.SaveChangesAsync());
        }
        finally
        {
            // Leave the shared test database with an ordinary default again.
            await using var db = factory.CreateDirectDbContext(0);
            var egp = await db.Currencies.FirstOrDefaultAsync(c => c.Code == "EGP");
            if (egp is not null)
            {
                (await admin.PutAsJsonAsync($"/api/v1/organization/currencies/{egp.Id}", new { nameAr = egp.NameAr, nameEn = egp.NameEn, isActive = true, isDefault = true })).EnsureSuccessStatusCode();
                await admin.DeleteAsync($"/api/v1/organization/currencies/{first}");
                await admin.DeleteAsync($"/api/v1/organization/currencies/{second}");
            }
        }
    }

    [Fact]
    public async Task An_items_tax_code_is_saved_and_optional()
    {
        var ids = await CycleSeed.SeedAsync(factory, NewCompanyId());
        var client = Client(ids.CompanyId);
        object Body(string? taxCode) => new
        {
            code = (string?)$"TX{ids.CompanyId % 1000000}{Random.Shared.Next(1000)}", nameAr = "صنف", nameEn = "Item", itemGroupId = (long?)null, posCategoryId = (long?)null,
            itemType = "RawMaterial", barcode = (string?)null, baseUnitOfMeasureId = ids.GramUnitId, purchaseUnitOfMeasureId = (long?)null,
            sellUnitOfMeasureId = (long?)null, saleMethod = "ByWeight", costMethod = "WeightedAverage", defaultPrice = (decimal?)null,
            isStocked = true, isTracked = false, trackSerial = false, shelfLifeDays = (int?)null, standardCost = (decimal?)null,
            isPurchasable = true, isSellable = false, isManufacturable = false, allowSubstitutes = false, status = "Active", taxCode
        };

        var withCode = await IdOf(await client.PostAsJsonAsync("/api/v1/inventory/items", Body(" 10001234 ")));
        var without = await IdOf(await client.PostAsJsonAsync("/api/v1/inventory/items", Body(null)));

        Assert.Equal("10001234", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/inventory/items/{withCode}")).GetProperty("taxCode").GetString());
        Assert.Equal(JsonValueKind.Null, (await client.GetFromJsonAsync<JsonElement>($"/api/v1/inventory/items/{without}")).GetProperty("taxCode").ValueKind);
    }
}
