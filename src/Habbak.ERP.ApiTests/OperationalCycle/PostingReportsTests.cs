using System.Net.Http.Json;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>The posting reports (00-Posting-Engine-Architecture.md section 10) against one posted and one failed invoice.</summary>
public class PostingReportsTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private const string Url = "/api/v1/accounting/posting-reports";

    private sealed record FailureRow(string ScreenCode, string ErrorCode, bool IsResolved, string? ResolvedByEntryNumber);
    private sealed record AutoEntryRow(string EntryNumber, string SourceModule, string? ScreenCode, int? TemplateVersion);
    private sealed record UnpostedRow(string ScreenCode, bool IsConfigured, int WithoutEntry, int WithoutEntrySinceActivation);
    private sealed record TemplateUsageRow(string ScreenCode, int EntryCount);
    private sealed record BySourceRow(string SourceModule, int EntryCount, decimal Total);

    [Fact]
    public async Task Reports_show_what_posted_what_failed_and_what_is_still_without_an_entry()
    {
        var companyId = Random.Shared.NextInt64(1, long.MaxValue);
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await PostingSeed.MapAllRolesAsync(factory, ids, CompanyAccountRole.VatReceivable);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");

        // Posted before accounting was switched on: expected to have no entry.
        var early = await CycleSeed.SeedPostedInvoiceAsync(factory, ids, 1000m, 0.5m, PurchaseInvoiceStatus.Draft);
        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{early}/post", null)).EnsureSuccessStatusCode();

        await PostingSeed.ActivateDefaultAsync(client, "PURCHASING_PURCHASE_INVOICE");

        var good = await CycleSeed.SeedPostedInvoiceAsync(factory, ids, 1000m, 0.5m, PurchaseInvoiceStatus.Draft);
        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{good}/post", null)).EnsureSuccessStatusCode();

        // Taxed, with the VAT role never mapped: fails, and stays a draft.
        var bad = await CycleSeed.SeedPostedInvoiceAsync(factory, ids, 1000m, 0.5m, PurchaseInvoiceStatus.Draft);
        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            var invoice = await db.PurchaseInvoices.SingleAsync(i => i.Id == bad);
            invoice.TaxAmount = 70m;
            invoice.TotalAmount += 70m;
            await db.SaveChangesAsync();
        }
        Assert.False((await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{bad}/post", null)).IsSuccessStatusCode);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var range = $"from={today.AddDays(-1):yyyy-MM-dd}&to={today.AddDays(1):yyyy-MM-dd}";

        var failures = await client.GetFromJsonAsync<List<FailureRow>>($"{Url}/failures");
        var failure = Assert.Single(failures!);
        Assert.Equal("POST-ACCOUNT-UNRESOLVED", failure.ErrorCode);
        Assert.False(failure.IsResolved);

        var auto = await client.GetFromJsonAsync<List<AutoEntryRow>>($"{Url}/auto-entries?{range}");
        var entry = Assert.Single(auto!);
        Assert.Equal("PURCHASING_PURCHASE_INVOICE", entry.ScreenCode);
        Assert.Equal(1, entry.TemplateVersion);

        var bySource = await client.GetFromJsonAsync<List<BySourceRow>>($"{Url}/by-source?{range}");
        Assert.Equal(500m, bySource!.Single(r => r.SourceModule == "Purchasing").Total);

        var unposted = (await client.GetFromJsonAsync<List<UnpostedRow>>($"{Url}/unposted-documents"))!
            .Single(r => r.ScreenCode == "PURCHASING_PURCHASE_INVOICE");
        Assert.True(unposted.IsConfigured);
        Assert.Equal(1, unposted.WithoutEntry);              // the early one
        Assert.Equal(0, unposted.WithoutEntrySinceActivation);

        var usage = await client.GetFromJsonAsync<List<TemplateUsageRow>>($"{Url}/template-usage");
        Assert.Equal(1, usage!.Single().EntryCount);

        foreach (var path in new[] { $"pos-entries?{range}", $"shift-variances?{range}", "periods", "integrity" })
        {
            (await client.GetAsync($"{Url}/{path}")).EnsureSuccessStatusCode();
        }

        // Fixing the setup and posting again settles the failure against the entry that followed.
        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            var vat = await db.Accounts.FirstAsync(a => a.NameEn == nameof(CompanyAccountRole.VatReceivable));
            db.CompanyAccountMappings.Add(new CompanyAccountMapping { CompanyId = companyId, Role = CompanyAccountRole.VatReceivable, AccountId = vat.Id });
            await db.SaveChangesAsync();
        }
        (await client.PostAsync($"/api/v1/purchasing/purchase-invoices/{bad}/post", null)).EnsureSuccessStatusCode();

        Assert.Empty((await client.GetFromJsonAsync<List<FailureRow>>($"{Url}/failures"))!);
        var resolved = Assert.Single((await client.GetFromJsonAsync<List<FailureRow>>($"{Url}/failures?includeResolved=true"))!);
        Assert.True(resolved.IsResolved);
        Assert.NotNull(resolved.ResolvedByEntryNumber);
    }
}
