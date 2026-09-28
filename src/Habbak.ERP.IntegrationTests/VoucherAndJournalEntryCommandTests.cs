using Habbak.ERP.Application.Accounting.JournalEntries.Commands.CreateManualJournalEntry;
using Habbak.ERP.Application.Accounting.JournalEntries.Commands.PostJournalEntry;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.CancelVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.CreateVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.PostVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.ReverseVoucher;
using Habbak.ERP.Application.Accounting.Vouchers.Commands.UpdateVoucher;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests;

/// <summary>
/// Integration tests for the new Application-layer command handlers built on top of
/// IPostingService — in particular PostVoucherCommandHandler, since it is new orchestration
/// logic (building a 2-line PostingRequest, then committing the Voucher's own status change in
/// the SAME SaveChangesAsync call, per rule 5's atomicity requirement) that PostingServiceTests
/// alone does not exercise.
/// </summary>
public class VoucherAndJournalEntryCommandTests(PostingServiceFixture fixture) : IClassFixture<PostingServiceFixture>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private async Task<(long treasuryAccountId, long expenseAccountId)> SeedAccountsAsync(long companyId)
    {
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));

        var treasury = new Account
        {
            CompanyId = companyId, Code = "1-1-01", NameAr = "الخزينة", NameEn = "Cash",
            AccountType = AccountType.Asset, Nature = AccountNature.Debit, IsPostable = true, IsActive = true
        };
        var expense = new Account
        {
            CompanyId = companyId, Code = "5-1-01", NameAr = "مصروفات عمومية", NameEn = "General Expenses",
            AccountType = AccountType.Expense, Nature = AccountNature.Debit, IsPostable = true, IsActive = true
        };
        db.Accounts.AddRange(treasury, expense);
        await db.SaveChangesAsync();

        return (treasury.Id, expense.Id);
    }

    private static (IApplicationDbContext Db, IPostingService PostingService) BuildServices(
        Habbak.ERP.Infrastructure.Persistence.AppDbContext db, long companyId) =>
        (db, new PostingService(db, new Habbak.ERP.Application.Approvals.Services.ApprovalWorkflowService(db, new Habbak.ERP.Application.Approvals.Services.ApprovalStepResolutionService(db), new Habbak.ERP.Application.Notifications.NotificationService(db, new TestCurrentCompanyContext(companyId))), new JournalEntryNumberGenerator(new CodeGenerator(db, new TestCurrentCompanyContext(companyId)))));

    /// <summary>
    /// Bug-001: a treasury account with a mandatory dimension whose CostCenterDimension is
    /// Branch-linked, plus a Branch and its already-mirrored CostCenterDimensionValue (what
    /// BranchDimensionSync would have created on Branch create/update).
    /// </summary>
    private async Task<(long treasuryAccountId, long expenseAccountId, long branchId, long branchDimensionId)>
        SeedAccountsWithMandatoryBranchDimensionAsync(long companyId)
    {
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));

        var branch = new Branch { CompanyId = companyId, Code = "MAIN", NameAr = "الفرع الرئيسي", NameEn = "Main Branch", IsActive = true };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var branchDimension = new CostCenterDimension
        {
            CompanyId = companyId, Code = "BRANCH", NameAr = "الفروع", NameEn = "Branches",
            IsActive = true, LinkedEntityType = CostCenterLinkedEntityType.Branch
        };
        db.CostCenterDimensions.Add(branchDimension);
        await db.SaveChangesAsync();

        db.AccountDimensionLinks.Add(new AccountDimensionLink
        {
            AccountId = treasuryAccountId, CostCenterDimensionId = branchDimension.Id, DisplayOrder = 1, IsMandatory = true
        });
        db.CostCenterDimensionValues.Add(new CostCenterDimensionValue
        {
            CostCenterDimensionId = branchDimension.Id, Code = branch.Code, NameAr = branch.NameAr, NameEn = branch.NameEn, IsActive = true
        });
        await db.SaveChangesAsync();

        return (treasuryAccountId, expenseAccountId, branch.Id, branchDimension.Id);
    }

    [Fact]
    public async Task PostVoucher_PaymentWithOtherCounterparty_PostsAndLinksJournalEntryAtomically()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);

        long voucherId;
        await using (var createDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var createHandler = new CreateVoucherCommandHandler(
                createDb, new TestCurrentCompanyContext(companyId), new VoucherNumberGenerator(new CodeGenerator(createDb, new TestCurrentCompanyContext(companyId))));

            voucherId = await createHandler.Handle(new CreateVoucherCommand
            {
                VoucherType = VoucherType.Payment,
                VoucherDate = new DateOnly(2026, 8, 15),
                BranchId = 1,
                TreasuryAccountId = treasuryAccountId,
                CounterpartyType = CounterpartyType.Other,
                DirectAccountId = expenseAccountId,
                Amount = 250m,
                CurrencyCode = "EGP",
                ExchangeRate = 1m,
                BaseCurrencyAmount = 250m
            }, CancellationToken.None);
        }

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var (appDb, postingService) = BuildServices(db, companyId);
        var postHandler = new PostVoucherCommandHandler(appDb, postingService, new NotImplementedCounterpartyAccountResolver());

        var result = await postHandler.Handle(new PostVoucherCommand(voucherId), CancellationToken.None);

        Assert.Equal(nameof(JournalEntryStatus.Posted), result.Status);

        await using var verifyDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var voucher = await verifyDb.Vouchers.Include(v => v.JournalEntry).ThenInclude(j => j!.Lines)
            .SingleAsync(v => v.Id == voucherId);

        Assert.Equal(VoucherStatus.Posted, voucher.Status);
        Assert.NotNull(voucher.JournalEntryId);
        Assert.Equal(JournalEntryStatus.Posted, voucher.JournalEntry!.Status);
        Assert.Equal(250m, voucher.JournalEntry.TotalDebit);
        Assert.Equal(250m, voucher.JournalEntry.TotalCredit);

        // Payment: debit the offset (expense) account, credit the treasury account.
        var expenseLine = voucher.JournalEntry.Lines.Single(l => l.AccountId == expenseAccountId);
        var treasuryLine = voucher.JournalEntry.Lines.Single(l => l.AccountId == treasuryAccountId);
        Assert.Equal(250m, expenseLine.DebitAmount);
        Assert.Equal(250m, treasuryLine.CreditAmount);
    }

    [Fact]
    public async Task PostVoucher_CustomerCounterparty_ThrowsResolutionPendingBusinessRule()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, _) = await SeedAccountsAsync(companyId);

        long voucherId;
        await using (var createDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var createHandler = new CreateVoucherCommandHandler(
                createDb, new TestCurrentCompanyContext(companyId), new VoucherNumberGenerator(new CodeGenerator(createDb, new TestCurrentCompanyContext(companyId))));

            voucherId = await createHandler.Handle(new CreateVoucherCommand
            {
                VoucherType = VoucherType.Receipt,
                VoucherDate = new DateOnly(2026, 8, 15),
                BranchId = 1,
                TreasuryAccountId = treasuryAccountId,
                CounterpartyType = CounterpartyType.Customer,
                CounterpartyId = 999,
                Amount = 100m,
                CurrencyCode = "EGP",
                ExchangeRate = 1m,
                BaseCurrencyAmount = 100m
            }, CancellationToken.None);
        }

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var (appDb, postingService) = BuildServices(db, companyId);
        var postHandler = new PostVoucherCommandHandler(appDb, postingService, new NotImplementedCounterpartyAccountResolver());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => postHandler.Handle(new PostVoucherCommand(voucherId), CancellationToken.None));
        Assert.Equal("ACC-COUNTERPARTY-RESOLUTION-PENDING", ex.Code);
    }

    [Fact]
    public async Task CancelVoucher_WhenPosted_ThrowsBusinessRule()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var voucher = new Voucher
        {
            CompanyId = companyId, VoucherType = VoucherType.Payment, VoucherNumber = "PAY-2026-00001",
            VoucherDate = new DateOnly(2026, 8, 15), TreasuryAccountId = treasuryAccountId,
            CounterpartyType = CounterpartyType.Other, DirectAccountId = expenseAccountId,
            Amount = 50m, CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyAmount = 50m,
            Status = VoucherStatus.Posted
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();

        var handler = new CancelVoucherCommandHandler(db);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => handler.Handle(new CancelVoucherCommand(voucher.Id), CancellationToken.None));
        Assert.Equal("ACC-VOUCHER-NOT-DRAFT", ex.Code);
    }

    [Fact]
    public async Task CancelVoucher_WhenDraft_SetsCancelled()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var voucher = new Voucher
        {
            CompanyId = companyId, VoucherType = VoucherType.Payment, VoucherNumber = "PAY-2026-00002",
            VoucherDate = new DateOnly(2026, 8, 15), TreasuryAccountId = treasuryAccountId,
            CounterpartyType = CounterpartyType.Other, DirectAccountId = expenseAccountId,
            Amount = 50m, CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyAmount = 50m,
            Status = VoucherStatus.Draft
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();

        var handler = new CancelVoucherCommandHandler(db);
        await handler.Handle(new CancelVoucherCommand(voucher.Id), CancellationToken.None);

        Assert.Equal(VoucherStatus.Cancelled, voucher.Status);
    }

    [Fact]
    public async Task ReverseVoucher_WhenPosted_CreatesDraftReversalAndKeepsVoucherPosted()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);

        long voucherId;
        await using (var createDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var createHandler = new CreateVoucherCommandHandler(
                createDb, new TestCurrentCompanyContext(companyId), new VoucherNumberGenerator(new CodeGenerator(createDb, new TestCurrentCompanyContext(companyId))));

            voucherId = await createHandler.Handle(new CreateVoucherCommand
            {
                VoucherType = VoucherType.Payment,
                VoucherDate = new DateOnly(2026, 8, 15),
                BranchId = 1,
                TreasuryAccountId = treasuryAccountId,
                CounterpartyType = CounterpartyType.Other,
                DirectAccountId = expenseAccountId,
                Amount = 75m,
                CurrencyCode = "EGP",
                ExchangeRate = 1m,
                BaseCurrencyAmount = 75m
            }, CancellationToken.None);
        }

        await using (var postDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var (appDb, postingService) = BuildServices(postDb, companyId);
            var postHandler = new PostVoucherCommandHandler(appDb, postingService, new NotImplementedCounterpartyAccountResolver());
            await postHandler.Handle(new PostVoucherCommand(voucherId), CancellationToken.None);
        }

        await using var reverseDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var (reverseAppDb, reversePostingService) = BuildServices(reverseDb, companyId);
        var reverseHandler = new ReverseVoucherCommandHandler(reverseAppDb, reversePostingService);

        var reversalEntryId = await reverseHandler.Handle(new ReverseVoucherCommand(voucherId), CancellationToken.None);

        await using var verifyDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var voucher = await verifyDb.Vouchers.SingleAsync(v => v.Id == voucherId);
        var reversalEntry = await verifyDb.JournalEntries.SingleAsync(j => j.Id == reversalEntryId);

        // The voucher itself stays Posted — it is the underlying journal entry that gets reversed.
        Assert.Equal(VoucherStatus.Posted, voucher.Status);
        Assert.Equal(JournalEntryStatus.Draft, reversalEntry.Status);
        Assert.Equal(voucher.JournalEntryId, reversalEntry.ReversalOfEntryId);
    }

    [Fact]
    public async Task PostJournalEntry_ManualUnbalancedDraft_ThrowsRule1()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);

        long entryId;
        await using (var createDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var createHandler = new CreateManualJournalEntryCommandHandler(
                createDb, new TestCurrentCompanyContext(companyId), new JournalEntryNumberGenerator(new CodeGenerator(createDb, new TestCurrentCompanyContext(companyId))));

            // Deliberately unbalanced — allowed to save as a work-in-progress Draft.
            entryId = await createHandler.Handle(new CreateManualJournalEntryCommand
            {
                BranchId = 1,
                EntryDate = new DateOnly(2026, 8, 15),
                Description = "مسودة غير متزنة",
                Lines =
                [
                    new JournalEntryLineInput
                    {
                        AccountId = expenseAccountId, DebitAmount = 100m, CreditAmount = 0m,
                        CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = 100m, BaseCurrencyCreditAmount = 0m
                    },
                    new JournalEntryLineInput
                    {
                        AccountId = treasuryAccountId, DebitAmount = 0m, CreditAmount = 60m,
                        CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = 0m, BaseCurrencyCreditAmount = 60m
                    }
                ]
            }, CancellationToken.None);
        }

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var (appDb, postingService) = BuildServices(db, companyId);
        var postHandler = new PostJournalEntryCommandHandler(postingService, appDb);

        var ex = await Assert.ThrowsAsync<PostingValidationException>(
            () => postHandler.Handle(new PostJournalEntryCommand(entryId), CancellationToken.None));
        Assert.Contains(ex.Errors, e => e.Code == "ACC-R1-UNBALANCED");
    }

    [Fact]
    public async Task CreateVoucher_WithExplicitBranchId_UsesRequestValue()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);

        long branchId;
        await using (var seedDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var branch = new Branch { CompanyId = companyId, Code = "MAIN", NameAr = "الفرع الرئيسي", NameEn = "Main Branch", IsActive = true };
            seedDb.Branches.Add(branch);
            await seedDb.SaveChangesAsync();
            branchId = branch.Id;
        }

        // Session is company-wide (no branch on the context) — the request's own BranchId must win.
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var handler = new CreateVoucherCommandHandler(
            db, new TestCurrentCompanyContext(companyId), new VoucherNumberGenerator(new CodeGenerator(db, new TestCurrentCompanyContext(companyId))));

        var voucherId = await handler.Handle(new CreateVoucherCommand
        {
            VoucherType = VoucherType.Payment,
            VoucherDate = new DateOnly(2026, 8, 15),
            BranchId = branchId,
            TreasuryAccountId = treasuryAccountId,
            CounterpartyType = CounterpartyType.Other,
            DirectAccountId = expenseAccountId,
            Amount = 50m,
            CurrencyCode = "EGP",
            ExchangeRate = 1m,
            BaseCurrencyAmount = 50m
        }, CancellationToken.None);

        await using var verifyDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var voucher = await verifyDb.Vouchers.SingleAsync(v => v.Id == voucherId);
        Assert.Equal(branchId, voucher.BranchId);
    }

    [Fact]
    public async Task CreateVoucher_WithoutBranchIdButSessionScopedToBranch_FallsBackToContextBranch()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);

        long branchId;
        await using (var seedDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var branch = new Branch { CompanyId = companyId, Code = "MAIN", NameAr = "الفرع الرئيسي", NameEn = "Main Branch", IsActive = true };
            seedDb.Branches.Add(branch);
            await seedDb.SaveChangesAsync();
            branchId = branch.Id;
        }

        // Request omits BranchId — a session pinned to one branch must fall back to it.
        var scopedContext = new TestCurrentCompanyContext(companyId, branchId);
        await using var db = fixture.CreateContext(scopedContext);
        var handler = new CreateVoucherCommandHandler(
            db, scopedContext, new VoucherNumberGenerator(new CodeGenerator(db, scopedContext)));

        var voucherId = await handler.Handle(new CreateVoucherCommand
        {
            VoucherType = VoucherType.Payment,
            VoucherDate = new DateOnly(2026, 8, 15),
            TreasuryAccountId = treasuryAccountId,
            CounterpartyType = CounterpartyType.Other,
            DirectAccountId = expenseAccountId,
            Amount = 50m,
            CurrencyCode = "EGP",
            ExchangeRate = 1m,
            BaseCurrencyAmount = 50m
        }, CancellationToken.None);

        await using var verifyDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var voucher = await verifyDb.Vouchers.SingleAsync(v => v.Id == voucherId);
        Assert.Equal(branchId, voucher.BranchId);
    }

    [Fact]
    public async Task CreateVoucher_WithoutBranchIdAndCompanyWideSession_ThrowsBranchRequired()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);

        // Neither the request nor the (company-wide) session names a branch.
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var handler = new CreateVoucherCommandHandler(
            db, new TestCurrentCompanyContext(companyId), new VoucherNumberGenerator(new CodeGenerator(db, new TestCurrentCompanyContext(companyId))));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new CreateVoucherCommand
        {
            VoucherType = VoucherType.Payment,
            VoucherDate = new DateOnly(2026, 8, 15),
            TreasuryAccountId = treasuryAccountId,
            CounterpartyType = CounterpartyType.Other,
            DirectAccountId = expenseAccountId,
            Amount = 50m,
            CurrencyCode = "EGP",
            ExchangeRate = 1m,
            BaseCurrencyAmount = 50m
        }, CancellationToken.None));

        Assert.Equal("ACC-BRANCH-REQUIRED", ex.Code);
    }

    [Fact]
    public async Task UpdateVoucher_WithoutBranchIdAndCompanyWideSession_ThrowsBranchRequired()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);

        long voucherId;
        await using (var createDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var branch = new Branch { CompanyId = companyId, Code = "MAIN", NameAr = "الفرع الرئيسي", NameEn = "Main Branch", IsActive = true };
            createDb.Branches.Add(branch);
            await createDb.SaveChangesAsync();

            var createHandler = new CreateVoucherCommandHandler(
                createDb, new TestCurrentCompanyContext(companyId), new VoucherNumberGenerator(new CodeGenerator(createDb, new TestCurrentCompanyContext(companyId))));

            voucherId = await createHandler.Handle(new CreateVoucherCommand
            {
                VoucherType = VoucherType.Payment,
                VoucherDate = new DateOnly(2026, 8, 15),
                BranchId = branch.Id,
                TreasuryAccountId = treasuryAccountId,
                CounterpartyType = CounterpartyType.Other,
                DirectAccountId = expenseAccountId,
                Amount = 50m,
                CurrencyCode = "EGP",
                ExchangeRate = 1m,
                BaseCurrencyAmount = 50m
            }, CancellationToken.None);
        }

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var voucher = await db.Vouchers.SingleAsync(v => v.Id == voucherId);
        var updateHandler = new UpdateVoucherCommandHandler(db, new TestCurrentCompanyContext(companyId));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => updateHandler.Handle(new UpdateVoucherCommand
        {
            Id = voucherId,
            RowVersion = Convert.ToBase64String(voucher.RowVersion),
            // BranchId omitted, and the session is company-wide — must be rejected, not silently cleared.
            VoucherDate = new DateOnly(2026, 8, 16),
            TreasuryAccountId = treasuryAccountId,
            CounterpartyType = CounterpartyType.Other,
            DirectAccountId = expenseAccountId,
            Amount = 75m,
            CurrencyCode = "EGP",
            ExchangeRate = 1m,
            BaseCurrencyAmount = 75m
        }, CancellationToken.None));

        Assert.Equal("ACC-BRANCH-REQUIRED", ex.Code);
    }

    [Fact]
    public async Task CreateManualJournalEntry_WithoutBranchIdAndCompanyWideSession_ThrowsBranchRequired()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId) = await SeedAccountsAsync(companyId);

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var createHandler = new CreateManualJournalEntryCommandHandler(
            db, new TestCurrentCompanyContext(companyId), new JournalEntryNumberGenerator(new CodeGenerator(db, new TestCurrentCompanyContext(companyId))));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => createHandler.Handle(new CreateManualJournalEntryCommand
        {
            EntryDate = new DateOnly(2026, 8, 15),
            Description = "قيد بدون فرع",
            Lines =
            [
                new JournalEntryLineInput
                {
                    AccountId = expenseAccountId, DebitAmount = 100m, CreditAmount = 0m,
                    CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = 100m, BaseCurrencyCreditAmount = 0m
                },
                new JournalEntryLineInput
                {
                    AccountId = treasuryAccountId, DebitAmount = 0m, CreditAmount = 100m,
                    CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = 0m, BaseCurrencyCreditAmount = 100m
                }
            ]
        }, CancellationToken.None));

        Assert.Equal("ACC-BRANCH-REQUIRED", ex.Code);
    }

    [Fact]
    public async Task PostVoucher_TreasuryAccountHasMandatoryBranchDimension_AutoResolvesAndPosts()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId, branchId, branchDimensionId) =
            await SeedAccountsWithMandatoryBranchDimensionAsync(companyId);

        long voucherId;
        await using (var createDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var createHandler = new CreateVoucherCommandHandler(
                createDb, new TestCurrentCompanyContext(companyId), new VoucherNumberGenerator(new CodeGenerator(createDb, new TestCurrentCompanyContext(companyId))));

            voucherId = await createHandler.Handle(new CreateVoucherCommand
            {
                VoucherType = VoucherType.Payment,
                VoucherDate = new DateOnly(2026, 8, 15),
                BranchId = branchId,
                TreasuryAccountId = treasuryAccountId,
                CounterpartyType = CounterpartyType.Other,
                DirectAccountId = expenseAccountId,
                Amount = 250m,
                CurrencyCode = "EGP",
                ExchangeRate = 1m,
                BaseCurrencyAmount = 250m
            }, CancellationToken.None);
        }

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var (appDb, postingService) = BuildServices(db, companyId);
        var postHandler = new PostVoucherCommandHandler(appDb, postingService, new NotImplementedCounterpartyAccountResolver());

        var result = await postHandler.Handle(new PostVoucherCommand(voucherId), CancellationToken.None);

        Assert.Equal(nameof(JournalEntryStatus.Posted), result.Status);

        await using var verifyDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var voucher = await verifyDb.Vouchers.Include(v => v.JournalEntry).ThenInclude(j => j!.Lines).ThenInclude(l => l.DimensionValues)
            .SingleAsync(v => v.Id == voucherId);

        var treasuryLine = voucher.JournalEntry!.Lines.Single(l => l.AccountId == treasuryAccountId);
        var resolvedDimension = Assert.Single(treasuryLine.DimensionValues);
        Assert.Equal(branchDimensionId, resolvedDimension.CostCenterDimensionId);
    }

    [Fact]
    public async Task PostJournalEntry_LineAccountHasMandatoryBranchDimension_AutoResolvesAndPosts()
    {
        var companyId = NewCompanyId();
        var (treasuryAccountId, expenseAccountId, branchId, branchDimensionId) =
            await SeedAccountsWithMandatoryBranchDimensionAsync(companyId);

        long entryId;
        await using (var createDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var createHandler = new CreateManualJournalEntryCommandHandler(
                createDb, new TestCurrentCompanyContext(companyId), new JournalEntryNumberGenerator(new CodeGenerator(createDb, new TestCurrentCompanyContext(companyId))));

            entryId = await createHandler.Handle(new CreateManualJournalEntryCommand
            {
                BranchId = branchId,
                EntryDate = new DateOnly(2026, 8, 15),
                Description = "قيد يدوي بحساب عليه بُعد فرع إلزامي",
                Lines =
                [
                    new JournalEntryLineInput
                    {
                        AccountId = expenseAccountId, DebitAmount = 100m, CreditAmount = 0m,
                        CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = 100m, BaseCurrencyCreditAmount = 0m
                    },
                    new JournalEntryLineInput
                    {
                        AccountId = treasuryAccountId, DebitAmount = 0m, CreditAmount = 100m,
                        CurrencyCode = "EGP", ExchangeRate = 1m, BaseCurrencyDebitAmount = 0m, BaseCurrencyCreditAmount = 100m
                        // No DimensionValues — must be auto-resolved from the entry's BranchId.
                    }
                ]
            }, CancellationToken.None);
        }

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var (appDb, postingService) = BuildServices(db, companyId);
        var postHandler = new PostJournalEntryCommandHandler(postingService, appDb);

        await postHandler.Handle(new PostJournalEntryCommand(entryId), CancellationToken.None);

        await using var verifyDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var entry = await verifyDb.JournalEntries.Include(j => j.Lines).ThenInclude(l => l.DimensionValues)
            .SingleAsync(j => j.Id == entryId);

        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        var treasuryLine = entry.Lines.Single(l => l.AccountId == treasuryAccountId);
        var resolvedDimension = Assert.Single(treasuryLine.DimensionValues);
        Assert.Equal(branchDimensionId, resolvedDimension.CostCenterDimensionId);
    }
}
