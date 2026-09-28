using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Habbak.ERP.Application.FixedAssets;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.FixedAssets;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// The fixed assets &amp; maintenance module end to end (Docs/Modules/08-Module-Maintenance-FixedAssets-Request.md):
/// the depreciation schedule and its monthly run (including the run asked for twice), transfers,
/// disposals, counts, and a maintenance job whose external cost and stocked spare parts post as two
/// separate entries. The daily jobs are driven through <see cref="FixedAssetJobs"/> itself.
/// </summary>
public class FixedAssetsMaintenanceTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private const string AssetsUrl = "/api/v1/fixed-assets";
    private const string RunsUrl = "/api/v1/fixed-assets/depreciation-runs";
    private const string MaintenanceUrl = "/api/v1/maintenance/requests";

    private sealed record Arrangement(CycleIds Ids, HttpClient Client, long CategoryId, FixedAssetAccounts Accounts);

    private sealed record FixedAssetAccounts(long Asset, long Accumulated, long Expense, long Gain, long Loss, long Maintenance, long Funding);

    /// <summary>The API writes enums as names; these options read them back the same way.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    /// <summary>
    /// A company with the operational seed, the posting roles, a branch-linked cost-center dimension
    /// (every entry of the module carries one — rule 27) and one asset category with its accounts.
    /// </summary>
    private async Task<Arrangement> ArrangeAsync()
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        await PostingSeed.MapAllRolesAsync(factory, ids);
        var client = CreateClient(companyId);

        FixedAssetAccounts accounts;
        await using (var db = factory.CreateDirectDbContext(companyId))
        {
            db.CostCenterDimensions.Add(new CostCenterDimension
            {
                CompanyId = companyId, Code = $"FA-BR-{companyId % 100000}", NameAr = "الفروع", NameEn = "Branches",
                LinkedEntityType = CostCenterLinkedEntityType.Branch, IsActive = true
            });

            Account New(string name, AccountType type) => new()
            {
                CompanyId = companyId, Code = $"FA-{Guid.NewGuid():N}"[..20], NameAr = name, NameEn = name, AccountType = type,
                Nature = type is AccountType.Asset or AccountType.Expense ? AccountNature.Debit : AccountNature.Credit,
                IsPostable = true, IsActive = true
            };

            var asset = New("أصول ثابتة", AccountType.Asset);
            var accumulated = New("مجمع الإهلاك", AccountType.Asset);
            var expense = New("مصروف الإهلاك", AccountType.Expense);
            var gain = New("أرباح استبعاد", AccountType.Revenue);
            var loss = New("خسائر استبعاد", AccountType.Expense);
            var maintenance = New("مصروف الصيانة", AccountType.Expense);
            var funding = New("دائنون - أصول", AccountType.Liability);
            db.Accounts.AddRange(asset, accumulated, expense, gain, loss, maintenance, funding);
            await db.SaveChangesAsync();
            accounts = new FixedAssetAccounts(asset.Id, accumulated.Id, expense.Id, gain.Id, loss.Id, maintenance.Id, funding.Id);
        }

        var created = await client.PostAsJsonAsync($"{AssetsUrl}/categories", new
        {
            code = $"CAT-{Random.Shared.Next(100000, 999999)}",
            data = new
            {
                nameAr = "أجهزة قهوة", nameEn = "Coffee machines", depreciationMethod = DepreciationMethod.StraightLine,
                defaultDepreciationRate = (decimal?)null, defaultUsefulLifeYears = 5, defaultSalvagePercentage = (decimal?)null,
                assetAccountId = accounts.Asset, accumulatedDepreciationAccountId = accounts.Accumulated,
                depreciationExpenseAccountId = accounts.Expense, disposalGainAccountId = accounts.Gain,
                disposalLossAccountId = accounts.Loss, maintenanceExpenseAccountId = accounts.Maintenance, isActive = true
            }
        });
        created.EnsureSuccessStatusCode();
        var categoryId = (await created.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;

        return new Arrangement(ids, client, categoryId, accounts);
    }

    private static object AssetPayload(
        Arrangement a, decimal cost = 120000m, decimal salvage = 0m, int? life = 5, DateOnly? start = null, bool? prorated = false,
        DepreciationMethod method = DepreciationMethod.StraightLine, decimal? rate = null, long? branchId = null) => new
        {
            data = new
            {
                nameAr = "ماكينة إسبرسو", nameEn = "Espresso machine", branchId = branchId ?? a.Ids.BranchId, categoryId = a.CategoryId,
                serialNumber = "SN-1", barcode = (string?)null, location = "الدور الأول",
                acquisitionDate = start ?? new DateOnly(2026, 1, 1), acquisitionCost = cost, currencyCode = "EGP", exchangeRate = 1m,
                supplierId = a.Ids.SupplierId, purchaseInvoiceId = (long?)null, fundingAccountId = a.Accounts.Funding,
                usefulLifeYears = life, salvageValue = salvage, depreciationMethod = method, depreciationRate = rate,
                depreciationStartDate = start ?? new DateOnly(2026, 1, 1), firstMonthProrated = prorated,
                custodyOfficerId = a.Ids.CustodyOfficerId, costCenterValueId = (long?)null, notes = (string?)null
            }
        };

    private async Task<long> CreateAssetAsync(Arrangement a, object? payload = null)
    {
        var response = await a.Client.PostAsJsonAsync(AssetsUrl, payload ?? AssetPayload(a));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;
    }

    private async Task<long> ActivateAssetAsync(Arrangement a, object? payload = null)
    {
        var id = await CreateAssetAsync(a, payload);
        (await a.Client.PostAsync($"{AssetsUrl}/{id}/activate", null)).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return body;
    }

    // ------------------------------------------------------------------ master data and validation

    [Fact]
    public async Task Category_rejects_an_account_that_cannot_be_posted_to()
    {
        var a = await ArrangeAsync();
        long headerAccountId;
        await using (var db = factory.CreateDirectDbContext(a.Ids.CompanyId))
        {
            var header = new Account
            {
                CompanyId = a.Ids.CompanyId, Code = $"H-{Guid.NewGuid():N}"[..18], NameAr = "حساب رئيسي", NameEn = "Header",
                AccountType = AccountType.Asset, Nature = AccountNature.Debit, IsPostable = false, IsActive = true
            };
            db.Accounts.Add(header);
            await db.SaveChangesAsync();
            headerAccountId = header.Id;
        }

        var response = await a.Client.PostAsJsonAsync($"{AssetsUrl}/categories", new
        {
            code = $"CAT-{Random.Shared.Next(100000, 999999)}",
            data = new
            {
                nameAr = "فئة", nameEn = "Category", depreciationMethod = DepreciationMethod.StraightLine,
                defaultDepreciationRate = (decimal?)null, defaultUsefulLifeYears = 4, defaultSalvagePercentage = (decimal?)null,
                assetAccountId = headerAccountId, accumulatedDepreciationAccountId = a.Accounts.Accumulated,
                depreciationExpenseAccountId = a.Accounts.Expense, disposalGainAccountId = (long?)null,
                disposalLossAccountId = (long?)null, maintenanceExpenseAccountId = a.Accounts.Maintenance, isActive = true
            }
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("FA-CATEGORY-ACCOUNT-INVALID", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Asset_cost_must_be_greater_than_zero()
    {
        var a = await ArrangeAsync();
        var response = await a.Client.PostAsJsonAsync(AssetsUrl, AssetPayload(a, cost: 0m));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Asset_salvage_value_must_be_below_its_cost()
    {
        var a = await ArrangeAsync();
        var response = await a.Client.PostAsJsonAsync(AssetsUrl, AssetPayload(a, cost: 10000m, salvage: 10000m));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("FA-SALVAGE-TOO-HIGH", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Declining_balance_without_a_rate_is_refused()
    {
        var a = await ArrangeAsync();
        var response = await a.Client.PostAsJsonAsync(
            AssetsUrl, AssetPayload(a, method: DepreciationMethod.DecliningBalance, rate: null));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("FA-RATE-REQUIRED", await ErrorCodeAsync(response));
    }

    // ------------------------------------------------------------------ activation and the schedule

    [Fact]
    public async Task Activating_an_asset_posts_its_acquisition_and_lays_out_the_schedule()
    {
        var a = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(a.Client, FixedAssetScreens.Assets);
        var assetId = await ActivateAssetAsync(a, AssetPayload(a, cost: 120000m, salvage: 0m, life: 5));

        await using var db = factory.CreateDirectDbContext(a.Ids.CompanyId);
        var asset = await db.FixedAssets.Include(x => x.Schedule).FirstAsync(x => x.Id == assetId);
        Assert.Equal(FixedAssetStatus.Active, asset.Status);
        Assert.Equal(60, asset.Schedule.Count);
        Assert.Equal(120000m, asset.Schedule.Sum(s => s.Amount));
        Assert.Equal(2000m, asset.Schedule.First(s => s.PeriodNumber == 1).Amount);
        Assert.NotNull(asset.AcquisitionJournalEntryId);

        var lines = await PostingSeed.LinesOfAsync(factory, a.Ids.CompanyId, asset.AcquisitionJournalEntryId!.Value);
        Assert.Equal(120000m, lines.Debit(a.Accounts.Asset));
        Assert.Equal(120000m, lines.Credit(a.Accounts.Funding));
    }

    [Fact]
    public async Task A_prorated_first_month_is_charged_by_the_days_left_and_adds_a_final_period()
    {
        var a = await ArrangeAsync();
        var assetId = await ActivateAssetAsync(a, AssetPayload(
            a, cost: 120000m, salvage: 0m, life: 5, start: new DateOnly(2026, 1, 16), prorated: true));

        await using var db = factory.CreateDirectDbContext(a.Ids.CompanyId);
        var schedule = await db.DepreciationSchedules.Where(s => s.FixedAssetId == assetId).OrderBy(s => s.PeriodNumber).ToListAsync();
        Assert.Equal(61, schedule.Count);
        // 16 of January's 31 days: 2 000 × 16/31 = 1 032.26
        Assert.Equal(1032.26m, schedule[0].Amount);
        Assert.Equal(2000m, schedule[1].Amount);
        Assert.Equal(120000m, schedule.Sum(s => s.Amount));
    }

    [Fact]
    public async Task An_asset_that_is_not_depreciated_gets_no_schedule()
    {
        var a = await ArrangeAsync();
        var assetId = await ActivateAssetAsync(a, AssetPayload(a, method: DepreciationMethod.NoDepreciation, life: null));

        await using var db = factory.CreateDirectDbContext(a.Ids.CompanyId);
        Assert.Empty(await db.DepreciationSchedules.Where(s => s.FixedAssetId == assetId).ToListAsync());
    }

    [Fact]
    public async Task An_active_asset_cannot_be_deleted()
    {
        var a = await ArrangeAsync();
        var assetId = await ActivateAssetAsync(a);
        var response = await a.Client.DeleteAsync($"{AssetsUrl}/{assetId}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("FA-ASSET-NOT-DRAFT", await ErrorCodeAsync(response));
    }

    // ------------------------------------------------------------------ the monthly run

    [Fact]
    public async Task The_monthly_run_posts_one_entry_carrying_each_asset_cost_center()
    {
        var a = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(a.Client, FixedAssetScreens.DepreciationRuns);
        var assetId = await ActivateAssetAsync(a);

        var created = await a.Client.PostAsJsonAsync(RunsUrl, new { year = 2026, month = 1 });
        created.EnsureSuccessStatusCode();
        var run = (await created.Content.ReadFromJsonAsync<DepreciationRunResult>(Json))!;
        (await a.Client.PostAsync($"{RunsUrl}/{run.Id}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(a.Ids.CompanyId);
        var posted = await db.DepreciationRuns.Include(r => r.Periods).FirstAsync(r => r.Id == run.Id);
        Assert.Equal(DepreciationRunStatus.Posted, posted.Status);
        Assert.Equal(2000m, posted.TotalDepreciation);
        Assert.All(posted.Periods, p => Assert.Equal(DepreciationScheduleStatus.Posted, p.Status));
        Assert.Equal(2000m, (await db.FixedAssets.FirstAsync(x => x.Id == assetId)).AccumulatedDepreciation);

        var lines = await PostingSeed.LinesOfAsync(factory, a.Ids.CompanyId, posted.JournalEntryId!.Value);
        Assert.Equal(2000m, lines.Debit(a.Accounts.Expense));
        Assert.Equal(2000m, lines.Credit(a.Accounts.Accumulated));
        var dimensions = await db.JournalEntryLineDimensionValues.Where(d => lines.Select(l => l.Id).Contains(d.JournalEntryLineId)).ToListAsync();
        Assert.Equal(lines.Count, dimensions.Count);
    }

    /// <summary>Rule 28: the month's key is (company, year, month) — a second run never doubles the entry.</summary>
    [Fact]
    public async Task Running_the_depreciation_twice_for_the_same_month_neither_creates_nor_posts_a_second_time()
    {
        var a = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(a.Client, FixedAssetScreens.DepreciationRuns);
        var assetId = await ActivateAssetAsync(a);

        var first = (await (await a.Client.PostAsJsonAsync(RunsUrl, new { year = 2026, month = 1 })).Content
            .ReadFromJsonAsync<DepreciationRunResult>(Json))!;
        Assert.False(first.AlreadyExisted);

        var second = (await (await a.Client.PostAsJsonAsync(RunsUrl, new { year = 2026, month = 1 })).Content
            .ReadFromJsonAsync<DepreciationRunResult>(Json))!;
        Assert.True(second.AlreadyExisted);
        Assert.Equal(first.Id, second.Id);

        (await a.Client.PostAsync($"{RunsUrl}/{first.Id}/post", null)).EnsureSuccessStatusCode();
        var postedAgain = await a.Client.PostAsync($"{RunsUrl}/{first.Id}/post", null);
        Assert.Equal(HttpStatusCode.Conflict, postedAgain.StatusCode);
        Assert.Contains("FA-RUN-ALREADY-POSTED", await ErrorCodeAsync(postedAgain));

        await using var db = factory.CreateDirectDbContext(a.Ids.CompanyId);
        Assert.Equal(1, await db.DepreciationRuns.CountAsync(r => r.Year == 2026 && r.Month == 1));
        Assert.Equal(1, await db.JournalEntries.CountAsync(e => e.SourceDocumentType == SourceDocumentType.Depreciation));
        Assert.Equal(2000m, (await db.FixedAssets.FirstAsync(x => x.Id == assetId)).AccumulatedDepreciation);
    }

    [Fact]
    public async Task A_run_catches_up_every_month_that_was_skipped()
    {
        var a = await ArrangeAsync();
        await ActivateAssetAsync(a);

        var run = (await (await a.Client.PostAsJsonAsync(RunsUrl, new { year = 2026, month = 3 })).Content
            .ReadFromJsonAsync<DepreciationRunResult>(Json))!;

        await using var db = factory.CreateDirectDbContext(a.Ids.CompanyId);
        var periods = await db.DepreciationSchedules.Where(s => s.DepreciationRunId == run.Id).ToListAsync();
        Assert.Equal(3, periods.Count);   // January, February, March
        Assert.Equal(6000m, periods.Sum(p => p.Amount));
    }

    [Fact]
    public async Task Reversing_a_run_puts_its_periods_back_and_takes_the_depreciation_off_the_asset()
    {
        var a = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(a.Client, FixedAssetScreens.DepreciationRuns);
        var assetId = await ActivateAssetAsync(a);

        var run = (await (await a.Client.PostAsJsonAsync(RunsUrl, new { year = 2026, month = 1 })).Content
            .ReadFromJsonAsync<DepreciationRunResult>(Json))!;
        (await a.Client.PostAsync($"{RunsUrl}/{run.Id}/post", null)).EnsureSuccessStatusCode();
        (await a.Client.PostAsJsonAsync($"{RunsUrl}/{run.Id}/reverse", new { reason = "الشهر اتقفل غلط" })).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(a.Ids.CompanyId);
        var reversed = await db.DepreciationRuns.FirstAsync(r => r.Id == run.Id);
        Assert.Equal(DepreciationRunStatus.Reversed, reversed.Status);
        Assert.NotNull(reversed.ReversalJournalEntryId);
        Assert.Equal(0m, (await db.FixedAssets.FirstAsync(x => x.Id == assetId)).AccumulatedDepreciation);
        Assert.All(
            await db.DepreciationSchedules.Where(s => s.FixedAssetId == assetId).ToListAsync(),
            s => Assert.Equal(DepreciationScheduleStatus.Scheduled, s.Status));

        // The month is free again: a new run for it is created, not the reversed one.
        var again = (await (await a.Client.PostAsJsonAsync(RunsUrl, new { year = 2026, month = 1 })).Content
            .ReadFromJsonAsync<DepreciationRunResult>(Json))!;
        Assert.False(again.AlreadyExisted);
        Assert.NotEqual(run.Id, again.Id);
    }

    // ------------------------------------------------------------------ transfers

    [Fact]
    public async Task Posting_a_transfer_moves_the_asset_and_leaves_its_schedule_alone()
    {
        var a = await ArrangeAsync();
        var assetId = await ActivateAssetAsync(a);
        long targetBranchId;
        await using (var db = factory.CreateDirectDbContext(a.Ids.CompanyId))
        {
            var branch = new Habbak.ERP.Domain.Organization.Branch
            {
                CompanyId = a.Ids.CompanyId, Code = $"FA-BR2-{Random.Shared.Next(100000, 999999)}", NameAr = "فرع تاني", NameEn = "Second", IsActive = true
            };
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
            targetBranchId = branch.Id;
        }

        var before = await ScheduleSignatureAsync(a.Ids.CompanyId, assetId);
        var created = await a.Client.PostAsJsonAsync($"{AssetsUrl}/transfers", new
        {
            fixedAssetId = assetId, toBranchId = targetBranchId, transferDate = new DateOnly(2026, 2, 1),
            reason = "نقل لفرع جديد", custodyOfficerId = a.Ids.CustodyOfficerId, notes = (string?)null
        });
        created.EnsureSuccessStatusCode();
        var transferId = (await created.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;

        await using (var db = factory.CreateDirectDbContext(a.Ids.CompanyId))
        {
            Assert.Equal(FixedAssetStatus.Transferred, (await db.FixedAssets.FirstAsync(x => x.Id == assetId)).Status);
        }

        (await a.Client.PostAsync($"{AssetsUrl}/transfers/{transferId}/post", null)).EnsureSuccessStatusCode();

        await using (var db = factory.CreateDirectDbContext(a.Ids.CompanyId))
        {
            var asset = await db.FixedAssets.FirstAsync(x => x.Id == assetId);
            Assert.Equal(targetBranchId, asset.BranchId);
            Assert.Equal(FixedAssetStatus.Active, asset.Status);
            Assert.Equal(AssetTransferStatus.Posted, (await db.AssetTransfers.FirstAsync(t => t.Id == transferId)).Status);
        }

        Assert.Equal(before, await ScheduleSignatureAsync(a.Ids.CompanyId, assetId));   // rule 33
    }

    [Fact]
    public async Task Cancelling_a_transfer_puts_the_asset_back_in_service()
    {
        var a = await ArrangeAsync();
        var assetId = await ActivateAssetAsync(a);
        long targetBranchId;
        await using (var db = factory.CreateDirectDbContext(a.Ids.CompanyId))
        {
            var branch = new Habbak.ERP.Domain.Organization.Branch
            {
                CompanyId = a.Ids.CompanyId, Code = $"FA-BR3-{Random.Shared.Next(100000, 999999)}", NameAr = "فرع ثالث", NameEn = "Third", IsActive = true
            };
            db.Branches.Add(branch);
            await db.SaveChangesAsync();
            targetBranchId = branch.Id;
        }

        var created = await a.Client.PostAsJsonAsync($"{AssetsUrl}/transfers", new
        {
            fixedAssetId = assetId, toBranchId = targetBranchId, transferDate = new DateOnly(2026, 2, 1),
            reason = (string?)null, custodyOfficerId = a.Ids.CustodyOfficerId, notes = (string?)null
        });
        var transferId = (await created.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;
        (await a.Client.PostAsync($"{AssetsUrl}/transfers/{transferId}/cancel", null)).EnsureSuccessStatusCode();

        await using var check = factory.CreateDirectDbContext(a.Ids.CompanyId);
        var asset = await check.FixedAssets.FirstAsync(x => x.Id == assetId);
        Assert.Equal(FixedAssetStatus.Active, asset.Status);
        Assert.Equal(a.Ids.BranchId, asset.BranchId);
    }

    private async Task<string> ScheduleSignatureAsync(long companyId, long assetId)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        var rows = await db.DepreciationSchedules.Where(s => s.FixedAssetId == assetId).OrderBy(s => s.PeriodNumber).ToListAsync();
        return string.Join("|", rows.Select(r => $"{r.PeriodNumber}:{r.Amount}:{r.Status}"));
    }

    // ------------------------------------------------------------------ disposals

    [Fact]
    public async Task Disposing_of_an_asset_books_the_gain_and_cancels_the_periods_not_yet_depreciated()
    {
        var a = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(a.Client, FixedAssetScreens.Disposals);
        var assetId = await ActivateAssetAsync(a);

        // One month posted: accumulated 2 000, book value 118 000; sold for 120 000 → gain 2 000.
        var run = (await (await a.Client.PostAsJsonAsync(RunsUrl, new { year = 2026, month = 1 })).Content
            .ReadFromJsonAsync<DepreciationRunResult>(Json))!;
        (await a.Client.PostAsync($"{RunsUrl}/{run.Id}/post", null)).EnsureSuccessStatusCode();

        var created = await a.Client.PostAsJsonAsync($"{AssetsUrl}/disposals", new
        {
            fixedAssetId = assetId, disposalDate = new DateOnly(2026, 2, 10), disposalType = DisposalType.Sale,
            proceeds = 120000m, proceedsAccountId = a.Accounts.Funding, buyerName = "كافيه الجيران", notes = (string?)null
        });
        created.EnsureSuccessStatusCode();
        var disposalId = (await created.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;
        (await a.Client.PostAsync($"{AssetsUrl}/disposals/{disposalId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(a.Ids.CompanyId);
        var disposal = await db.AssetDisposals.FirstAsync(d => d.Id == disposalId);
        Assert.Equal(AssetDisposalStatus.Posted, disposal.Status);
        Assert.Equal(118000m, disposal.BookValueAtDisposal);
        Assert.Equal(2000m, disposal.GainOrLoss);
        Assert.Equal(FixedAssetStatus.Disposed, (await db.FixedAssets.FirstAsync(x => x.Id == assetId)).Status);

        var schedule = await db.DepreciationSchedules.Where(s => s.FixedAssetId == assetId).ToListAsync();
        Assert.Equal(1, schedule.Count(s => s.Status == DepreciationScheduleStatus.Posted));
        Assert.Equal(59, schedule.Count(s => s.Status == DepreciationScheduleStatus.Cancelled));   // rule 32: kept, not deleted

        var lines = await PostingSeed.LinesOfAsync(factory, a.Ids.CompanyId, disposal.JournalEntryId!.Value);
        Assert.Equal(2000m, lines.Debit(a.Accounts.Accumulated));
        Assert.Equal(120000m, lines.Debit(a.Accounts.Funding));
        Assert.Equal(120000m, lines.Credit(a.Accounts.Asset));
        Assert.Equal(2000m, lines.Credit(a.Accounts.Gain));
    }

    [Fact]
    public async Task A_lost_asset_is_written_off_at_its_book_value()
    {
        var a = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(a.Client, FixedAssetScreens.Disposals);
        var assetId = await ActivateAssetAsync(a);

        var created = await a.Client.PostAsJsonAsync($"{AssetsUrl}/disposals", new
        {
            fixedAssetId = assetId, disposalDate = new DateOnly(2026, 1, 20), disposalType = DisposalType.Loss,
            proceeds = (decimal?)null, proceedsAccountId = (long?)null, buyerName = (string?)null, notes = "مفقود بعد الجرد"
        });
        created.EnsureSuccessStatusCode();
        var disposalId = (await created.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;
        (await a.Client.PostAsync($"{AssetsUrl}/disposals/{disposalId}/post", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(a.Ids.CompanyId);
        var disposal = await db.AssetDisposals.FirstAsync(d => d.Id == disposalId);
        Assert.Equal(-120000m, disposal.GainOrLoss);
        Assert.Equal(FixedAssetStatus.WrittenOff, (await db.FixedAssets.FirstAsync(x => x.Id == assetId)).Status);

        var lines = await PostingSeed.LinesOfAsync(factory, a.Ids.CompanyId, disposal.JournalEntryId!.Value);
        Assert.Equal(120000m, lines.Debit(a.Accounts.Loss));
        Assert.Equal(120000m, lines.Credit(a.Accounts.Asset));
    }

    [Fact]
    public async Task A_disposal_is_refused_while_earlier_months_are_still_undepreciated()
    {
        var a = await ArrangeAsync();
        var assetId = await ActivateAssetAsync(a);

        var created = await a.Client.PostAsJsonAsync($"{AssetsUrl}/disposals", new
        {
            fixedAssetId = assetId, disposalDate = new DateOnly(2026, 4, 5), disposalType = DisposalType.Scrap,
            proceeds = (decimal?)null, proceedsAccountId = (long?)null, buyerName = (string?)null, notes = (string?)null
        });
        var disposalId = (await created.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;

        var posted = await a.Client.PostAsync($"{AssetsUrl}/disposals/{disposalId}/post", null);
        Assert.Equal(HttpStatusCode.Conflict, posted.StatusCode);
        Assert.Contains("FA-DISPOSAL-DEPRECIATION-DUE", await ErrorCodeAsync(posted));
    }

    // ------------------------------------------------------------------ physical counts

    [Fact]
    public async Task A_count_lists_the_branch_assets_and_completing_it_moves_the_ones_found_elsewhere()
    {
        var a = await ArrangeAsync();
        var assetId = await ActivateAssetAsync(a);

        var created = await a.Client.PostAsJsonAsync($"{AssetsUrl}/physical-counts", new
        {
            branchId = a.Ids.BranchId, countDate = new DateOnly(2026, 2, 1), notes = (string?)null
        });
        created.EnsureSuccessStatusCode();
        var countId = (await created.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;

        var detail = (await a.Client.GetFromJsonAsync<AssetPhysicalCountDetailDto>($"{AssetsUrl}/physical-counts/{countId}", Json))!;
        Assert.Single(detail.Lines);
        Assert.Equal("الدور الأول", detail.Lines[0].ExpectedLocation);

        (await a.Client.PostAsJsonAsync($"{AssetsUrl}/physical-counts/{countId}/record", new
        {
            lines = new[] { new { lineId = detail.Lines[0].Id, isFound = true, actualLocation = "الدور الثاني", condition = AssetCondition.Fair, notes = (string?)null } }
        })).EnsureSuccessStatusCode();
        (await a.Client.PostAsync($"{AssetsUrl}/physical-counts/{countId}/complete", null)).EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(a.Ids.CompanyId);
        Assert.Equal(AssetPhysicalCountStatus.Completed, (await db.AssetPhysicalCounts.FirstAsync(c => c.Id == countId)).Status);
        Assert.Equal("الدور الثاني", (await db.FixedAssets.FirstAsync(x => x.Id == assetId)).Location);
    }

    [Fact]
    public async Task A_count_cannot_be_completed_while_an_asset_is_still_uncounted()
    {
        var a = await ArrangeAsync();
        await ActivateAssetAsync(a);

        var created = await a.Client.PostAsJsonAsync($"{AssetsUrl}/physical-counts", new
        {
            branchId = a.Ids.BranchId, countDate = new DateOnly(2026, 2, 1), notes = (string?)null
        });
        var countId = (await created.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;

        var completed = await a.Client.PostAsync($"{AssetsUrl}/physical-counts/{countId}/complete", null);
        Assert.Equal(HttpStatusCode.Conflict, completed.StatusCode);
        Assert.Contains("FA-COUNT-INCOMPLETE", await ErrorCodeAsync(completed));
    }

    // ------------------------------------------------------------------ maintenance

    private async Task<long> CreateMaintenanceRequestAsync(
        Arrangement a, long assetId, decimal laborCost, decimal? estimatedCost = null, object[]? spareParts = null, long? issueId = null)
    {
        long categoryId;
        await using (var db = factory.CreateDirectDbContext(a.Ids.CompanyId))
        {
            categoryId = await db.MaintenanceCategories.Where(c => c.Code == "CORR").Select(c => c.Id).FirstOrDefaultAsync();
            if (categoryId == 0)
            {
                var category = new MaintenanceCategory
                {
                    CompanyId = a.Ids.CompanyId, Code = "CORR", NameAr = "إصلاح أعطال", NameEn = "Corrective", MaintenanceType = MaintenanceType.Corrective
                };
                db.MaintenanceCategories.Add(category);
                await db.SaveChangesAsync();
                categoryId = category.Id;
            }
        }

        var created = await a.Client.PostAsJsonAsync(MaintenanceUrl, new
        {
            issueId,
            data = new
            {
                fixedAssetId = assetId, maintenanceCategoryId = categoryId, requestDate = new DateOnly(2026, 2, 1),
                scheduledDate = (DateOnly?)null, technicianId = (long?)null, technicianName = "فني خارجي", supplierId = a.Ids.SupplierId,
                externalCreditAccountId = a.Accounts.Funding, estimatedCost, laborCost, notes = (string?)null,
                spareParts = spareParts ?? []
            }
        });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;
    }

    /// <summary>Rule 20: the external cost and the stocked parts are two entries, never one lumped line.</summary>
    [Fact]
    public async Task Completing_a_job_with_both_an_external_cost_and_stocked_parts_posts_two_separate_entries()
    {
        var a = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(a.Client, Habbak.ERP.Application.Posting.Screens.PostingScreenCatalog.MaintenanceExternalCost);
        await PostingSeed.ActivateDefaultAsync(a.Client, Habbak.ERP.Application.Posting.Screens.PostingScreenCatalog.MaintenanceSpareParts);
        var assetId = await ActivateAssetAsync(a);
        await CycleSeed.GiveStockAsync(factory, a.Ids.CompanyId, a.Ids.BranchWarehouseId, a.Ids.EspressoItemId, 100m, averageCost: 2.5m);

        var requestId = await CreateMaintenanceRequestAsync(a, assetId, laborCost: 600m, spareParts:
        [
            new { itemId = a.Ids.EspressoItemId, warehouseId = a.Ids.BranchWarehouseId, description = (string?)null, quantity = 20m, unitCost = (decimal?)null },
            new { itemId = (long?)null, warehouseId = (long?)null, description = (string?)"فلتر اتشرى مخصوص", quantity = 1m, unitCost = (decimal?)300m }
        ]);

        (await a.Client.PostAsync($"{MaintenanceUrl}/{requestId}/start", null)).EnsureSuccessStatusCode();
        (await a.Client.PostAsJsonAsync($"{MaintenanceUrl}/{requestId}/complete", new { completedDate = new DateOnly(2026, 2, 5) }))
            .EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(a.Ids.CompanyId);
        var request = await db.MaintenanceRequests.Include(r => r.SpareParts).FirstAsync(r => r.Id == requestId);
        Assert.Equal(MaintenanceRequestStatus.Completed, request.Status);
        Assert.Equal(350m, request.SparePartsTotalCost);   // 20 × 2.50 (average) + 300 bought
        Assert.Equal(950m, request.ActualCost);
        Assert.NotNull(request.JournalEntryId);
        Assert.NotNull(request.SparePartsJournalEntryId);
        Assert.NotEqual(request.JournalEntryId, request.SparePartsJournalEntryId);

        var external = await PostingSeed.LinesOfAsync(factory, a.Ids.CompanyId, request.JournalEntryId!.Value);
        Assert.Equal(900m, external.Debit(a.Accounts.Maintenance));   // labor 600 + parts bought 300
        Assert.Equal(900m, external.Credit(a.Accounts.Funding));

        var parts = await PostingSeed.LinesOfAsync(factory, a.Ids.CompanyId, request.SparePartsJournalEntryId!.Value);
        Assert.Equal(50m, parts.Debit(a.Accounts.Maintenance));
        Assert.Equal(50m, parts.Sum(l => l.CreditAmount));

        Assert.Equal(80m, await CycleSeed.GetBalanceAsync(factory, a.Ids.CompanyId, a.Ids.BranchWarehouseId, a.Ids.EspressoItemId));
        Assert.Equal(FixedAssetStatus.Active, (await db.FixedAssets.FirstAsync(x => x.Id == assetId)).Status);
    }

    /// <summary>Rule 16: a stocked part costs what the warehouse's average says at the moment it is issued.</summary>
    [Fact]
    public async Task A_stocked_spare_part_is_costed_at_the_warehouse_average_when_it_is_issued()
    {
        var a = await ArrangeAsync();
        var assetId = await ActivateAssetAsync(a);
        await CycleSeed.GiveStockAsync(factory, a.Ids.CompanyId, a.Ids.BranchWarehouseId, a.Ids.MilkItemId, 500m, averageCost: 0.40m);

        var requestId = await CreateMaintenanceRequestAsync(a, assetId, laborCost: 0m, spareParts:
        [
            new { itemId = a.Ids.MilkItemId, warehouseId = a.Ids.BranchWarehouseId, description = (string?)null, quantity = 100m, unitCost = (decimal?)null }
        ]);
        (await a.Client.PostAsync($"{MaintenanceUrl}/{requestId}/start", null)).EnsureSuccessStatusCode();
        (await a.Client.PostAsJsonAsync($"{MaintenanceUrl}/{requestId}/complete", new { completedDate = new DateOnly(2026, 2, 5) }))
            .EnsureSuccessStatusCode();

        await using var db = factory.CreateDirectDbContext(a.Ids.CompanyId);
        var part = await db.MaintenanceSpareParts.FirstAsync(p => p.MaintenanceRequestId == requestId);
        Assert.Equal(0.40m, part.UnitCost);
        Assert.Equal(40m, part.TotalCost);
        Assert.NotNull(part.StockTransactionId);
        var movement = await db.StockTransactions.FirstAsync(t => t.Id == part.StockTransactionId);
        Assert.Equal(TransactionType.MaintenanceIssue, movement.TransactionType);
    }

    [Fact]
    public async Task A_job_above_the_approval_threshold_cannot_start_before_it_is_approved()
    {
        var a = await ArrangeAsync();
        var assetId = await ActivateAssetAsync(a);
        await using (var db = factory.CreateDirectDbContext(a.Ids.CompanyId))
        {
            var settings = await db.AssetSettingsRows.FirstOrDefaultAsync(s => s.CompanyId == a.Ids.CompanyId);
            if (settings is null)
            {
                settings = new AssetSettings { CompanyId = a.Ids.CompanyId };
                db.AssetSettingsRows.Add(settings);
            }

            settings.MaintenanceApprovalThreshold = 500m;
            await db.SaveChangesAsync();
        }

        var requestId = await CreateMaintenanceRequestAsync(a, assetId, laborCost: 800m, estimatedCost: 800m);

        var started = await a.Client.PostAsync($"{MaintenanceUrl}/{requestId}/start", null);
        Assert.Equal(HttpStatusCode.Conflict, started.StatusCode);
        Assert.Contains("FA-MAINT-APPROVAL-REQUIRED", await ErrorCodeAsync(started));

        (await a.Client.PostAsync($"{MaintenanceUrl}/{requestId}/approve", null)).EnsureSuccessStatusCode();
        (await a.Client.PostAsync($"{MaintenanceUrl}/{requestId}/start", null)).EnsureSuccessStatusCode();

        await using var check = factory.CreateDirectDbContext(a.Ids.CompanyId);
        Assert.Equal(MaintenanceRequestStatus.InProgress, (await check.MaintenanceRequests.FirstAsync(r => r.Id == requestId)).Status);
    }

    [Fact]
    public async Task A_fault_report_follows_its_job_from_inspection_to_repaired()
    {
        var a = await ArrangeAsync();
        var assetId = await ActivateAssetAsync(a);

        var reported = await a.Client.PostAsJsonAsync("/api/v1/maintenance/issues", new
        {
            branchId = a.Ids.BranchId, fixedAssetId = assetId, deviceName = (string?)null,
            description = "الماكينة بتسرب مياه", severity = IssueSeverity.High, notes = (string?)null
        });
        reported.EnsureSuccessStatusCode();
        var issueId = (await reported.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;

        var requestId = await CreateMaintenanceRequestAsync(a, assetId, laborCost: 250m, issueId: issueId);
        (await a.Client.PostAsync($"{MaintenanceUrl}/{requestId}/start", null)).EnsureSuccessStatusCode();

        await using (var db = factory.CreateDirectDbContext(a.Ids.CompanyId))
        {
            Assert.Equal(MaintenanceIssueStatus.Repairing, (await db.MaintenanceIssues.FirstAsync(i => i.Id == issueId)).Status);
            Assert.Equal(FixedAssetStatus.InMaintenance, (await db.FixedAssets.FirstAsync(x => x.Id == assetId)).Status);
        }

        (await a.Client.PostAsJsonAsync($"{MaintenanceUrl}/{requestId}/complete", new { completedDate = new DateOnly(2026, 2, 6) }))
            .EnsureSuccessStatusCode();

        await using var check = factory.CreateDirectDbContext(a.Ids.CompanyId);
        Assert.Equal(MaintenanceIssueStatus.Repaired, (await check.MaintenanceIssues.FirstAsync(i => i.Id == issueId)).Status);
        Assert.Equal(FixedAssetStatus.Active, (await check.FixedAssets.FirstAsync(x => x.Id == assetId)).Status);
    }

    [Fact]
    public async Task The_board_shows_open_reports_and_jobs_in_their_columns()
    {
        var a = await ArrangeAsync();
        var assetId = await ActivateAssetAsync(a);
        (await a.Client.PostAsJsonAsync("/api/v1/maintenance/issues", new
        {
            branchId = a.Ids.BranchId, fixedAssetId = (long?)null, deviceName = "ثلاجة العرض",
            description = "صوت عالي", severity = IssueSeverity.Medium, notes = (string?)null
        })).EnsureSuccessStatusCode();
        var requestId = await CreateMaintenanceRequestAsync(a, assetId, laborCost: 100m);
        (await a.Client.PostAsync($"{MaintenanceUrl}/{requestId}/start", null)).EnsureSuccessStatusCode();

        var cards = (await a.Client.GetFromJsonAsync<List<MaintenanceBoardCardDto>>("/api/v1/maintenance/board", Json))!;
        Assert.Contains(cards, c => c.Kind == "Issue" && c.Column == MaintenanceBoardColumn.Reported);
        Assert.Contains(cards, c => c.Kind == "Request" && c.Id == requestId && c.Column == MaintenanceBoardColumn.InProgress);
    }

    // ------------------------------------------------------------------ the daily jobs

    private FixedAssetJobs Jobs() => new(
        factory.Services.GetRequiredService<IServiceScopeFactory>(),
        factory.Services.GetRequiredService<ILoggerFactory>().CreateLogger<FixedAssetJobs>());

    /// <summary>Rule 29: one request per (schedule, due date), however often the job runs.</summary>
    [Fact]
    public async Task The_preventive_job_raises_one_request_per_due_date_however_often_it_runs()
    {
        var a = await ArrangeAsync();
        var assetId = await ActivateAssetAsync(a);
        var today = new DateOnly(2026, 3, 1);
        await using (var db = factory.CreateDirectDbContext(a.Ids.CompanyId))
        {
            var category = await db.MaintenanceCategories.FirstOrDefaultAsync(c => c.Code == "PREV");
            if (category is null)
            {
                category = new MaintenanceCategory
                {
                    CompanyId = a.Ids.CompanyId, Code = "PREV", NameAr = "وقائية", NameEn = "Preventive", MaintenanceType = MaintenanceType.Preventive
                };
                db.MaintenanceCategories.Add(category);
                await db.SaveChangesAsync();
            }

            db.MaintenanceSchedules.Add(new MaintenanceSchedule
            {
                CompanyId = a.Ids.CompanyId, FixedAssetId = assetId, MaintenanceCategoryId = category.Id,
                Frequency = MaintenanceFrequency.Monthly, NextDueDate = today, IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var first = await Jobs().RunForCompanyAsync(a.Ids.CompanyId, today, CancellationToken.None);
        var second = await Jobs().RunForCompanyAsync(a.Ids.CompanyId, today, CancellationToken.None);

        Assert.Null(first.Error);
        Assert.Equal(1, first.MaintenanceRequestsRaised);
        Assert.Equal(0, second.MaintenanceRequestsRaised);

        await using var db2 = factory.CreateDirectDbContext(a.Ids.CompanyId);
        var raised = await db2.MaintenanceRequests.Where(r => r.MaintenanceScheduleId != null).ToListAsync();
        Assert.Single(raised);
        Assert.Equal(today, raised[0].DueDate);
        Assert.NotNull(raised[0].IdempotencyKey);
    }

    [Fact]
    public async Task The_depreciation_job_posts_the_month_once_when_it_is_switched_on()
    {
        var a = await ArrangeAsync();
        await PostingSeed.ActivateDefaultAsync(a.Client, FixedAssetScreens.DepreciationRuns);
        var assetId = await ActivateAssetAsync(a);
        await using (var db = factory.CreateDirectDbContext(a.Ids.CompanyId))
        {
            var settings = await db.AssetSettingsRows.FirstOrDefaultAsync(s => s.CompanyId == a.Ids.CompanyId);
            if (settings is null)
            {
                settings = new AssetSettings { CompanyId = a.Ids.CompanyId };
                db.AssetSettingsRows.Add(settings);
            }

            settings.AutoDepreciationEnabled = true;
            settings.DepreciationRunDay = 28;
            await db.SaveChangesAsync();
        }

        var today = new DateOnly(2026, 1, 28);
        var first = await Jobs().RunForCompanyAsync(a.Ids.CompanyId, today, CancellationToken.None);
        var second = await Jobs().RunForCompanyAsync(a.Ids.CompanyId, today, CancellationToken.None);

        Assert.Equal("posted", first.Depreciation);
        Assert.Equal("already-posted", second.Depreciation);

        await using var db2 = factory.CreateDirectDbContext(a.Ids.CompanyId);
        Assert.Equal(1, await db2.DepreciationRuns.CountAsync());
        Assert.Equal(2000m, (await db2.FixedAssets.FirstAsync(x => x.Id == assetId)).AccumulatedDepreciation);
    }

    [Fact]
    public async Task The_depreciation_job_does_nothing_before_the_run_day()
    {
        var a = await ArrangeAsync();
        await ActivateAssetAsync(a);
        await using (var db = factory.CreateDirectDbContext(a.Ids.CompanyId))
        {
            db.AssetSettingsRows.Add(new AssetSettings { CompanyId = a.Ids.CompanyId, AutoDepreciationEnabled = true, DepreciationRunDay = 28 });
            await db.SaveChangesAsync();
        }

        var result = await Jobs().RunForCompanyAsync(a.Ids.CompanyId, new DateOnly(2026, 1, 10), CancellationToken.None);
        Assert.Equal("not-due", result.Depreciation);

        await using var db2 = factory.CreateDirectDbContext(a.Ids.CompanyId);
        Assert.Equal(0, await db2.DepreciationRuns.CountAsync());
    }
}
