using System.Net;
using System.Net.Http.Json;
using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Step 1 of Docs/End-to-End-Cycle-Test.md — a direct purchase invoice with no purchase order.
/// The point of these assertions is that posting an invoice is a *financial* event only: it must
/// not move stock, which is what makes the separate receipt step necessary.
/// </summary>
public class PurchaseInvoiceFlowTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private static async Task AllowDirectInvoicesAsync(HttpClient client)
    {
        var response = await client.PutAsJsonAsync("/api/v1/purchasing/settings/purchase-cycle", new
        {
            // Remarks4 item 6: buying straight off an invoice IS the Direct cycle — the named type
            // now owns these flags, so the test asks for it by name.
            cycleType = "Direct",
            requiresPurchaseRequest = false,
            requiresQuotation = false,
            requiresPurchaseOrder = false,
            requiresGoodsReceipt = true,
            allowInvoiceWithoutOrder = true,
            allowReceiptWithoutInvoice = true,
            autoCreateReceiptOnInvoicePost = false,
            autoCreateInvoiceOnReceipt = false,
            requiresApprovalForPurchaseOrder = false,
            requiresApprovalForInvoice = false,
            defaultPaymentTerms = "Net30",
            capitalizeAdditionalCosts = true
        });
        response.EnsureSuccessStatusCode();
    }

    private static object BuildInvoice(CycleIds ids) => new
    {
        branchId = ids.BranchId,
        invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
        dueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(30),
        supplierId = ids.SupplierId,
        supplierInvoiceNumber = "GOLD-0001",
        purchaseOrderId = (long?)null,
        goodsReceiptId = (long?)null,
        currencyCode = "EGP",
        exchangeRate = 1m,
        paymentTerms = "Net30",
        taxAmount = 0m,
        additionalCosts = 0m,
        lines = new[]
        {
            new { itemId = ids.EspressoItemId, quantity = 5000m, receivedQuantity = 5000m, unitPrice = 0.50m, unitId = ids.GramUnitId },
            new { itemId = ids.MilkItemId,     quantity = 20000m, receivedQuantity = 20000m, unitPrice = 0.02m, unitId = ids.MilliUnitId }
        }
    };

    [Fact]
    public async Task Direct_invoice_is_rejected_when_AllowInvoiceWithoutOrder_is_off()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);

        // No settings row written at all -> PurchaseCycleSettings defaults apply
        // (AllowInvoiceWithoutOrder = false).
        var response = await client.PostAsJsonAsync("/api/v1/purchasing/purchase-invoices", BuildInvoice(ids));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("PUR-INVOICE-ORDER-REQUIRED", body);
    }

    [Fact]
    public async Task Direct_invoice_posts_and_totals_line_amounts()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);
        await AllowDirectInvoicesAsync(client);

        var create = await client.PostAsJsonAsync("/api/v1/purchasing/purchase-invoices", BuildInvoice(ids));
        create.EnsureSuccessStatusCode();
        var invoiceId = (await create.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;

        var post = await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null);
        post.EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);
        var invoice = await db.PurchaseInvoices.FirstAsync(i => i.Id == invoiceId);

        Assert.Equal(PurchaseInvoiceStatus.Posted, invoice.Status);
        Assert.Null(invoice.PurchaseOrderId);
        Assert.Equal(2900m, invoice.TotalAmount);   // 5000*0.50 + 20000*0.02
    }

    [Fact]
    public async Task Posting_an_invoice_moves_no_stock_and_creates_no_goods_receipt()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);
        await AllowDirectInvoicesAsync(client);

        var create = await client.PostAsJsonAsync("/api/v1/purchasing/purchase-invoices", BuildInvoice(ids));
        create.EnsureSuccessStatusCode();
        var invoiceId = (await create.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(companyId);

        Assert.Equal(0, await db.StockTransactions.CountAsync());
        Assert.Equal(0m, await CycleSeed.GetBalanceAsync(factory, companyId, ids.MainWarehouseId, ids.EspressoItemId));

        // AutoCreateReceiptOnInvoicePost is a dead flag (gap G-6): nothing reads it, so no
        // receipt appears here regardless of its value.
        Assert.Equal(0, await db.GoodsReceipts.CountAsync());
    }

    private sealed record CreatedResponse(long Id);
}
