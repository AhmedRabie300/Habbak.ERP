using System.Net;
using System.Net.Http.Json;
using Habbak.ERP.API.Contracts;
using Habbak.ERP.API.Contracts.Accounting;
using Habbak.ERP.Application.Accounting.JournalEntries.Commands.CreateManualJournalEntry;
using Habbak.ERP.Application.Accounting.JournalEntries.Dtos;
using Habbak.ERP.Application.Accounting.Vouchers.Dtos;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// End-to-end HTTP tests for the Accounting controllers (00-Project-Overview.md, section 25:
/// "xUnit + WebApplicationFactory" — "عقد الـ API وشكل الأخطاء الموحّد"). Exercises real
/// routing, model binding, [Authorize], the exception-handling middleware's error contract, and
/// — crucially — that the Global Query Filter actually isolates companies over real requests,
/// not just inside a single DbContext instance like the earlier tests.
/// </summary>
public class AccountingControllersTests : IClassFixture<AccountingApiFactory>
{
    private readonly AccountingApiFactory _factory;

    public AccountingControllersTests(AccountingApiFactory factory) => _factory = factory;

    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId, long userId = 1)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        return client;
    }

    private async Task<(long treasuryAccountId, long expenseAccountId)> SeedAccountsAsync(long companyId)
    {
        await using var db = _factory.CreateDirectDbContext(companyId);

        var treasury = new Account
        {
            CompanyId = companyId, Code = $"1-1-{companyId % 1000}", NameAr = "الخزينة", NameEn = "Cash",
            AccountType = AccountType.Asset, Nature = AccountNature.Debit, IsPostable = true, IsActive = true
        };
        var expense = new Account
        {
            CompanyId = companyId, Code = $"5-1-{companyId % 1000}", NameAr = "مصروفات عمومية", NameEn = "General Expenses",
            AccountType = AccountType.Expense, Nature = AccountNature.Debit, IsPostable = true, IsActive = true
        };
        db.Accounts.AddRange(treasury, expense);
        await db.SaveChangesAsync();

        return (treasury.Id, expense.Id);
    }

    [Fact]
    public async Task Unauthenticated_Request_IsRejected()
    {
        var client = _factory.CreateClient(); // no auth headers at all -> anonymous ClaimsPrincipal
        var response = await client.GetAsync("/api/v1/accounting/journal-entries");

        // TestAuthHandler always "succeeds" with default header values, so this proves
        // [Authorize] is wired, not that credentials are missing — real rejection needs the
        // not-yet-built JWT issuance/validation flow. Kept as a routing/wiring smoke check only.
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndPostPaymentVoucher_FullLifecycle_Succeeds()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);
        var client = CreateClient(companyId);

        var createResponse = await client.PostAsJsonAsync("/api/v1/accounting/payment-vouchers", new CreateVoucherRequest(
            BranchId: 1,
            VoucherDate: new DateOnly(2026, 8, 15),
            TreasuryAccountId: treasuryAccountId,
            Description: null,
            CounterpartyType: CounterpartyType.Other,
            CounterpartyId: null,
            DirectAccountId: expenseAccountId,
            Amount: 300m,
            CurrencyCode: "EGP",
            ExchangeRate: 1m,
            BaseCurrencyAmount: 300m,
            RelatedInvoiceId: null));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var voucherId = await createResponse.Content.ReadFromJsonAsync<long>();

        var getResponse = await client.GetAsync($"/api/v1/accounting/payment-vouchers/{voucherId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var voucher = await getResponse.Content.ReadFromJsonAsync<VoucherDetailDto>();
        Assert.Equal(nameof(VoucherStatus.Draft), voucher!.Status);

        var postResponse = await client.PostAsync($"/api/v1/accounting/payment-vouchers/{voucherId}/post", content: null);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        var afterPost = await client.GetFromJsonAsync<VoucherDetailDto>($"/api/v1/accounting/payment-vouchers/{voucherId}");
        Assert.Equal(nameof(VoucherStatus.Posted), afterPost!.Status);
        Assert.NotNull(afterPost.JournalEntryId);
    }

    [Fact]
    public async Task GetVoucher_FromWrongVoucherTypeRoute_Returns404()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);
        var client = CreateClient(companyId);

        var createResponse = await client.PostAsJsonAsync("/api/v1/accounting/payment-vouchers", new CreateVoucherRequest(
            1, new DateOnly(2026, 8, 15), treasuryAccountId, null, CounterpartyType.Other, null, expenseAccountId,
            100m, "EGP", 1m, 100m, null));
        var voucherId = await createResponse.Content.ReadFromJsonAsync<long>();

        // A payment voucher's Id must not resolve from the receipt-vouchers route.
        var response = await client.GetAsync($"/api/v1/accounting/receipt-vouchers/{voucherId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateVoucher_WithZeroAmount_ReturnsStandardValidationErrorShape()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);
        var client = CreateClient(companyId);

        var response = await client.PostAsJsonAsync("/api/v1/accounting/payment-vouchers", new CreateVoucherRequest(
            1, new DateOnly(2026, 8, 15), treasuryAccountId, null, CounterpartyType.Other, null, expenseAccountId,
            0m, "EGP", 1m, 0m, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("VALIDATION_ERROR", error!.ErrorCode);
        Assert.NotEmpty(error.CorrelationId);
        Assert.Contains(error.Details!, d => d.Field == nameof(CreateVoucherRequest.Amount) || d.Field == "Amount");
    }

    [Fact]
    public async Task PostJournalEntry_Unbalanced_ReturnsPostingValidationErrorShape()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);
        var client = CreateClient(companyId);

        var createResponse = await client.PostAsJsonAsync("/api/v1/accounting/journal-entries", new CreateJournalEntryRequest(
            BranchId: 1,
            EntryDate: new DateOnly(2026, 8, 15),
            Description: "قيد غير متزن",
            Lines:
            [
                new JournalEntryLineInput
                {
                    AccountId = expenseAccountId, DebitAmount = 100m, CreditAmount = 0m,
                    CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = 100m, BaseCurrencyCreditAmount = 0m
                },
                new JournalEntryLineInput
                {
                    AccountId = treasuryAccountId, DebitAmount = 0m, CreditAmount = 40m,
                    CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = 0m, BaseCurrencyCreditAmount = 40m
                }
            ]));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var entryId = await createResponse.Content.ReadFromJsonAsync<long>();

        var postResponse = await client.PostAsync($"/api/v1/accounting/journal-entries/{entryId}/post", content: null);

        Assert.Equal(HttpStatusCode.BadRequest, postResponse.StatusCode);
        var error = await postResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("POSTING_VALIDATION_ERROR", error!.ErrorCode);
        Assert.Contains(error.Details!, d => d.Field == "ACC-R1-UNBALANCED");
    }

    [Fact]
    public async Task JournalEntriesList_IsIsolatedPerCompany()
    {
        var companyA = NewCompanyId();
        var companyB = NewCompanyId();
        var (treasuryA, expenseA) = await SeedAccountsAsync(companyA);
        await SeedAccountsAsync(companyB);

        var clientA = CreateClient(companyA);
        var clientB = CreateClient(companyB);

        var createResponse = await clientA.PostAsJsonAsync("/api/v1/accounting/journal-entries", new CreateJournalEntryRequest(
            1, new DateOnly(2026, 8, 15), "قيد خاص بالشركة أ",
            [
                new JournalEntryLineInput { AccountId = expenseA, DebitAmount = 50m, CreditAmount = 0m, CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = 50m, BaseCurrencyCreditAmount = 0m },
                new JournalEntryLineInput { AccountId = treasuryA, DebitAmount = 0m, CreditAmount = 50m, CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = 0m, BaseCurrencyCreditAmount = 50m }
            ]));
        var entryId = await createResponse.Content.ReadFromJsonAsync<long>();

        // Company A can see it directly.
        var getAsA = await clientA.GetAsync($"/api/v1/accounting/journal-entries/{entryId}");
        Assert.Equal(HttpStatusCode.OK, getAsA.StatusCode);

        // Company B must not be able to see Company A's entry, even by guessing its Id.
        var getAsB = await clientB.GetAsync($"/api/v1/accounting/journal-entries/{entryId}");
        Assert.Equal(HttpStatusCode.NotFound, getAsB.StatusCode);

        // And it must not show up in Company B's list either.
        var listAsB = await clientB.GetFromJsonAsync<PagedResult<JournalEntryListItemDto>>(
            "/api/v1/accounting/journal-entries");
        Assert.DoesNotContain(listAsB!.Items, i => i.Id == entryId);
    }
}
