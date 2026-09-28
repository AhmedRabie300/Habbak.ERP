using System.Net.Http.Json;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// What the posting engine needs on top of the operational seed: an account for every company role
/// (of the type that role demands), the terminal's cash treasury, and templates switched on.
/// Stands in for the finance manager's setup, which these tests are not about.
/// </summary>
public sealed record PostingAccounts(IReadOnlyDictionary<CompanyAccountRole, long> Roles, long TerminalTreasuryId)
{
    public long this[CompanyAccountRole role] => Roles[role];
}

public static class PostingSeed
{
    private const string TemplatesUrl = "/api/v1/accounting/posting-templates";

    public static async Task<PostingAccounts> MapAllRolesAsync(
        AccountingApiFactory factory, CycleIds ids, params CompanyAccountRole[] leaveUnmapped)
    {
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);

        // The operational seed never creates the Company row itself (nothing there needed it); the
        // engine does — it reads the base currency. Inserted under the test's random id.
        if (!await db.Companies.AnyAsync(c => c.Id == ids.CompanyId))
        {
            var egp = await db.Currencies.IgnoreQueryFilters().FirstAsync(c => c.Code == "EGP");
            await db.Database.OpenConnectionAsync();
            try
            {
                await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Companies] ON");
                db.Companies.Add(new Company
                {
                    Id = ids.CompanyId, Code = $"C{ids.CompanyId % 100000000}", NameAr = "شركة اختبار", NameEn = "Test Co",
                    BaseCurrencyId = egp.Id
                });
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Companies] OFF");
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }

        Account New(string name, AccountType type) => new()
        {
            CompanyId = ids.CompanyId, Code = $"P-{Guid.NewGuid():N}"[..20], NameAr = name, NameEn = name, AccountType = type,
            Nature = type is AccountType.Asset or AccountType.Expense ? AccountNature.Debit : AccountNature.Credit,
            IsPostable = true, IsActive = true
        };

        var accounts = Enum.GetValues<CompanyAccountRole>().ToDictionary(r => r, r => New(r.ToString(), r.ExpectedAccountType()));
        var treasury = New("خزينة كاشير الاختبار", AccountType.Asset);
        db.Accounts.AddRange(accounts.Values);
        db.Accounts.Add(treasury);
        await db.SaveChangesAsync();

        foreach (var (role, account) in accounts.Where(a => !leaveUnmapped.Contains(a.Key)))
        {
            db.CompanyAccountMappings.Add(new CompanyAccountMapping { CompanyId = ids.CompanyId, Role = role, AccountId = account.Id });
        }

        db.POSPaymentMethodConfigs.Add(new POSPaymentMethodConfig
        {
            POSTerminalId = ids.PosTerminalId, PaymentMethodId = ids.PaymentMethodId, IsEnabled = true, LinkedTreasuryAccountId = treasury.Id
        });
        await db.SaveChangesAsync();

        return new PostingAccounts(accounts.ToDictionary(a => a.Key, a => a.Value.Id), treasury.Id);
    }

    /// <summary>Creates the screen's standard templates (they start switched off) and switches the screen on.</summary>
    public static async Task<IReadOnlyList<long>> ActivateDefaultAsync(HttpClient client, string screenCode)
    {
        var created = await client.PostAsJsonAsync($"{TemplatesUrl}/defaults", new { screenCode });
        created.EnsureSuccessStatusCode();
        var ids = (await created.Content.ReadFromJsonAsync<IdsResponse>())!.Ids;
        (await client.PostAsJsonAsync($"{TemplatesUrl}/screens/{screenCode}/active", new { isActive = true })).EnsureSuccessStatusCode();
        return ids;
    }

    public static async Task SetPOSModeAsync(AccountingApiFactory factory, CycleIds ids, POSPostingMode mode)
    {
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var row = await db.BranchPOSSettingsRows.FirstOrDefaultAsync(s => s.BranchId == ids.BranchId);
        if (row is null)
        {
            row = new BranchPOSSettings { CompanyId = ids.CompanyId, BranchId = ids.BranchId };
            db.BranchPOSSettingsRows.Add(row);
        }
        row.PostingMode = mode;
        await db.SaveChangesAsync();
    }

    public static async Task<List<JournalEntryLine>> LinesOfAsync(AccountingApiFactory factory, long companyId, long entryId)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        return await db.JournalEntryLines.Where(l => l.JournalEntryId == entryId).ToListAsync();
    }

    /// <summary>Every line of the posting group the entry belongs to — a document's revenue and cost-of-sales entries together.</summary>
    public static async Task<List<JournalEntryLine>> LinesOfGroupAsync(AccountingApiFactory factory, long companyId, long entryId)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        var groupId = await db.JournalEntries.Where(e => e.Id == entryId).Select(e => e.PostingGroupId).SingleAsync();
        return await db.JournalEntryLines.Where(l => l.JournalEntry!.PostingGroupId == groupId).ToListAsync();
    }

    public static decimal Debit(this List<JournalEntryLine> lines, long accountId) =>
        lines.Where(l => l.AccountId == accountId).Sum(l => l.DebitAmount);

    public static decimal Credit(this List<JournalEntryLine> lines, long accountId) =>
        lines.Where(l => l.AccountId == accountId).Sum(l => l.CreditAmount);

    public sealed record IdResponse(long Id);

    public sealed record IdsResponse(List<long> Ids);
}
