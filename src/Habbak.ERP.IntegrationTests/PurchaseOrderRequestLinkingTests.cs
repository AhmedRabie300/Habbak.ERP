using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.CancelPurchaseOrder;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.CreatePurchaseOrder;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.RejectPurchaseOrder;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.UpdatePurchaseOrder;
using Habbak.ERP.Application.Purchasing.PurchaseOrders.Dtos;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Queries.GetOpenPurchaseRequests;
using Habbak.ERP.Application.Purchasing.PurchaseRequests.Queries.GetPurchaseRequestLinesForOrder;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests;

/// <summary>
/// Converting a purchase request through its orders (Docs/My Remarks/Remarks7.md): which requests
/// are still open to convert, the cap on what one order may take off a request, and the request
/// line's OrderedQuantity and status following every create, edit, cancel and reject — the same
/// shape PurchaseInvoiceOrderLinkingTests (ApiTests) already pins for Remarks6's order/invoice link.
/// </summary>
public class PurchaseOrderRequestLinkingTests(PostingServiceFixture fixture) : IClassFixture<PostingServiceFixture>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);
    private static TestCurrentCompanyContext Ctx(long companyId) => new(companyId);

    private sealed record Seed(long CompanyId, long SupplierId, long ItemId, long ExtraItemId, long UnitId, long RequestId, long RequestLineId);

    private async Task<Seed> SeedAsync(decimal requestedQuantity = 1000m, bool allowManualOrderLines = true)
    {
        var companyId = NewCompanyId();
        var suffix = companyId % 100000000;

        await using var db = fixture.CreateContext(Ctx(companyId));

        var unit = new UnitOfMeasure
        {
            CompanyId = companyId, Code = $"R7GM{suffix}", NameAr = "جرام", NameEn = "Gram", Category = UnitCategory.Weight, IsActive = true
        };
        db.UnitsOfMeasure.Add(unit);

        var supplier = new Supplier
        {
            CompanyId = companyId, Code = $"R7SUP{suffix}", NameAr = "مورد الاختبار", NameEn = "Test Supplier",
            PaymentTerms = SupplierPaymentTerms.Net30, CurrencyCode = "EGP", IsActive = true
        };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();

        var item = new Item
        {
            CompanyId = companyId, Code = $"R7ITM{suffix}", NameAr = "بن", NameEn = "Coffee",
            ItemType = ItemType.RawMaterial, BaseUnitOfMeasureId = unit.Id, IsStocked = true, IsPurchasable = true, Status = ItemStatus.Active
        };
        var extraItem = new Item
        {
            CompanyId = companyId, Code = $"R7EXT{suffix}", NameAr = "صنف تاني", NameEn = "Other Item",
            ItemType = ItemType.RawMaterial, BaseUnitOfMeasureId = unit.Id, IsStocked = true, IsPurchasable = true, Status = ItemStatus.Active
        };
        db.Items.AddRange(item, extraItem);
        await db.SaveChangesAsync();

        db.PurchaseCycleSettingsRows.Add(new PurchaseCycleSettings
        {
            CompanyId = companyId,
            RequiresPurchaseRequest = false,
            AllowManualOrderLines = allowManualOrderLines
        });

        var request = new PurchaseRequest
        {
            CompanyId = companyId, RequestNumber = $"R7-REQ-{Random.Shared.Next(100000, 999999)}",
            RequestDate = DateOnly.FromDateTime(DateTime.UtcNow.Date), RequestedByUserId = 1,
            Priority = PurchaseRequestPriority.Normal, Status = PurchaseRequestStatus.Approved
        };
        request.Lines.Add(new PurchaseRequestLine
        {
            ItemId = item.Id, Quantity = requestedQuantity, UnitId = unit.Id, UnitFactor = 1m, BaseQuantity = requestedQuantity
        });
        db.PurchaseRequests.Add(request);
        await db.SaveChangesAsync();

        return new Seed(companyId, supplier.Id, item.Id, extraItem.Id, unit.Id, request.Id, request.Lines.Single().Id);
    }

    private async Task<long> CreateOrderAsync(
        Seed seed, decimal quantity, long? requestLineId, decimal price = 0.5m, IReadOnlyList<PurchaseOrderLineInput>? extraLines = null)
    {
        await using var db = fixture.CreateContext(Ctx(seed.CompanyId));
        var handler = new CreatePurchaseOrderCommandHandler(db, Ctx(seed.CompanyId), new CodeGenerator(db, Ctx(seed.CompanyId)));

        var lines = new List<PurchaseOrderLineInput> { new(seed.ItemId, quantity, price, null, seed.UnitId, null, null, requestLineId) };
        if (extraLines is not null)
        {
            lines.AddRange(extraLines);
        }

        return await handler.Handle(new CreatePurchaseOrderCommand
        {
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            SupplierId = seed.SupplierId,
            PurchaseRequestId = seed.RequestId,
            CurrencyCode = "EGP",
            ExchangeRate = 1m,
            Lines = lines
        }, CancellationToken.None);
    }

    private async Task<PurchaseRequestLine> RequestLineAsync(Seed seed)
    {
        await using var db = fixture.CreateContext(Ctx(seed.CompanyId));
        return await db.PurchaseRequestLines.SingleAsync(l => l.Id == seed.RequestLineId);
    }

    private async Task<PurchaseRequestStatus> RequestStatusAsync(Seed seed)
    {
        await using var db = fixture.CreateContext(Ctx(seed.CompanyId));
        return (await db.PurchaseRequests.SingleAsync(r => r.Id == seed.RequestId)).Status;
    }

    // ================================================================ converting a request

    [Fact]
    public async Task PartialConvert_CreatesPartiallyConvertedStatus()
    {
        var seed = await SeedAsync(1000m);

        await CreateOrderAsync(seed, 400m, seed.RequestLineId);

        Assert.Equal(400m, (await RequestLineAsync(seed)).OrderedQuantity);
        Assert.Equal(PurchaseRequestStatus.PartiallyConverted, await RequestStatusAsync(seed));
    }

    [Fact]
    public async Task FullConvert_CreatesConvertedStatus()
    {
        var seed = await SeedAsync(1000m);

        await CreateOrderAsync(seed, 1000m, seed.RequestLineId);

        Assert.Equal(PurchaseRequestStatus.Converted, await RequestStatusAsync(seed));
    }

    [Fact]
    public async Task ExceedRequestedQuantity_ThrowsQtyExceeds()
    {
        var seed = await SeedAsync(1000m);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateOrderAsync(seed, 1200m, seed.RequestLineId));

        Assert.Equal("PUR-ORDER-QTY-EXCEEDS-REQUEST", ex.Code);
        Assert.Equal(0m, (await RequestLineAsync(seed)).OrderedQuantity);
    }

    // ================================================================ editing, cancelling, rejecting

    [Fact]
    public async Task UpdateOrder_ReversesOldThenAppliesNew()
    {
        var seed = await SeedAsync(1000m);
        var orderId = await CreateOrderAsync(seed, 400m, seed.RequestLineId);

        string rowVersion;
        await using (var db = fixture.CreateContext(Ctx(seed.CompanyId)))
        {
            rowVersion = Convert.ToBase64String((await db.PurchaseOrders.SingleAsync(o => o.Id == orderId)).RowVersion);
        }

        await using (var db = fixture.CreateContext(Ctx(seed.CompanyId)))
        {
            var handler = new UpdatePurchaseOrderCommandHandler(db, Ctx(seed.CompanyId));
            await handler.Handle(new UpdatePurchaseOrderCommand
            {
                Id = orderId,
                RowVersion = rowVersion,
                OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                SupplierId = seed.SupplierId,
                CurrencyCode = "EGP",
                ExchangeRate = 1m,
                Lines = [new PurchaseOrderLineInput(seed.ItemId, 900m, 0.5m, null, seed.UnitId, null, null, seed.RequestLineId)]
            }, CancellationToken.None);
        }

        Assert.Equal(900m, (await RequestLineAsync(seed)).OrderedQuantity);
        Assert.Equal(PurchaseRequestStatus.PartiallyConverted, await RequestStatusAsync(seed));
    }

    [Fact]
    public async Task CancelOrder_ReturnsQuantityToRequest()
    {
        var seed = await SeedAsync(1000m);
        var orderId = await CreateOrderAsync(seed, 1000m, seed.RequestLineId);
        Assert.Equal(PurchaseRequestStatus.Converted, await RequestStatusAsync(seed));

        await using var db = fixture.CreateContext(Ctx(seed.CompanyId));
        await new CancelPurchaseOrderCommandHandler(db).Handle(new CancelPurchaseOrderCommand(orderId), CancellationToken.None);

        Assert.Equal(0m, (await RequestLineAsync(seed)).OrderedQuantity);
        Assert.Equal(PurchaseRequestStatus.Approved, await RequestStatusAsync(seed));
    }

    [Fact]
    public async Task RejectOrder_ReturnsQuantityToRequest()
    {
        var seed = await SeedAsync(1000m);
        var orderId = await CreateOrderAsync(seed, 600m, seed.RequestLineId);
        Assert.Equal(PurchaseRequestStatus.PartiallyConverted, await RequestStatusAsync(seed));

        await using var db = fixture.CreateContext(Ctx(seed.CompanyId));
        await new RejectPurchaseOrderCommandHandler(db).Handle(new RejectPurchaseOrderCommand(orderId), CancellationToken.None);

        Assert.Equal(0m, (await RequestLineAsync(seed)).OrderedQuantity);
        Assert.Equal(PurchaseRequestStatus.Approved, await RequestStatusAsync(seed));
    }

    /// <summary>Reject gives the quantity all the way back, so the exact same amount can be reordered.</summary>
    [Fact]
    public async Task RejectThenReorderSameQuantity_Succeeds()
    {
        var seed = await SeedAsync(1000m);
        var firstOrderId = await CreateOrderAsync(seed, 600m, seed.RequestLineId);

        await using (var db = fixture.CreateContext(Ctx(seed.CompanyId)))
        {
            await new RejectPurchaseOrderCommandHandler(db).Handle(new RejectPurchaseOrderCommand(firstOrderId), CancellationToken.None);
        }

        var secondOrderId = await CreateOrderAsync(seed, 600m, seed.RequestLineId);

        Assert.True(secondOrderId > 0);
        Assert.Equal(600m, (await RequestLineAsync(seed)).OrderedQuantity);
        Assert.Equal(PurchaseRequestStatus.PartiallyConverted, await RequestStatusAsync(seed));
    }

    // ================================================================ the pickers

    [Fact]
    public async Task PartiallyConvertedAppearsInOpenForOrder()
    {
        var seed = await SeedAsync(1000m);
        await CreateOrderAsync(seed, 400m, seed.RequestLineId);

        await using var db = fixture.CreateContext(Ctx(seed.CompanyId));
        var result = await new GetOpenPurchaseRequestsQueryHandler(db).Handle(new GetOpenPurchaseRequestsQuery(), CancellationToken.None);

        var open = Assert.Single(result);
        Assert.Equal(seed.RequestId, open.Id);
        Assert.Equal(1, open.RemainingLineCount);
    }

    [Fact]
    public async Task FullyConvertedRequestDisappearsFromOpenList()
    {
        var seed = await SeedAsync(1000m);
        await CreateOrderAsync(seed, 1000m, seed.RequestLineId);

        await using var db = fixture.CreateContext(Ctx(seed.CompanyId));
        var result = await new GetOpenPurchaseRequestsQueryHandler(db).Handle(new GetOpenPurchaseRequestsQuery(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(PurchaseRequestStatus.Rejected)]
    [InlineData(PurchaseRequestStatus.Cancelled)]
    public async Task CancelledOrRejectedRequestNotInOpenList(PurchaseRequestStatus status)
    {
        var seed = await SeedAsync(1000m);
        await using (var db = fixture.CreateContext(Ctx(seed.CompanyId)))
        {
            (await db.PurchaseRequests.SingleAsync(r => r.Id == seed.RequestId)).Status = status;
            await db.SaveChangesAsync();
        }

        await using var read = fixture.CreateContext(Ctx(seed.CompanyId));
        var result = await new GetOpenPurchaseRequestsQueryHandler(read).Handle(new GetOpenPurchaseRequestsQuery(), CancellationToken.None);

        Assert.Empty(result);
    }

    // ================================================================ the manual-line setting

    [Fact]
    public async Task ManualLine_RejectedWhenSettingOff()
    {
        var seed = await SeedAsync(1000m, allowManualOrderLines: false);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateOrderAsync(
            seed, 400m, seed.RequestLineId,
            extraLines: [new PurchaseOrderLineInput(seed.ExtraItemId, 5m, 1m, null, seed.UnitId, null, null, null)]));

        Assert.Equal("PUR-ORDER-MANUAL-LINE-NOT-ALLOWED", ex.Code);
    }

    [Fact]
    public async Task ManualLine_AcceptedWhenSettingOn()
    {
        var seed = await SeedAsync(1000m, allowManualOrderLines: true);

        await CreateOrderAsync(
            seed, 400m, seed.RequestLineId,
            extraLines: [new PurchaseOrderLineInput(seed.ExtraItemId, 5m, 1m, null, seed.UnitId, null, null, null)]);

        // Only the request-linked line converts the request — the manual line rides along untouched.
        Assert.Equal(400m, (await RequestLineAsync(seed)).OrderedQuantity);
    }

    [Fact]
    public async Task LineFromDifferentRequest_ThrowsNotInRequest()
    {
        var seedA = await SeedAsync(1000m);
        var seedB = await SeedAsync(1000m);

        // seedB's request line, pointed at from an order created against seedA's request.
        var mixed = new Seed(seedA.CompanyId, seedA.SupplierId, seedA.ItemId, seedA.ExtraItemId, seedA.UnitId, seedA.RequestId, seedB.RequestLineId);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateOrderAsync(mixed, 100m, mixed.RequestLineId));

        Assert.Equal("PUR-ORDER-LINE-NOT-IN-REQUEST", ex.Code);
    }

    // ================================================================ unit conversion

    /// <summary>A bag of 1 000 g ordered against a request in grams counts as the 1 000 g it is (mirrors
    /// Remarks6's own PurchaseInvoiceOrderLinkingTests.A_line_in_another_unit_is_counted_through_its_base_quantity).</summary>
    [Fact]
    public async Task DifferentUnitConversion_BaseQuantityCorrect()
    {
        var seed = await SeedAsync(1000m);
        long bagId;
        await using (var db = fixture.CreateContext(Ctx(seed.CompanyId)))
        {
            var bag = new UnitOfMeasure
            {
                CompanyId = seed.CompanyId, Code = $"R7BAG{seed.CompanyId % 100000000}", NameAr = "شكارة", NameEn = "Bag",
                Category = UnitCategory.Weight, IsActive = true
            };
            db.UnitsOfMeasure.Add(bag);
            await db.SaveChangesAsync();
            db.ItemUnitConversions.Add(new ItemUnitConversion
            {
                ItemId = seed.ItemId, AlternateUnitOfMeasureId = bag.Id, ConversionFactor = 1000m
            });
            await db.SaveChangesAsync();
            bagId = bag.Id;
        }

        await using (var db = fixture.CreateContext(Ctx(seed.CompanyId)))
        {
            var handler = new CreatePurchaseOrderCommandHandler(db, Ctx(seed.CompanyId), new CodeGenerator(db, Ctx(seed.CompanyId)));
            await handler.Handle(new CreatePurchaseOrderCommand
            {
                OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                SupplierId = seed.SupplierId,
                PurchaseRequestId = seed.RequestId,
                CurrencyCode = "EGP",
                ExchangeRate = 1m,
                Lines = [new PurchaseOrderLineInput(seed.ItemId, 1m, 500m, null, bagId, null, null, seed.RequestLineId)]
            }, CancellationToken.None);
        }

        // One bag = 1 000 g of the 1 000 g requested, so the request is fully converted.
        Assert.Equal(1000m, (await RequestLineAsync(seed)).OrderedQuantity);
        Assert.Equal(PurchaseRequestStatus.Converted, await RequestStatusAsync(seed));
    }
}
