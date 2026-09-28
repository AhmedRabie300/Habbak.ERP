using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Two properties of the request/order linking (Docs/My Remarks/Remarks7.md) that only show up
/// against a real database, not the handler-level checks in PurchaseOrderRequestLinkingTests
/// (IntegrationTests):
///
/// 1. Backward compatibility — the migration's backfill SQL correctly links pre-existing order
///    lines to their request line and totals up OrderedQuantity, for data that existed before the
///    PurchaseRequestLineId/OrderedQuantity columns did.
/// 2. Consistency — GetPurchaseOrderSourceRequestQuery (the "source request" panel) and
///    PurchaseOrderRequestLinking.PlanAsync (the create-order cap) must agree on exactly how much
///    of a request is left, since both now read the same persisted OrderedQuantity.
/// </summary>
public class PurchaseOrderRequestLinkingBackfillTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private HttpClient Client(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private static async Task<long> IdOf(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errorCode").GetString()!;

    private sealed record Seed(long CompanyId, long SupplierId, long ItemId, long UnitId);

    private async Task<Seed> SeedAsync()
    {
        var companyId = Random.Shared.NextInt64(1, long.MaxValue);
        var suffix = companyId % 100000000;
        await using var db = factory.CreateDirectDbContext(companyId);

        var unit = new UnitOfMeasure
        {
            CompanyId = companyId, Code = $"R7BGM{suffix}", NameAr = "جرام", NameEn = "Gram", Category = UnitCategory.Weight, IsActive = true
        };
        db.UnitsOfMeasure.Add(unit);

        var supplier = new Supplier
        {
            CompanyId = companyId, Code = $"R7BSUP{suffix}", NameAr = "مورد الاختبار", NameEn = "Test Supplier",
            PaymentTerms = SupplierPaymentTerms.Net30, CurrencyCode = "EGP", IsActive = true
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var item = new Item
        {
            CompanyId = companyId, Code = $"R7BITM{suffix}", NameAr = "بن", NameEn = "Coffee",
            ItemType = ItemType.RawMaterial, BaseUnitOfMeasureId = unit.Id, IsStocked = true, IsPurchasable = true, Status = ItemStatus.Active
        };
        db.Items.Add(item);

        db.PurchaseCycleSettingsRows.Add(new PurchaseCycleSettings { CompanyId = companyId, RequiresPurchaseRequest = false });

        await db.SaveChangesAsync();
        return new Seed(companyId, supplier.Id, item.Id, unit.Id);
    }

    // ================================================================ 1. backward compatibility

    /// <summary>
    /// A request and an order the way they would have looked right before this migration: the order
    /// line carries no PurchaseRequestLineId and the request line's OrderedQuantity is still 0, exactly
    /// what a pre-Remarks7 database has. Running the migration's own backfill SQL against that data
    /// must link the order line to its one matching request line and total up the quantity — the same
    /// three-step SQL the migration runs, exercised here directly since the shared test database is
    /// already past that migration by the time this test starts.
    /// </summary>
    [Fact]
    public async Task Backfill_MatchesExistingOrderAgainstItsRequestLine()
    {
        var seed = await SeedAsync();
        long requestId, requestLineId, orderId, orderLineId;

        await using (var db = factory.CreateDirectDbContext(seed.CompanyId))
        {
            var request = new PurchaseRequest
            {
                CompanyId = seed.CompanyId, RequestNumber = $"R7B-REQ-{Random.Shared.Next(100000, 999999)}",
                RequestDate = Today, RequestedByUserId = 1, Priority = PurchaseRequestPriority.Normal,
                Status = PurchaseRequestStatus.Approved
            };
            request.Lines.Add(new PurchaseRequestLine { ItemId = seed.ItemId, Quantity = 1000m, UnitId = seed.UnitId, UnitFactor = 1m, BaseQuantity = 1000m });
            db.PurchaseRequests.Add(request);
            await db.SaveChangesAsync();
            requestId = request.Id;
            requestLineId = request.Lines.Single().Id;

            // Pre-migration shape: PurchaseRequestId is set on the order (that FK already existed,
            // Remarks4), but the order LINE carries no PurchaseRequestLineId and the request line's
            // OrderedQuantity is untouched — exactly what a row saved before Remarks7 looks like.
            var order = new PurchaseOrder
            {
                CompanyId = seed.CompanyId, OrderNumber = $"R7B-PO-{Random.Shared.Next(100000, 999999)}",
                OrderDate = Today, SupplierId = seed.SupplierId, PurchaseRequestId = requestId,
                CurrencyCode = "EGP", ExchangeRate = 1m, PaymentTerms = SupplierPaymentTerms.Net30,
                Status = PurchaseOrderStatus.Confirmed, Subtotal = 400m, TotalAmount = 400m
            };
            order.Lines.Add(new PurchaseOrderLine
            {
                LineNumber = 1, ItemId = seed.ItemId, Quantity = 400m, UnitPrice = 1m, TotalPrice = 400m,
                UnitId = seed.UnitId, UnitFactor = 1m, BaseQuantity = 400m, BaseUnitCost = 1m
                // PurchaseRequestLineId intentionally left null.
            });
            db.PurchaseOrders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id;
            orderLineId = order.Lines.Single().Id;
        }

        await using (var db = factory.CreateDirectDbContext(seed.CompanyId))
        {
            // The migration's own three SQL steps (20260925141556_PurchaseOrderRequestLinking.Up),
            // run here directly against the already-created columns.
            await db.Database.ExecuteSqlRawAsync(@"
UPDATE ol
SET ol.PurchaseRequestLineId = match.RequestLineId
FROM PurchaseOrderLines ol
JOIN PurchaseOrders o ON o.Id = ol.PurchaseOrderId
CROSS APPLY (
    SELECT MIN(rl.Id) AS RequestLineId, COUNT(*) AS LineCount
    FROM PurchaseRequestLines rl
    WHERE rl.PurchaseRequestId = o.PurchaseRequestId AND rl.ItemId = ol.ItemId AND rl.IsDeleted = 0
) AS match
WHERE ol.PurchaseRequestLineId IS NULL
  AND ol.IsDeleted = 0
  AND o.PurchaseRequestId IS NOT NULL
  AND match.LineCount = 1;");

            await db.Database.ExecuteSqlRawAsync(@"
UPDATE rl
SET rl.OrderedQuantity = ordered.Quantity
FROM PurchaseRequestLines rl
JOIN (
    SELECT ol.PurchaseRequestLineId AS RequestLineId,
           SUM(CASE WHEN rl2.UnitFactor = 0 THEN ol.BaseQuantity ELSE ol.BaseQuantity / rl2.UnitFactor END) AS Quantity
    FROM PurchaseOrderLines ol
    JOIN PurchaseOrders o ON o.Id = ol.PurchaseOrderId
    JOIN PurchaseRequestLines rl2 ON rl2.Id = ol.PurchaseRequestLineId
    WHERE ol.PurchaseRequestLineId IS NOT NULL AND ol.IsDeleted = 0 AND o.IsDeleted = 0
      AND o.Status NOT IN (8, 9)
    GROUP BY ol.PurchaseRequestLineId
) AS ordered ON ordered.RequestLineId = rl.Id;");

            await db.Database.ExecuteSqlRawAsync(@"
UPDATE r
SET r.Status = CASE WHEN totals.UncoveredLines = 0 THEN 4 ELSE 8 END
FROM PurchaseRequests r
JOIN (
    SELECT rl.PurchaseRequestId,
           SUM(CASE WHEN rl.OrderedQuantity > 0 THEN 1 ELSE 0 END) AS CoveredLines,
           SUM(CASE WHEN rl.OrderedQuantity >= rl.Quantity THEN 0 ELSE 1 END) AS UncoveredLines
    FROM PurchaseRequestLines rl
    WHERE rl.IsDeleted = 0
    GROUP BY rl.PurchaseRequestId
) AS totals ON totals.PurchaseRequestId = r.Id
WHERE totals.CoveredLines > 0
  AND r.IsDeleted = 0
  AND r.Status NOT IN (5, 6, 7);");
        }

        await using var check = factory.CreateDirectDbContext(seed.CompanyId);
        Assert.Equal(requestLineId, (await check.PurchaseOrderLines.SingleAsync(l => l.Id == orderLineId)).PurchaseRequestLineId);
        Assert.Equal(400m, (await check.PurchaseRequestLines.SingleAsync(l => l.Id == requestLineId)).OrderedQuantity);
        Assert.Equal(PurchaseRequestStatus.PartiallyConverted, (await check.PurchaseRequests.SingleAsync(r => r.Id == requestId)).Status);

        // The panel reads the very same backfilled column, so it must already agree with what the
        // backfill just computed — no separate "re-sync the panel" step needed.
        var client = Client(seed.CompanyId);
        var sourceRequest = await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-orders/{orderId}/source-request");
        var line = sourceRequest.GetProperty("lines").EnumerateArray().Single();
        Assert.Equal(400m, line.GetProperty("orderedOnThisOrderBaseQuantity").GetDecimal());
        Assert.Equal(600m, line.GetProperty("remainingBaseQuantity").GetDecimal());
    }

    // ================================================================ 2. panel/validation consistency

    /// <summary>
    /// Whatever GetPurchaseOrderSourceRequestQuery reports as remaining is exactly what
    /// PurchaseOrderRequestLinking.PlanAsync will accept on the next order and refuse one unit past —
    /// both now read PurchaseRequestLine.OrderedQuantity directly, so there is only one number to agree
    /// with itself, but this pins that nobody reintroduces a second, drifting computation.
    /// </summary>
    [Fact]
    public async Task Panel_And_Validation_Agree_On_Remaining_Quantity()
    {
        var seed = await SeedAsync();
        var client = Client(seed.CompanyId);

        var requestId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-requests", new
        {
            branchId = 1,
            requestDate = Today,
            priority = "Normal",
            reason = (string?)null,
            notes = (string?)null,
            lines = new[] { new { itemId = seed.ItemId, quantity = 1000m, unitId = seed.UnitId, notes = (string?)null } }
        }));

        await using (var db = factory.CreateDirectDbContext(seed.CompanyId))
        {
            (await db.PurchaseRequests.SingleAsync(r => r.Id == requestId)).Status = PurchaseRequestStatus.Approved;
            await db.SaveChangesAsync();
        }

        var requestLineId = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-requests/{requestId}/order-lines"))
            .EnumerateArray().Single().GetProperty("purchaseRequestLineId").GetInt64();

        object OrderBody(decimal quantity, long? lineId) => new
        {
            orderDate = Today,
            supplierId = seed.SupplierId,
            purchaseRequestId = requestId,
            currencyCode = "EGP",
            exchangeRate = 1m,
            taxAmount = 0m,
            lines = new[] { new { itemId = seed.ItemId, quantity, unitPrice = 1m, unitId = seed.UnitId, purchaseRequestLineId = lineId } }
        };

        var firstOrderId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-orders", OrderBody(400m, requestLineId)));

        var sourceRequest = await client.GetFromJsonAsync<JsonElement>($"/api/v1/purchasing/purchase-orders/{firstOrderId}/source-request");
        var remaining = sourceRequest.GetProperty("lines").EnumerateArray().Single().GetProperty("remainingBaseQuantity").GetDecimal();
        Assert.Equal(600m, remaining);

        // One unit past what the panel reports as remaining — still PartiallyConverted, so this is the
        // quantity cap refusing it, not the status check.
        var overResponse = await client.PostAsJsonAsync("/api/v1/purchasing/purchase-orders", OrderBody(remaining + 1m, requestLineId));
        Assert.Equal(HttpStatusCode.Conflict, overResponse.StatusCode);
        Assert.Equal("PUR-ORDER-QTY-EXCEEDS-REQUEST", await ErrorCodeAsync(overResponse));

        // Exactly what the panel reports as remaining is accepted.
        var secondOrderId = await IdOf(await client.PostAsJsonAsync("/api/v1/purchasing/purchase-orders", OrderBody(remaining, requestLineId)));
        Assert.True(secondOrderId > 0);
    }
}
