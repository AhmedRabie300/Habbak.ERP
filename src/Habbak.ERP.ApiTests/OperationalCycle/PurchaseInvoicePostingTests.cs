using System.Net;
using System.Net.Http.Json;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Stage 2 of the posting engine (Docs/Posting-Engine-Implementation-Plan.md): the purchase invoice
/// is the first document that posts through a template, and cancelling a posted one reverses it.
///
/// The template is created through the API with the shape the finance manager is expected to use:
///   Dr Inventory   NetAmount (subtotal − discount)
///   Dr VAT         TaxAmount — only when there is tax
///   Cr Supplier    TotalAmount — supplier's own account, else the company default
/// </summary>
public class PurchaseInvoicePostingTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private const string InvoicesUrl = "/api/v1/purchasing/purchase-invoices";

    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private sealed record Accounts(long Inventory, long Vat, long Payable);

    private async Task<Accounts> SeedAccountsAsync(long companyId, bool mapVat = true)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        Account New(string name, AccountType type) => new()
        {
            CompanyId = companyId, Code = $"T-{Guid.NewGuid():N}"[..20], NameAr = name, NameEn = name, AccountType = type,
            Nature = type == AccountType.Asset ? AccountNature.Debit : AccountNature.Credit, IsPostable = true, IsActive = true
        };
        var inventory = New("المخزون", AccountType.Asset);
        var vat = New("ضريبة القيمة المضافة - مدخلات", AccountType.Asset);
        var payable = New("الموردين", AccountType.Liability);
        db.Accounts.AddRange(inventory, vat, payable);
        await db.SaveChangesAsync();

        db.CompanyAccountMappings.Add(new CompanyAccountMapping { CompanyId = companyId, Role = CompanyAccountRole.DefaultPayable, AccountId = payable.Id });
        if (mapVat)
        {
            db.CompanyAccountMappings.Add(new CompanyAccountMapping { CompanyId = companyId, Role = CompanyAccountRole.VatReceivable, AccountId = vat.Id });
        }
        await db.SaveChangesAsync();
        return new Accounts(inventory.Id, vat.Id, payable.Id);
    }

    private static async Task CreateTemplateAsync(HttpClient client, Accounts accounts)
    {
        var response = await client.PostAsJsonAsync("/api/v1/accounting/posting-templates", new
        {
            screenCode = "PURCHASING_PURCHASE_INVOICE",
            definition = new
            {
                nameAr = "فاتورة مشتريات",
                nameEn = "Purchase invoice",
                lines = new object[]
                {
                    new
                    {
                        lineNumber = 1, direction = "Debit", accountSourceType = "Fixed", fixedAccountId = accounts.Inventory,
                        amountFormulaType = "DirectField", amountFieldName = "NetAmount", conditionType = "None",
                        costCenters = Array.Empty<object>()
                    },
                    new
                    {
                        lineNumber = 2, direction = "Debit", accountSourceType = "FromCompany", accountResolverKey = "VatReceivable",
                        amountFormulaType = "DirectField", amountFieldName = "TaxAmount",
                        conditionType = "FieldGreaterThanZero", conditionFieldName = "TaxAmount",
                        costCenters = Array.Empty<object>()
                    },
                    new
                    {
                        lineNumber = 3, direction = "Credit", accountSourceType = "Resolver", accountResolverKey = "Supplier.PayableAccountId",
                        amountFormulaType = "DirectField", amountFieldName = "TotalAmount", conditionType = "None",
                        costCenters = Array.Empty<object>()
                    }
                }
            }
        });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>2 500 of espresso, 100 discount, 14% VAT on the net: 2 400 + 336 = 2 736.</summary>
    private async Task<long> SeedDraftInvoiceAsync(CycleIds ids, decimal tax = 336m)
    {
        var id = await CycleSeed.SeedPostedInvoiceAsync(factory, ids, espressoQty: 5000m, espressoPrice: 0.50m, status: PurchaseInvoiceStatus.Draft);
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var invoice = await db.PurchaseInvoices.SingleAsync(i => i.Id == id);
        invoice.DiscountAmount = 100m;
        invoice.TaxAmount = tax;
        invoice.TotalAmount = invoice.Subtotal - 100m + tax;
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<(CycleIds Ids, Accounts Accounts, HttpClient Client)> ArrangeAsync(bool withTemplate = true, bool mapVat = true)
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var accounts = await SeedAccountsAsync(companyId, mapVat);
        var client = CreateClient(companyId);
        if (withTemplate)
        {
            await CreateTemplateAsync(client, accounts);
        }
        return (ids, accounts, client);
    }

    private sealed record InvoiceDetail(string Status, long? JournalEntryId, string? JournalEntryNumber,
        long? ReversalJournalEntryId, string? ReversalJournalEntryNumber);

    private static Task<InvoiceDetail?> GetInvoiceAsync(HttpClient client, long id) =>
        client.GetFromJsonAsync<InvoiceDetail>($"{InvoicesUrl}/{id}");

    // ------------------------------------------------------------------ posting

    [Fact]
    public async Task Without_a_template_the_invoice_posts_as_before_with_no_entry()
    {
        var (ids, _, client) = await ArrangeAsync(withTemplate: false);
        var invoiceId = await SeedDraftInvoiceAsync(ids);

        (await client.PostAsync($"{InvoicesUrl}/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        var invoice = await GetInvoiceAsync(client, invoiceId);
        Assert.Equal("Posted", invoice!.Status);
        Assert.Null(invoice.JournalEntryId);
    }

    [Fact]
    public async Task Posting_the_invoice_posts_its_entry_from_the_template()
    {
        var (ids, accounts, client) = await ArrangeAsync();
        var invoiceId = await SeedDraftInvoiceAsync(ids);

        (await client.PostAsync($"{InvoicesUrl}/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        var invoice = await GetInvoiceAsync(client, invoiceId);
        Assert.NotNull(invoice!.JournalEntryId);
        Assert.NotNull(invoice.JournalEntryNumber);

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var entry = await db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == invoice.JournalEntryId);

        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.True(entry.IsAutoGenerated);
        Assert.Equal(SourceModule.Purchasing, entry.SourceModule);
        Assert.Equal(invoiceId, entry.SourceDocumentId);
        Assert.Equal(ids.BranchId, entry.BranchId);

        Assert.Equal(2400m, entry.Lines.Single(l => l.AccountId == accounts.Inventory).DebitAmount);
        Assert.Equal(336m, entry.Lines.Single(l => l.AccountId == accounts.Vat).DebitAmount);
        Assert.Equal(2736m, entry.Lines.Single(l => l.AccountId == accounts.Payable).CreditAmount);
        Assert.Equal(2736m, entry.TotalDebit);
        Assert.Equal(2736m, entry.TotalCredit);
    }

    [Fact]
    public async Task An_invoice_without_tax_skips_the_vat_line()
    {
        var (ids, accounts, client) = await ArrangeAsync(mapVat: false);
        var invoiceId = await SeedDraftInvoiceAsync(ids, tax: 0m);

        (await client.PostAsync($"{InvoicesUrl}/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var entry = await db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.SourceDocumentId == invoiceId);
        Assert.Equal(2, entry.Lines.Count);
        Assert.DoesNotContain(entry.Lines, l => l.AccountId == accounts.Vat);
    }

    [Fact]
    public async Task When_the_entry_cannot_be_built_the_invoice_is_not_posted_either()
    {
        // Taxed invoice, VAT role never mapped by the finance manager.
        var (ids, _, client) = await ArrangeAsync(mapVat: false);
        var invoiceId = await SeedDraftInvoiceAsync(ids);

        var response = await client.PostAsync($"{InvoicesUrl}/{invoiceId}/post", null);

        Assert.False(response.IsSuccessStatusCode);
        Assert.Contains("POST-ACCOUNT-UNRESOLVED", await response.Content.ReadAsStringAsync());

        var invoice = await GetInvoiceAsync(client, invoiceId);
        Assert.Equal("Draft", invoice!.Status);
        Assert.Null(invoice.JournalEntryId);

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.False(await db.SupplierPriceHistories.AnyAsync(h => h.PurchaseInvoiceId == invoiceId));
    }

    // ------------------------------------------------------------------ cancelling

    [Fact]
    public async Task Cancelling_a_posted_invoice_posts_a_reversing_entry()
    {
        var (ids, accounts, client) = await ArrangeAsync();
        var invoiceId = await SeedDraftInvoiceAsync(ids);
        (await client.PostAsync($"{InvoicesUrl}/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        (await client.PostAsync($"{InvoicesUrl}/{invoiceId}/cancel", null)).EnsureSuccessStatusCode();

        var invoice = await GetInvoiceAsync(client, invoiceId);
        Assert.Equal("Cancelled", invoice!.Status);
        Assert.NotNull(invoice.ReversalJournalEntryId);
        Assert.NotNull(invoice.ReversalJournalEntryNumber);

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var original = await db.JournalEntries.SingleAsync(e => e.Id == invoice.JournalEntryId);
        var reversal = await db.JournalEntries.Include(e => e.Lines).SingleAsync(e => e.Id == invoice.ReversalJournalEntryId);

        Assert.Equal(JournalEntryStatus.Posted, original.Status); // the original stays on the books
        Assert.Equal(JournalEntryStatus.Posted, reversal.Status);
        Assert.Equal(original.Id, reversal.ReversalOfEntryId);
        Assert.Equal(SourceModule.Purchasing, reversal.SourceModule);
        Assert.Equal(invoiceId, reversal.SourceDocumentId);

        Assert.Equal(2400m, reversal.Lines.Single(l => l.AccountId == accounts.Inventory).CreditAmount);
        Assert.Equal(336m, reversal.Lines.Single(l => l.AccountId == accounts.Vat).CreditAmount);
        Assert.Equal(2736m, reversal.Lines.Single(l => l.AccountId == accounts.Payable).DebitAmount);

        // Net effect on every account is zero.
        var net = await db.JournalEntryLines
            .Where(l => l.JournalEntryId == original.Id || l.JournalEntryId == reversal.Id)
            .GroupBy(l => l.AccountId)
            .Select(g => g.Sum(l => l.DebitAmount - l.CreditAmount))
            .ToListAsync();
        Assert.All(net, n => Assert.Equal(0m, n));
    }

    [Fact]
    public async Task Cancelling_a_posted_invoice_that_never_had_an_entry_just_cancels_it()
    {
        var (ids, _, client) = await ArrangeAsync(withTemplate: false);
        var invoiceId = await SeedDraftInvoiceAsync(ids);
        (await client.PostAsync($"{InvoicesUrl}/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        (await client.PostAsync($"{InvoicesUrl}/{invoiceId}/cancel", null)).EnsureSuccessStatusCode();

        var invoice = await GetInvoiceAsync(client, invoiceId);
        Assert.Equal("Cancelled", invoice!.Status);
        Assert.Null(invoice.ReversalJournalEntryId);
    }

    [Fact]
    public async Task A_paid_invoice_cannot_be_cancelled()
    {
        var (ids, _, client) = await ArrangeAsync();
        var invoiceId = await SeedDraftInvoiceAsync(ids);
        (await client.PostAsync($"{InvoicesUrl}/{invoiceId}/post", null)).EnsureSuccessStatusCode();
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            var invoice = await db.PurchaseInvoices.SingleAsync(i => i.Id == invoiceId);
            invoice.AmountPaid = 1000m;
            invoice.Status = PurchaseInvoiceStatus.PartiallyPaid;
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsync($"{InvoicesUrl}/{invoiceId}/cancel", null);

        Assert.Contains("PUR-INVOICE-HAS-PAYMENTS", await response.Content.ReadAsStringAsync());
        await using var check = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.False(await check.JournalEntries.AnyAsync(e => e.ReversalOfEntryId != null && e.SourceDocumentId == invoiceId));
    }

    [Fact]
    public async Task An_invoice_whose_goods_were_received_cannot_be_cancelled()
    {
        var (ids, _, client) = await ArrangeAsync();
        var invoiceId = await SeedDraftInvoiceAsync(ids);
        (await client.PostAsync($"{InvoicesUrl}/{invoiceId}/post", null)).EnsureSuccessStatusCode();
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            db.GoodsReceipts.Add(new GoodsReceipt
            {
                CompanyId = ids.CompanyId, BranchId = ids.BranchId, WarehouseId = ids.MainWarehouseId,
                ReceiptNumber = $"GR-{Random.Shared.Next(100000, 999999)}", ReceiptDate = DateOnly.FromDateTime(DateTime.UtcNow),
                SupplierId = ids.SupplierId, PurchaseInvoiceId = invoiceId, Status = GoodsReceiptStatus.Posted
            });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsync($"{InvoicesUrl}/{invoiceId}/cancel", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("PUR-INVOICE-HAS-RECEIPT", await response.Content.ReadAsStringAsync());
        Assert.Equal("Posted", (await GetInvoiceAsync(client, invoiceId))!.Status);
    }

    [Fact]
    public async Task Cancelling_takes_the_auto_drafted_receipt_with_it()
    {
        var (ids, _, client) = await ArrangeAsync();
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            db.PurchaseCycleSettingsRows.Add(new PurchaseCycleSettings { CompanyId = ids.CompanyId, AutoCreateReceiptOnInvoicePost = true });
            await db.SaveChangesAsync();
        }
        var invoiceId = await SeedDraftInvoiceAsync(ids);
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            (await db.PurchaseInvoices.SingleAsync(i => i.Id == invoiceId)).WarehouseId = ids.MainWarehouseId;
            await db.SaveChangesAsync();
        }
        (await client.PostAsync($"{InvoicesUrl}/{invoiceId}/post", null)).EnsureSuccessStatusCode();

        (await client.PostAsync($"{InvoicesUrl}/{invoiceId}/cancel", null)).EnsureSuccessStatusCode();

        await using var check = factory.CreateDirectDbContext(ids.CompanyId);
        var receipt = await check.GoodsReceipts.SingleAsync(r => r.PurchaseInvoiceId == invoiceId);
        Assert.Equal(GoodsReceiptStatus.Cancelled, receipt.Status);
    }
}
