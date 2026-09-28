using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Gaps G-10 and G-9, both about a caller being able to pay a check without guessing.
///
/// G-10: GET /pos/checks/{id} used to report only the line total, while the payment endpoint
/// demanded that plus service charge and VAT — so the documented figure was never the amount due
/// and any consumer but the app's own payment screen got it wrong.
///
/// G-9: an unknown payment method reached the foreign key and surfaced as a bare 500.
/// </summary>
public class CheckPayableTotalTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private sealed record CheckTotals(decimal Total, decimal ServiceChargeAmount, decimal TaxAmount, decimal PayableTotal);

    private static async Task<CheckTotals> GetTotalsAsync(HttpClient client, long checkId)
    {
        var response = await client.GetAsync($"/api/v1/pos/checks/{checkId}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CheckTotals>())!;
    }

    [Fact]
    public async Task The_check_reports_service_charge_vat_and_what_is_actually_payable()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.SetBranchPOSChargesAsync(factory, ids, serviceChargeRate: 12m, vatRate: 14m);
        var client = CreateClient(companyId);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);

        var totals = await GetTotalsAsync(client, checkId);

        Assert.Equal(40m, totals.Total);                  // the lines alone
        Assert.Equal(4.80m, totals.ServiceChargeAmount);  // 12%
        Assert.Equal(5.60m, totals.TaxAmount);            // 14% of the net, not of net + service
        Assert.Equal(50.40m, totals.PayableTotal);
    }

    [Fact]
    public async Task Paying_exactly_the_reported_payable_total_is_accepted()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.SetBranchPOSChargesAsync(factory, ids, serviceChargeRate: 12m, vatRate: 14m);
        var client = CreateClient(companyId);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);
        var totals = await GetTotalsAsync(client, checkId);

        // The whole point: a client reads the figure and pays it, with no arithmetic of its own.
        var pay = await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, totals.PayableTotal);

        pay.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Paying_the_pre_tax_total_is_still_rejected_with_the_figure_to_use()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await CycleSeed.SetBranchPOSChargesAsync(factory, ids, serviceChargeRate: 12m, vatRate: 14m);
        var client = CreateClient(companyId);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);

        var pay = await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 40m);

        Assert.Equal(HttpStatusCode.Conflict, pay.StatusCode);
        var body = await pay.Content.ReadAsStringAsync();
        Assert.Contains("POS-PAYMENT-AMOUNT-MISMATCH", body);
        Assert.Contains("50.40", body);
    }

    [Fact]
    public async Task With_no_branch_settings_payable_equals_the_line_total()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);

        // No settings row seeded at all - neither charge applies, and the preview must agree.
        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);
        var totals = await GetTotalsAsync(client, checkId);

        Assert.Equal(0m, totals.ServiceChargeAmount);
        Assert.Equal(0m, totals.TaxAmount);
        Assert.Equal(totals.Total, totals.PayableTotal);
    }

    [Fact]
    public async Task An_unknown_payment_method_is_a_business_error_not_a_500()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);

        var pay = await client.PostAsJsonAsync("/api/v1/pos/invoices/complete-payment", new
        {
            checkId,
            tipAmount = 0m,
            idempotencyKey = Guid.NewGuid(),
            payments = new[] { new { paymentMethodId = 999_999_999L, amount = 40m, amountTendered = 40m } }
        });

        Assert.NotEqual(HttpStatusCode.InternalServerError, pay.StatusCode);
        Assert.Contains("POS-PAYMENT-METHOD-NOT-FOUND", await pay.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_rejected_payment_method_leaves_the_check_untouched()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var client = CreateClient(companyId);

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);

        await client.PostAsJsonAsync("/api/v1/pos/invoices/complete-payment", new
        {
            checkId,
            tipAmount = 0m,
            idempotencyKey = Guid.NewGuid(),
            payments = new[] { new { paymentMethodId = 999_999_999L, amount = 40m, amountTendered = 40m } }
        });

        // The check must still be payable afterwards - the failed attempt created no invoice and
        // did not close it.
        var retry = await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 40m);
        retry.EnsureSuccessStatusCode();
    }
}
