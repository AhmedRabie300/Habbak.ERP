using System.Net.Http.Json;
using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Step 6 of Docs/End-to-End-Cycle-Test.md — selling the finished good at the till. This class
/// covers the money side (invoice, payment, check closure); the stock side is deliberately split
/// out into StockDeductionOnSaleTests because that is where the cycle breaks down.
/// </summary>
public class POSTerminalSaleFlowTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    /// <summary>Opens a shift and an empty takeaway check.</summary>
    internal static async Task<long> OpenCheckAsync(HttpClient client, CycleIds ids)
    {
        var openShift = await client.PostAsJsonAsync("/api/v1/pos/shifts/open", new
        {
            posTerminalId = ids.PosTerminalId,
            cashierUserId = 1L,
            openingCounts = new[] { new { denominationValue = 100m, count = 1 } },
            idempotencyKey = Guid.NewGuid()
        });
        openShift.EnsureSuccessStatusCode();

        // Takeaway, not DineIn: dine-in checks are required to be opened against a table.
        var openCheck = await client.PostAsJsonAsync("/api/v1/pos/checks/open-standalone", new
        {
            posTerminalId = ids.PosTerminalId,
            orderType = "Takeaway",
            customerId = (long?)null
        });
        openCheck.EnsureSuccessStatusCode();
        return (await openCheck.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
    }

    internal static async Task AddLineAsync(HttpClient client, long checkId, long itemId, decimal quantity)
    {
        var addLine = await client.PostAsJsonAsync($"/api/v1/pos/checks/{checkId}/lines", new
        {
            itemId,
            quantity,
            unitPrice = (decimal?)null,
            discountAmount = 0m,
            note = (string?)null
        });
        addLine.EnsureSuccessStatusCode();
    }

    internal static Task AddLatteLineAsync(HttpClient client, CycleIds ids, long checkId, decimal quantity)
        => AddLineAsync(client, checkId, ids.LatteItemId, quantity);

    /// <summary>Opens a shift and a takeaway check with one latte on it, ready to pay.</summary>
    internal static async Task<long> OpenCheckWithLatteAsync(HttpClient client, CycleIds ids, decimal quantity = 1m)
    {
        var checkId = await OpenCheckAsync(client, ids);
        await AddLineAsync(client, checkId, ids.LatteItemId, quantity);
        return checkId;
    }

    internal static async Task<HttpResponseMessage> PayAsync(HttpClient client, CycleIds ids, long checkId, decimal amount)
        => await client.PostAsJsonAsync("/api/v1/pos/invoices/complete-payment", new
        {
            checkId,
            tipAmount = 0m,
            idempotencyKey = Guid.NewGuid(),
            payments = new[] { new { paymentMethodId = ids.PaymentMethodId, amount, amountTendered = amount } }
        });

    [Fact]
    public async Task Selling_a_latte_creates_a_posted_invoice_and_closes_the_check()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);

        var checkId = await OpenCheckWithLatteAsync(client, ids);

        // No BranchPOSSettings row is seeded, so service charge and VAT default to off and the
        // payable total is just the line total.
        var pay = await PayAsync(client, ids, checkId, 40m);
        pay.EnsureSuccessStatusCode();
        var invoiceId = (await pay.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;

        await using var db = factory.CreateDirectDbContext(companyId);

        var invoice = await db.POSInvoices.FirstAsync(i => i.Id == invoiceId);
        Assert.Equal(POSInvoiceStatus.Posted, invoice.Status);   // rule 15: posted straight away
        Assert.Equal(40m, invoice.Total);
        Assert.Equal(checkId, invoice.CheckId);
        Assert.Null(invoice.JournalEntryId);                     // no accounting wired in

        var check = await db.Checks.FirstAsync(c => c.Id == checkId);
        Assert.Equal(CheckStatus.Completed, check.Status);
    }

    [Fact]
    public async Task Adding_a_line_performs_no_stock_or_recipe_availability_check()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);

        // Nothing at all is in stock: no latte, and none of its recipe components. Gap G-5 says
        // the line is still accepted, so the cashier gets no warning at the moment it matters.
        var checkId = await OpenCheckWithLatteAsync(client, ids, quantity: 99m);

        await using var db = factory.CreateDirectDbContext(companyId);
        var lines = await db.CheckLines.Where(l => l.CheckId == checkId).ToListAsync();
        Assert.Single(lines);
        Assert.Equal(99m, lines[0].Quantity);
    }

    private sealed record CreatedResponse(long Id);
}
