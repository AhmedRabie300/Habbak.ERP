using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.POS;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Habbak.ERP.IntegrationTests;

/// <summary>
/// Branch-level data scope: a session limited to a branch sees that branch and company-wide rows;
/// a company-wide session sees everything. Checked straight against the query filters, the way
/// every screen and report reads.
/// </summary>
public class BranchScopeTests(PostingServiceFixture fixture, ITestOutputHelper output) : IClassFixture<PostingServiceFixture>
{
    private sealed record Seed(long CompanyId, long BranchA, long BranchB, long ShiftA, long ShiftB);

    private async Task<Seed> SeedAsync()
    {
        var companyId = Random.Shared.NextInt64(1_000_000, 9_000_000);
        var tag = Guid.NewGuid().ToString("N")[..6];
        await using var db = fixture.CreateContext();

        var a = new Branch { CompanyId = companyId, Code = $"A{tag}", NameAr = "فرع أ", NameEn = "A" };
        var b = new Branch { CompanyId = companyId, Code = $"B{tag}", NameAr = "فرع ب", NameEn = "B" };
        db.Branches.AddRange(a, b);
        await db.SaveChangesAsync();

        db.Warehouses.AddRange(
            new Warehouse { CompanyId = companyId, BranchId = a.Id, Code = $"WA{tag}", NameAr = "مخزن أ", NameEn = "WA", WarehouseType = WarehouseType.BranchMaterials },
            new Warehouse { CompanyId = companyId, BranchId = b.Id, Code = $"WB{tag}", NameAr = "مخزن ب", NameEn = "WB", WarehouseType = WarehouseType.BranchMaterials },
            new Warehouse { CompanyId = companyId, BranchId = null, Code = $"WM{tag}", NameAr = "الرئيسي", NameEn = "Main", WarehouseType = WarehouseType.Main });

        var terminalA = new POSTerminal { CompanyId = companyId, BranchId = a.Id, Code = $"PA{tag}", NameAr = "جهاز أ", NameEn = "PA" };
        var terminalB = new POSTerminal { CompanyId = companyId, BranchId = b.Id, Code = $"PB{tag}", NameAr = "جهاز ب", NameEn = "PB" };
        db.POSTerminals.AddRange(terminalA, terminalB);
        await db.SaveChangesAsync();

        Shift NewShift(POSTerminal t) => new()
        {
            CompanyId = companyId, BranchId = t.BranchId, POSTerminalId = t.Id, CashierUserId = 1, Status = ShiftStatus.Closed,
            OpenedAtUtc = DateTime.UtcNow, OpeningCashAmount = 100,
            DenominationCounts = { new ShiftDenominationCount { CountType = DenominationCountType.Opening, DenominationValue = 100, Count = 1 } }
        };
        var shiftA = NewShift(terminalA);
        var shiftB = NewShift(terminalB);
        db.Shifts.AddRange(shiftA, shiftB);
        await db.SaveChangesAsync();

        return new Seed(companyId, a.Id, b.Id, shiftA.Id, shiftB.Id);
    }

    [Fact]
    public async Task A_branch_session_sees_its_branch_and_company_wide_rows_and_nothing_of_other_branches()
    {
        var s = await SeedAsync();

        await using var asA = fixture.CreateContext(new TestCurrentCompanyContext(s.CompanyId, branchId: s.BranchA));
        Assert.Equal([s.BranchA], await asA.Branches.Select(b => b.Id).ToListAsync());
        Assert.Equal(["Main", "WA"], (await asA.Warehouses.Select(w => w.NameEn).ToListAsync()).Order().ToList());
        Assert.Equal([s.ShiftA], await asA.Shifts.Select(x => x.Id).ToListAsync());

        // A child row follows its parent: the other branch's shift counts are hidden too.
        var countShifts = await asA.ShiftDenominationCounts.Select(c => c.ShiftId).Distinct().ToListAsync();
        Assert.Contains(s.ShiftA, countShifts);
        Assert.DoesNotContain(s.ShiftB, countShifts);

        await using var companyWide = fixture.CreateContext(new TestCurrentCompanyContext(s.CompanyId));
        Assert.Equal(2, await companyWide.Branches.CountAsync());
        Assert.Equal(3, await companyWide.Warehouses.CountAsync());
        Assert.Equal(2, await companyWide.Shifts.CountAsync());
    }

    [Fact]
    public async Task A_branch_session_cannot_record_anything_in_another_branch()
    {
        var s = await SeedAsync();

        await using (var own = fixture.CreateContext(new TestCurrentCompanyContext(s.CompanyId, branchId: s.BranchA)))
        {
            own.CustodyOfficers.Add(new CustodyOfficer { CompanyId = s.CompanyId, BranchId = s.BranchA, Code = $"CA{Guid.NewGuid():N}"[..10], NameAr = "مسؤول", NameEn = "Officer" });
            await own.SaveChangesAsync();
        }

        await using var other = fixture.CreateContext(new TestCurrentCompanyContext(s.CompanyId, branchId: s.BranchA));
        other.CustodyOfficers.Add(new CustodyOfficer { CompanyId = s.CompanyId, BranchId = s.BranchB, Code = $"CB{Guid.NewGuid():N}"[..10], NameAr = "مسؤول", NameEn = "Officer" });
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => other.SaveChangesAsync());
        Assert.Equal("BRANCH-OUT-OF-SCOPE", ex.Code);

        // …nor move one of its own records there.
        other.ChangeTracker.Clear();
        var terminal = await other.POSTerminals.SingleAsync(t => t.BranchId == s.BranchA);
        terminal.BranchId = s.BranchB;
        await Assert.ThrowsAsync<ForbiddenException>(() => other.SaveChangesAsync());
    }

    [Fact]
    public void The_branch_rule_reaches_the_rows_reports_read_directly()
    {
        using var db = fixture.CreateContext();
        var filtered = db.Model.GetEntityTypes()
            .Where(t => t.GetDeclaredQueryFilters().Any(f => f.Expression!.ToString().Contains("CurrentBranchId")))
            .Select(t => t.ClrType.Name)
            .Order()
            .ToList();
        output.WriteLine(string.Join(", ", filtered));

        // Rows without a branch of their own that must still follow one.
        Assert.Contains(nameof(JournalEntryLine), filtered);
        Assert.Contains(nameof(StockBalance), filtered);
        Assert.Contains(nameof(StockTransaction), filtered);
        Assert.Contains(nameof(CheckLine), filtered);
        Assert.Contains(nameof(POSPayment), filtered);
        Assert.Contains(nameof(InventoryCount), filtered);
        Assert.Contains(nameof(WasteRecord), filtered);

        // Company-wide reference data stays visible to everyone in the company.
        Assert.DoesNotContain(nameof(Item), filtered);
        Assert.DoesNotContain(nameof(Account), filtered);

        // Customers (and their loyalty points) are shared by every branch.
        Assert.DoesNotContain("Customer", filtered);
        Assert.DoesNotContain("LoyaltyTransaction", filtered);
    }
}
