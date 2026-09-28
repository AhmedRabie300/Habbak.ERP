using System.Net;
using System.Net.Http.Json;
using Habbak.ERP.Domain.Accounting;
using Xunit;

namespace Habbak.ERP.ApiTests;

/// <summary>
/// The HTTP surface of the posting templates (Docs/Posting-Engine-Implementation-Plan.md, stage 1).
/// Engine behaviour and versioning are covered against a real database in
/// IntegrationTests/PostingTemplateEngineTests; these check the wiring the editor screen will use.
/// </summary>
public class PostingTemplatesTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private const string Url = "/api/v1/accounting/posting-templates";

    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private async Task<(long Debit, long Credit)> SeedAccountsAsync(long companyId)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        Account New(AccountType type, AccountNature nature) => new()
        {
            CompanyId = companyId, Code = $"T-{Guid.NewGuid():N}"[..20], NameAr = "حساب", NameEn = "Account",
            AccountType = type, Nature = nature, IsPostable = true, IsActive = true
        };
        var debit = New(AccountType.Expense, AccountNature.Debit);
        var credit = New(AccountType.Asset, AccountNature.Debit);
        db.Accounts.AddRange(debit, credit);
        await db.SaveChangesAsync();
        return (debit.Id, credit.Id);
    }

    private static object Line(int number, string direction, long accountId) => new
    {
        lineNumber = number,
        direction,
        accountSourceType = "Fixed",
        fixedAccountId = accountId,
        amountFormulaType = "DirectField",
        amountFieldName = "Amount",
        conditionType = "None",
        costCenters = Array.Empty<object>()
    };

    private static object Template(string screenCode, params object[] lines) => new
    {
        screenCode,
        definition = new { nameAr = "مصروف نقدي", nameEn = "Cash expense", lines }
    };

    private sealed record CatalogDto(List<string> AmountFormulaTypes, List<string> CompanyAccountRoles,
        List<string> AccountResolverKeys, List<string> CostCenterResolverKeys);

    private sealed record ListItemDto(long Id, string ScreenCode, int VersionNumber, bool IsActive, int LineCount);

    private sealed record LineDto(int LineNumber, string Direction, long? FixedAccountId);

    private sealed record DetailDto(long Id, string ScreenCode, int VersionNumber, bool IsCurrentVersion, List<LineDto> Lines);

    private sealed record CreatedDto(long Id);

    [Fact]
    public async Task Catalog_lists_the_closed_choices_including_registered_resolvers()
    {
        var catalog = await CreateClient(NewCompanyId()).GetFromJsonAsync<CatalogDto>($"{Url}/catalog");

        Assert.Contains("SubtotalMinusDiscount", catalog!.AmountFormulaTypes);
        Assert.Contains("VatReceivable", catalog.CompanyAccountRoles);
        Assert.Contains("Supplier.PayableAccountId", catalog.AccountResolverKeys);
        Assert.Contains("Branch.ToCostCenterValue", catalog.CostCenterResolverKeys);
    }

    [Fact]
    public async Task Created_template_is_listed_and_readable()
    {
        var companyId = NewCompanyId();
        var client = CreateClient(companyId);
        var (debit, credit) = await SeedAccountsAsync(companyId);

        var response = await client.PostAsJsonAsync(Url, Template("POS_DRAWER_EXPENSE", Line(1, "Debit", debit), Line(2, "Credit", credit)));
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<CreatedDto>())!.Id;

        var detail = await client.GetFromJsonAsync<DetailDto>($"{Url}/{id}");
        Assert.Equal("POS_DRAWER_EXPENSE", detail!.ScreenCode);
        Assert.Equal(1, detail.VersionNumber);
        Assert.True(detail.IsCurrentVersion);
        Assert.Equal(["Debit", "Credit"], detail.Lines.Select(l => l.Direction));
        Assert.Equal(debit, detail.Lines[0].FixedAccountId);

        var list = await client.GetFromJsonAsync<List<ListItemDto>>(Url);
        var row = Assert.Single(list!);
        Assert.Equal(2, row.LineCount);
        Assert.True(row.IsActive);
    }

    [Fact]
    public async Task A_template_without_a_credit_side_is_rejected()
    {
        var companyId = NewCompanyId();
        var client = CreateClient(companyId);
        var (debit, _) = await SeedAccountsAsync(companyId);

        var response = await client.PostAsJsonAsync(Url, Template("ONE_SIDED", Line(1, "Debit", debit)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Deactivating_keeps_the_template_but_marks_it_inactive()
    {
        var companyId = NewCompanyId();
        var client = CreateClient(companyId);
        var (debit, credit) = await SeedAccountsAsync(companyId);
        var created = await client.PostAsJsonAsync(Url, Template("POS_DRAWER_EXPENSE", Line(1, "Debit", debit), Line(2, "Credit", credit)));
        var id = (await created.Content.ReadFromJsonAsync<CreatedDto>())!.Id;

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"{Url}/{id}/deactivate", null)).StatusCode);

        var row = Assert.Single((await client.GetFromJsonAsync<List<ListItemDto>>(Url))!);
        Assert.False(row.IsActive);
    }

    [Fact]
    public async Task Another_companys_template_is_not_found()
    {
        var owner = NewCompanyId();
        var (debit, credit) = await SeedAccountsAsync(owner);
        var created = await CreateClient(owner).PostAsJsonAsync(Url, Template("POS_DRAWER_EXPENSE", Line(1, "Debit", debit), Line(2, "Credit", credit)));
        var id = (await created.Content.ReadFromJsonAsync<CreatedDto>())!.Id;

        var response = await CreateClient(NewCompanyId()).GetAsync($"{Url}/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
