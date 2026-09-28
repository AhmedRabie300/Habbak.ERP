using System.Net;
using System.Net.Http.Json;
using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// Stage 0 of the posting engine: the company-level accounts templates resolve by role
/// (Docs/Posting-Engine-Implementation-Plan.md). Mapped by the finance manager, so the tests
/// concentrate on what protects him from himself — wrong account types, summary accounts,
/// and partial saves that must not wipe what he already filled in.
/// </summary>
public class AccountMappingsTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private const string Url = "/api/v1/accounting/settings/account-mappings";

    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private async Task<long> SeedAccountAsync(
        long companyId, AccountType type, bool isPostable = true, bool isActive = true)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        var account = new Account
        {
            CompanyId = companyId,
            Code = $"T-{Guid.NewGuid():N}"[..20],
            NameAr = "حساب اختبار",
            NameEn = "Test account",
            AccountType = type,
            Nature = type is AccountType.Asset or AccountType.Expense ? AccountNature.Debit : AccountNature.Credit,
            IsPostable = isPostable,
            IsActive = isActive
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private sealed record MappingRow(string Role, string ExpectedAccountType, long? AccountId, string? AccountCode);

    private static async Task<Dictionary<string, MappingRow>> GetAsync(HttpClient client)
    {
        var rows = await client.GetFromJsonAsync<List<MappingRow>>(Url);
        return rows!.ToDictionary(r => r.Role);
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, params (string Role, long? AccountId)[] mappings) =>
        client.PutAsJsonAsync(Url, new
        {
            mappings = mappings.Select(m => new { role = m.Role, accountId = m.AccountId }).ToArray()
        });

    [Fact]
    public async Task A_new_company_lists_every_role_as_unmapped()
    {
        var client = CreateClient(NewCompanyId());

        var rows = await GetAsync(client);

        // The screen has to show the gaps, not just what is filled in.
        Assert.Equal(Enum.GetValues<CompanyAccountRole>().Length, rows.Count);
        Assert.All(rows.Values, r => Assert.Null(r.AccountId));
        Assert.Equal("Asset", rows["Cash"].ExpectedAccountType);
        Assert.Equal("Revenue", rows["SalesRevenue"].ExpectedAccountType);
    }

    [Fact]
    public async Task Mapped_roles_are_returned_with_their_account()
    {
        var companyId = NewCompanyId();
        var client = CreateClient(companyId);
        var cash = await SeedAccountAsync(companyId, AccountType.Asset);
        var revenue = await SeedAccountAsync(companyId, AccountType.Revenue);

        (await PutAsync(client, ("Cash", cash), ("SalesRevenue", revenue))).EnsureSuccessStatusCode();

        var rows = await GetAsync(client);
        Assert.Equal(cash, rows["Cash"].AccountId);
        Assert.Equal(revenue, rows["SalesRevenue"].AccountId);
        Assert.NotNull(rows["Cash"].AccountCode);
        Assert.Null(rows["VatPayable"].AccountId);
    }

    [Fact]
    public async Task Saving_some_roles_leaves_the_others_alone()
    {
        var companyId = NewCompanyId();
        var client = CreateClient(companyId);
        var cash = await SeedAccountAsync(companyId, AccountType.Asset);
        var revenue = await SeedAccountAsync(companyId, AccountType.Revenue);

        (await PutAsync(client, ("Cash", cash))).EnsureSuccessStatusCode();
        (await PutAsync(client, ("SalesRevenue", revenue))).EnsureSuccessStatusCode();

        // The second request named only SalesRevenue; a whole-row replace would have wiped Cash.
        var rows = await GetAsync(client);
        Assert.Equal(cash, rows["Cash"].AccountId);
        Assert.Equal(revenue, rows["SalesRevenue"].AccountId);
    }

    [Fact]
    public async Task Null_clears_a_role_and_it_can_be_set_again_afterwards()
    {
        var companyId = NewCompanyId();
        var client = CreateClient(companyId);
        var first = await SeedAccountAsync(companyId, AccountType.Asset);
        var second = await SeedAccountAsync(companyId, AccountType.Asset);

        (await PutAsync(client, ("Cash", first))).EnsureSuccessStatusCode();
        (await PutAsync(client, ("Cash", (long?)null))).EnsureSuccessStatusCode();
        Assert.Null((await GetAsync(client))["Cash"].AccountId);

        // The unique index is filtered on IsDeleted, so the cleared row must not block a new one.
        (await PutAsync(client, ("Cash", second))).EnsureSuccessStatusCode();
        Assert.Equal(second, (await GetAsync(client))["Cash"].AccountId);
    }

    [Fact]
    public async Task An_account_of_the_wrong_type_is_refused_and_nothing_is_saved()
    {
        var companyId = NewCompanyId();
        var client = CreateClient(companyId);
        var revenue = await SeedAccountAsync(companyId, AccountType.Revenue);

        // Revenue as the cash account: an entry built on this would still balance, which is
        // exactly why it has to be stopped here.
        var response = await PutAsync(client, ("SalesRevenue", revenue), ("Cash", revenue));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("ACC-MAPPING-ACCOUNT-TYPE-MISMATCH", await response.Content.ReadAsStringAsync());

        // All or nothing — the valid half of the request must not have landed either.
        var rows = await GetAsync(client);
        Assert.Null(rows["SalesRevenue"].AccountId);
        Assert.Null(rows["Cash"].AccountId);
    }

    [Fact]
    public async Task A_summary_account_is_refused()
    {
        var companyId = NewCompanyId();
        var client = CreateClient(companyId);
        var parent = await SeedAccountAsync(companyId, AccountType.Asset, isPostable: false);

        var response = await PutAsync(client, ("Cash", parent));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("ACC-MAPPING-ACCOUNT-NOT-POSTABLE", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_inactive_account_is_refused()
    {
        var companyId = NewCompanyId();
        var client = CreateClient(companyId);
        var inactive = await SeedAccountAsync(companyId, AccountType.Asset, isActive: false);

        var response = await PutAsync(client, ("Cash", inactive));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("ACC-MAPPING-ACCOUNT-INACTIVE", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Another_companys_account_reads_as_not_found()
    {
        var otherCompany = NewCompanyId();
        var foreign = await SeedAccountAsync(otherCompany, AccountType.Asset);
        var client = CreateClient(NewCompanyId());

        var response = await PutAsync(client, ("Cash", foreign));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("ACC-MAPPING-ACCOUNT-NOT-FOUND", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Naming_the_same_role_twice_is_a_validation_error()
    {
        var companyId = NewCompanyId();
        var client = CreateClient(companyId);
        var a = await SeedAccountAsync(companyId, AccountType.Asset);
        var b = await SeedAccountAsync(companyId, AccountType.Asset);

        var response = await PutAsync(client, ("Cash", a), ("Cash", b));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Mappings_are_isolated_per_company()
    {
        var companyA = NewCompanyId();
        var clientA = CreateClient(companyA);
        var cash = await SeedAccountAsync(companyA, AccountType.Asset);
        (await PutAsync(clientA, ("Cash", cash))).EnsureSuccessStatusCode();

        var rowsB = await GetAsync(CreateClient(NewCompanyId()));

        Assert.Null(rowsB["Cash"].AccountId);

        await using var db = factory.CreateDirectDbContext(companyA);
        Assert.Equal(1, await db.CompanyAccountMappings.CountAsync());
    }
}
