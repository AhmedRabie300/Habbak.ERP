using System.Net.Http.Json;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// POS in the ledger (Docs/Posting-Engine-Implementation-Plan.md, section 9): the three posting modes,
/// switching between them, the shift difference, returns and drawer expenses.
///
/// Takings must land in the terminal's own treasury — that is how the ledger tells one branch's cash
/// from another's — and one entry must never post an invoice twice, whatever the mode history.
/// </summary>
public class POSPostingTests(AccountingApiFactory factory) : IClassFixture<AccountingApiFactory>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private HttpClient CreateClient(long companyId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.CompanyIdHeader, companyId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, "1");
        return client;
    }

    private async Task<(CycleIds Ids, PostingAccounts Accounts, HttpClient Client)> ArrangeAsync(POSPostingMode mode)
    {
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var accounts = await PostingSeed.MapAllRolesAsync(factory, ids);
        await PostingSeed.SetPOSModeAsync(factory, ids, mode);
        var client = CreateClient(companyId);
        await PostingSeed.ActivateDefaultAsync(client, "POS_INVOICES");
        await PostingSeed.ActivateDefaultAsync(client, "POS_SHIFT_VARIANCE");
        return (ids, accounts, client);
    }

    /// <summary>Sells one latte on the terminal's open shift (opening one the first time).</summary>
    private static async Task<long> SellLatteAsync(HttpClient client, CycleIds ids, bool openShift)
    {
        long checkId;
        if (openShift)
        {
            checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);
        }
        else
        {
            var open = await client.PostAsJsonAsync("/api/v1/pos/checks/open-standalone", new
            {
                posTerminalId = ids.PosTerminalId, orderType = "Takeaway", customerId = (long?)null
            });
            open.EnsureSuccessStatusCode();
            checkId = (await open.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;
            await POSTerminalSaleFlowTests.AddLatteLineAsync(client, ids, checkId, 1m);
        }

        var pay = await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 40m);
        pay.EnsureSuccessStatusCode();
        return (await pay.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;
    }

    private sealed record ShiftDto(long Id, string RowVersion);

    /// <summary>Closes the open shift having counted the expected cash minus <paramref name="shortage"/>.</summary>
    private async Task<long> CloseShiftAsync(HttpClient client, CycleIds ids, decimal shortage = 0m)
    {
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var shiftId = await db.Shifts.Where(s => s.POSTerminalId == ids.PosTerminalId && s.Status == ShiftStatus.Open).Select(s => s.Id).SingleAsync();
        var cash = await db.POSPayments.Where(p => p.POSInvoice!.ShiftId == shiftId).SumAsync(p => p.Amount);
        var shift = await client.GetFromJsonAsync<ShiftDto>($"/api/v1/pos/shifts/{shiftId}");

        var close = await client.PostAsJsonAsync($"/api/v1/pos/shifts/{shiftId}/close", new
        {
            rowVersion = shift!.RowVersion,
            closedByUserId = 1L,
            closingCounts = new[] { new { denominationValue = 1m, count = (int)(100m + cash - shortage) } },
            idempotencyKey = Guid.NewGuid()
        });
        close.EnsureSuccessStatusCode();
        return shiftId;
    }

    private async Task<POSInvoice> InvoiceAsync(long companyId, long id)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        return await db.POSInvoices.Include(i => i.JournalEntry).SingleAsync(i => i.Id == id);
    }

    [Fact]
    public async Task PerTransaction_posts_each_sale_to_the_terminal_treasury()
    {
        var (ids, accounts, client) = await ArrangeAsync(POSPostingMode.PerTransaction);

        var invoiceId = await SellLatteAsync(client, ids, openShift: true);

        var invoice = await InvoiceAsync(ids.CompanyId, invoiceId);
        Assert.NotNull(invoice.JournalEntryId);
        Assert.Equal(JournalEntryStatus.Posted, invoice.JournalEntry!.Status);
        Assert.Equal(ids.BranchId, invoice.JournalEntry.BranchId);

        var lines = await PostingSeed.LinesOfAsync(factory, ids.CompanyId, invoice.JournalEntryId!.Value);
        Assert.Equal(40m, lines.Debit(accounts.TerminalTreasuryId));
        Assert.Equal(40m, lines.Credit(accounts[CompanyAccountRole.SalesRevenue]));
        Assert.Equal(0m, lines.Debit(accounts[CompanyAccountRole.Cash])); // not the company-wide cash
    }

    [Fact]
    public async Task PerShift_posts_nothing_until_close_then_one_entry_for_the_whole_shift()
    {
        var (ids, accounts, client) = await ArrangeAsync(POSPostingMode.PerShift);

        var first = await SellLatteAsync(client, ids, openShift: true);
        var second = await SellLatteAsync(client, ids, openShift: false);
        Assert.Null((await InvoiceAsync(ids.CompanyId, first)).JournalEntryId);

        await CloseShiftAsync(client, ids);

        var a = await InvoiceAsync(ids.CompanyId, first);
        var b = await InvoiceAsync(ids.CompanyId, second);
        Assert.NotNull(a.JournalEntryId);
        Assert.Equal(a.JournalEntryId, b.JournalEntryId);

        var lines = await PostingSeed.LinesOfAsync(factory, ids.CompanyId, a.JournalEntryId!.Value);
        Assert.Equal(80m, lines.Debit(accounts.TerminalTreasuryId));
        Assert.Equal(80m, lines.Credit(accounts[CompanyAccountRole.SalesRevenue]));
    }

    [Fact]
    public async Task PerDay_waits_for_the_day_close_button()
    {
        var (ids, accounts, client) = await ArrangeAsync(POSPostingMode.PerDay);
        var invoiceId = await SellLatteAsync(client, ids, openShift: true);
        await CloseShiftAsync(client, ids);
        Assert.Null((await InvoiceAsync(ids.CompanyId, invoiceId)).JournalEntryId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var status = await client.GetFromJsonAsync<DayStatus>($"/api/v1/pos/day-close?branchId={ids.BranchId}&date={today:yyyy-MM-dd}");
        Assert.Equal(1, status!.UnpostedInvoiceCount);

        var close = await client.PostAsJsonAsync("/api/v1/pos/day-close", new { branchId = ids.BranchId, date = today });
        close.EnsureSuccessStatusCode();
        var result = await close.Content.ReadFromJsonAsync<DayCloseResult>();
        Assert.Equal(1, result!.PostedInvoiceCount);
        Assert.NotNull(result.EntryNumber);

        var invoice = await InvoiceAsync(ids.CompanyId, invoiceId);
        var lines = await PostingSeed.LinesOfAsync(factory, ids.CompanyId, invoice.JournalEntryId!.Value);
        Assert.Equal(40m, lines.Debit(accounts.TerminalTreasuryId));

        // Nothing left: pressing it again says so rather than posting an empty entry.
        var again = await client.PostAsJsonAsync("/api/v1/pos/day-close", new { branchId = ids.BranchId, date = today });
        Assert.Contains("POS-DAY-NOTHING-TO-POST", await again.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Switching_mode_mid_shift_posts_every_invoice_exactly_once()
    {
        var (ids, _, client) = await ArrangeAsync(POSPostingMode.PerTransaction);
        var first = await SellLatteAsync(client, ids, openShift: true);   // posted on its own
        await PostingSeed.SetPOSModeAsync(factory, ids, POSPostingMode.PerShift);
        var second = await SellLatteAsync(client, ids, openShift: false); // waits for close

        await CloseShiftAsync(client, ids);

        var a = await InvoiceAsync(ids.CompanyId, first);
        var b = await InvoiceAsync(ids.CompanyId, second);
        Assert.NotNull(a.JournalEntryId);
        Assert.NotNull(b.JournalEntryId);
        Assert.NotEqual(a.JournalEntryId, b.JournalEntryId);

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var posted = await db.JournalEntries.Where(e => e.SourceModule == Shared.Enums.SourceModule.POS).SumAsync(e => e.TotalDebit);
        Assert.Equal(80m, posted); // 40 + 40, not 120
    }

    [Theory]
    [InlineData(5, CompanyAccountRole.CashShortage)]        // within the 10 threshold — the company's
    [InlineData(50, CompanyAccountRole.EmployeeReceivable)] // over it — the cashier's, all of it
    public async Task A_shift_shortage_posts_to_the_account_its_size_calls_for(int shortage, CompanyAccountRole expected)
    {
        var (ids, accounts, client) = await ArrangeAsync(POSPostingMode.PerShift);
        await SellLatteAsync(client, ids, openShift: true);

        var shiftId = await CloseShiftAsync(client, ids, shortage);

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var shift = await db.Shifts.SingleAsync(s => s.Id == shiftId);
        Assert.NotNull(shift.VarianceJournalEntryId);

        var lines = await PostingSeed.LinesOfAsync(factory, ids.CompanyId, shift.VarianceJournalEntryId!.Value);
        Assert.Equal(shortage, lines.Debit(accounts[expected]));
        Assert.Equal(shortage, lines.Credit(accounts.TerminalTreasuryId));
    }

    [Fact]
    public async Task A_real_time_sale_posts_its_recipe_cost_as_cost_of_sales()
    {
        var (ids, accounts, client) = await ArrangeAsync(POSPostingMode.PerTransaction);
        await CycleSeed.SetProductionSalesModeAsync(factory, ids.CompanyId, SettingScopeType.Company, null, ProductionSalesMode.RealTime);
        await CycleSeed.GiveStockAsync(factory, ids.CompanyId, ids.BranchWarehouseId, ids.EspressoItemId, 1000m, averageCost: 0.50m);
        await CycleSeed.GiveStockAsync(factory, ids.CompanyId, ids.BranchWarehouseId, ids.MilkItemId, 5000m, averageCost: 0.02m);

        var invoiceId = await SellLatteAsync(client, ids, openShift: true);

        // 9 g × 0.50 + 150 ml × 0.02 = 7.50
        var invoice = await InvoiceAsync(ids.CompanyId, invoiceId);
        var lines = await PostingSeed.LinesOfGroupAsync(factory, ids.CompanyId, invoice.JournalEntryId!.Value);
        Assert.Equal(7.50m, lines.Debit(accounts[CompanyAccountRole.CostOfGoodsSold]));
        Assert.Equal(7.50m, lines.Credit(accounts[CompanyAccountRole.Inventory]));
    }

    [Fact]
    public async Task A_return_refunds_from_the_terminal_cash_drawer()
    {
        var (ids, accounts, client) = await ArrangeAsync(POSPostingMode.PerShift);
        await PostingSeed.ActivateDefaultAsync(client, "POS_RETURNS");
        var invoiceId = await SellLatteAsync(client, ids, openShift: true);

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var shiftId = await db.Shifts.Where(s => s.Status == ShiftStatus.Open).Select(s => s.Id).SingleAsync();
        var created = await client.PostAsJsonAsync("/api/v1/pos/returns", new
        {
            shiftId,
            sourceInvoiceId = invoiceId,
            returnDate = DateOnly.FromDateTime(DateTime.UtcNow),
            reason = "القهوة باردة",
            lines = new[] { new { itemId = ids.LatteItemId, quantity = 1m, unitPrice = 40m } }
        });
        created.EnsureSuccessStatusCode();
        var returnId = (await created.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;

        var posReturn = await db.POSReturns.SingleAsync(r => r.Id == returnId);
        var lines = await PostingSeed.LinesOfAsync(factory, ids.CompanyId, posReturn.JournalEntryId!.Value);
        Assert.Equal(40m, lines.Debit(accounts[CompanyAccountRole.SalesReturn]));
        Assert.Equal(40m, lines.Credit(accounts.TerminalTreasuryId));
    }

    [Fact]
    public async Task A_drawer_expense_posts_to_its_own_account_from_the_drawer()
    {
        var (ids, accounts, client) = await ArrangeAsync(POSPostingMode.PerShift);
        await PostingSeed.ActivateDefaultAsync(client, "POS_DRAWER_EXPENSE");
        await SellLatteAsync(client, ids, openShift: true);

        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        var shiftId = await db.Shifts.Where(s => s.Status == ShiftStatus.Open).Select(s => s.Id).SingleAsync();
        var expenseAccount = accounts[CompanyAccountRole.BankCommission]; // any expense account will do
        var created = await client.PostAsJsonAsync("/api/v1/pos/drawer-expenses", new
        {
            shiftId, expenseAccountId = expenseAccount, amount = 15m, description = "ثلج"
        });
        created.EnsureSuccessStatusCode();
        var expenseId = (await created.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;

        var expense = await db.DrawerExpenses.SingleAsync(e => e.Id == expenseId);
        var lines = await PostingSeed.LinesOfAsync(factory, ids.CompanyId, expense.JournalEntryId!.Value);
        Assert.Equal(15m, lines.Debit(expenseAccount));
        Assert.Equal(15m, lines.Credit(accounts.TerminalTreasuryId));
    }

    [Fact]
    public async Task A_sale_whose_entry_fails_is_not_saved_and_the_failure_is_logged()
    {
        var (ids, _, client) = await ArrangeAsync(POSPostingMode.PerTransaction);
        await using (var db = factory.CreateDirectDbContext(ids.CompanyId))
        {
            // The finance manager never linked this terminal's payment method to a treasury.
            db.POSPaymentMethodConfigs.RemoveRange(db.POSPaymentMethodConfigs.Where(c => c.POSTerminalId == ids.PosTerminalId));
            await db.SaveChangesAsync();
        }

        var checkId = await POSTerminalSaleFlowTests.OpenCheckWithLatteAsync(client, ids);
        var pay = await POSTerminalSaleFlowTests.PayAsync(client, ids, checkId, 40m);

        Assert.False(pay.IsSuccessStatusCode);
        Assert.Contains("POS-POSTING-NO-TREASURY", await pay.Content.ReadAsStringAsync());

        await using var check = factory.CreateDirectDbContext(ids.CompanyId);
        Assert.False(await check.POSInvoices.AnyAsync());                                     // the transaction rolled it all back
        Assert.Equal(CheckStatus.Open, (await check.Checks.SingleAsync(c => c.Id == checkId)).Status);
        var failure = await check.PostingFailures.SingleAsync();
        Assert.Equal("POS_INVOICES", failure.ScreenCode);
        Assert.Equal("POS-POSTING-NO-TREASURY", failure.ErrorCode);
        Assert.False(failure.IsResolved);
    }

    [Fact]
    public async Task Cost_centers_come_from_the_shift_the_invoice_was_sold_on()
    {
        // Design notes, note 3: the cashier and the terminal are not fields of the invoice. The cashier
        // arrives from the module (the shift it is posting); the terminal is read off the shift record.
        var companyId = NewCompanyId();
        var ids = await CycleSeed.SeedAsync(factory, companyId);
        var accounts = await PostingSeed.MapAllRolesAsync(factory, ids);
        await PostingSeed.SetPOSModeAsync(factory, ids, POSPostingMode.PerShift);
        var client = CreateClient(companyId);

        async Task<long> CreateDimension(string code, string linked)
        {
            var created = await client.PostAsJsonAsync("/api/v1/accounting/dimensions", new { code, nameAr = code, nameEn = code, linkedEntityType = linked });
            created.EnsureSuccessStatusCode();
            return (await created.Content.ReadFromJsonAsync<PostingSeed.IdResponse>())!.Id;
        }

        var terminals = await CreateDimension($"TRM{companyId % 100000}", "POSTerminal");   // values seeded from the terminals
        var cashiers = await CreateDimension($"CSH{companyId % 100000}", "Cashier");        // values entered by hand
        var badCode = await client.PostAsJsonAsync($"/api/v1/accounting/dimensions/{cashiers}/values", new { code = "ahmed", nameAr = "أحمد", nameEn = "Ahmed" });
        Assert.Contains("ACC-DIMENSION-CASHIER-CODE", await badCode.Content.ReadAsStringAsync());
        (await client.PostAsJsonAsync($"/api/v1/accounting/dimensions/{cashiers}/values", new { code = "1", nameAr = "كاشير 1", nameEn = "Cashier 1" }))
            .EnsureSuccessStatusCode();

        var template = await client.PostAsJsonAsync("/api/v1/accounting/posting-templates", new
        {
            screenCode = "POS_INVOICES",
            definition = new
            {
                nameAr = "مبيعات بالكاشير", nameEn = "Sales by cashier",
                lines = new object[]
                {
                    new { lineNumber = 1, direction = "Debit", accountSourceType = "FromGroup", amountFormulaType = "GroupItemAmount",
                          amountFieldName = "Payments", conditionType = "None", costCenters = Array.Empty<object>() },
                    new { lineNumber = 2, direction = "Credit", accountSourceType = "FromCompany", accountResolverKey = "SalesRevenue",
                          amountFormulaType = "DirectField", amountFieldName = "NetSales", conditionType = "None",
                          costCenters = new object[]
                          {
                              new { costCenterDimensionId = cashiers, sourceType = "FromContext", contextKey = "CashierUserId", displayOrder = 1 },
                              new { costCenterDimensionId = terminals, sourceType = "FromRelatedEntity", relatedEntityType = "Shift",
                                    relatedEntityField = "POSTerminalId", displayOrder = 2 }
                          } }
                }
            }
        });
        template.EnsureSuccessStatusCode();

        await SellLatteAsync(client, ids, openShift: true);
        await CloseShiftAsync(client, ids);

        await using var db = factory.CreateDirectDbContext(companyId);
        var entryId = await db.POSInvoices.Select(i => i.JournalEntryId).SingleAsync();
        var revenue = await db.JournalEntryLines.Include(l => l.DimensionValues)
            .SingleAsync(l => l.JournalEntryId == entryId && l.AccountId == accounts[CompanyAccountRole.SalesRevenue]);
        var valueCodes = await db.CostCenterDimensionValues
            .Where(v => revenue.DimensionValues.Select(d => d.CostCenterDimensionValueId).Contains(v.Id))
            .ToDictionaryAsync(v => v.CostCenterDimensionId, v => v.Code);

        Assert.Equal("1", valueCodes[cashiers]);
        Assert.Equal(await db.POSTerminals.Where(t => t.Id == ids.PosTerminalId).Select(t => t.Code).SingleAsync(), valueCodes[terminals]);
    }

    private sealed record DayStatus(int UnpostedInvoiceCount, decimal UnpostedTotal, int PostedInvoiceCount);

    private sealed record DayCloseResult(int PostedInvoiceCount, decimal PostedTotal, string? EntryNumber);
}
