using Habbak.ERP.Application.Accounting.TreasuryTransfers.Commands.CreateTreasuryTransfer;
using Habbak.ERP.Application.Accounting.TreasuryTransfers.Commands.PostTreasuryTransfer;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests;

/// <summary>
/// Bug-001: TreasuryTransfer posts through the same central IPostingService as Voucher and
/// JournalEntry (PostTreasuryTransferCommand.cs), so the same mandatory-Branch-dimension
/// auto-resolve fix in PostingService.ValidateAndThrowAsync must cover it too, with no
/// TreasuryTransfer-specific code change.
/// </summary>
public class TreasuryTransferCommandTests(PostingServiceFixture fixture) : IClassFixture<PostingServiceFixture>
{
    private static long NewCompanyId() => Random.Shared.NextInt64(1, long.MaxValue);

    private async Task<(long fromAccountId, long toAccountId, long branchId, long branchDimensionId)>
        SeedAccountsWithMandatoryBranchDimensionAsync(long companyId)
    {
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));

        var fromAccount = new Account
        {
            CompanyId = companyId, Code = "1-1-01", NameAr = "الخزينة", NameEn = "Cash",
            AccountType = AccountType.Asset, Nature = AccountNature.Debit, IsPostable = true, IsActive = true
        };
        var toAccount = new Account
        {
            CompanyId = companyId, Code = "1-1-02", NameAr = "البنك الأهلي", NameEn = "Bank",
            AccountType = AccountType.Asset, Nature = AccountNature.Debit, IsPostable = true, IsActive = true
        };
        db.Accounts.AddRange(fromAccount, toAccount);
        await db.SaveChangesAsync();

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
            AccountId = toAccount.Id, CostCenterDimensionId = branchDimension.Id, DisplayOrder = 1, IsMandatory = true
        });
        db.CostCenterDimensionValues.Add(new CostCenterDimensionValue
        {
            CostCenterDimensionId = branchDimension.Id, Code = branch.Code, NameAr = branch.NameAr, NameEn = branch.NameEn, IsActive = true
        });
        await db.SaveChangesAsync();

        return (fromAccount.Id, toAccount.Id, branch.Id, branchDimension.Id);
    }

    private async Task<(long fromAccountId, long toAccountId)> SeedPlainAccountsAsync(long companyId)
    {
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));

        var fromAccount = new Account
        {
            CompanyId = companyId, Code = "1-1-01", NameAr = "الخزينة", NameEn = "Cash",
            AccountType = AccountType.Asset, Nature = AccountNature.Debit, IsPostable = true, IsActive = true
        };
        var toAccount = new Account
        {
            CompanyId = companyId, Code = "1-1-02", NameAr = "البنك الأهلي", NameEn = "Bank",
            AccountType = AccountType.Asset, Nature = AccountNature.Debit, IsPostable = true, IsActive = true
        };
        db.Accounts.AddRange(fromAccount, toAccount);
        await db.SaveChangesAsync();

        return (fromAccount.Id, toAccount.Id);
    }

    [Fact]
    public async Task CreateTreasuryTransfer_WithoutBranchIdAndCompanyWideSession_ThrowsBranchRequired()
    {
        var companyId = NewCompanyId();
        var (fromAccountId, toAccountId) = await SeedPlainAccountsAsync(companyId);

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var handler = new CreateTreasuryTransferCommandHandler(db, new TestCurrentCompanyContext(companyId));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(new CreateTreasuryTransferCommand
        {
            // BranchId omitted, and the session is company-wide.
            FromTreasuryAccountId = fromAccountId,
            ToTreasuryAccountId = toAccountId,
            Amount = 500m,
            TransferDate = new DateOnly(2026, 8, 15)
        }, CancellationToken.None));

        Assert.Equal("ACC-BRANCH-REQUIRED", ex.Code);
    }

    [Fact]
    public async Task CreateTreasuryTransfer_WithoutBranchIdButSessionScopedToBranch_FallsBackToContextBranch()
    {
        var companyId = NewCompanyId();
        var (fromAccountId, toAccountId) = await SeedPlainAccountsAsync(companyId);

        long branchId;
        await using (var seedDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var branch = new Branch { CompanyId = companyId, Code = "MAIN", NameAr = "الفرع الرئيسي", NameEn = "Main Branch", IsActive = true };
            seedDb.Branches.Add(branch);
            await seedDb.SaveChangesAsync();
            branchId = branch.Id;
        }

        var scopedContext = new TestCurrentCompanyContext(companyId, branchId);
        await using var db = fixture.CreateContext(scopedContext);
        var handler = new CreateTreasuryTransferCommandHandler(db, scopedContext);

        var transferId = await handler.Handle(new CreateTreasuryTransferCommand
        {
            FromTreasuryAccountId = fromAccountId,
            ToTreasuryAccountId = toAccountId,
            Amount = 500m,
            TransferDate = new DateOnly(2026, 8, 15)
        }, CancellationToken.None);

        await using var verifyDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var transfer = await verifyDb.TreasuryTransfers.SingleAsync(t => t.Id == transferId);
        Assert.Equal(branchId, transfer.BranchId);
    }

    [Fact]
    public async Task PostTreasuryTransfer_ToAccountHasMandatoryBranchDimension_AutoResolvesAndPosts()
    {
        var companyId = NewCompanyId();
        var (fromAccountId, toAccountId, branchId, branchDimensionId) =
            await SeedAccountsWithMandatoryBranchDimensionAsync(companyId);

        long transferId;
        await using (var createDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId)))
        {
            var createHandler = new CreateTreasuryTransferCommandHandler(createDb, new TestCurrentCompanyContext(companyId));

            transferId = await createHandler.Handle(new CreateTreasuryTransferCommand
            {
                BranchId = branchId,
                FromTreasuryAccountId = fromAccountId,
                ToTreasuryAccountId = toAccountId,
                Amount = 500m,
                TransferDate = new DateOnly(2026, 8, 15)
            }, CancellationToken.None);
        }

        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        IApplicationDbContext appDb = db;
        IPostingService postingService = new PostingService(
            db, new Habbak.ERP.Application.Approvals.Services.ApprovalWorkflowService(db, new Habbak.ERP.Application.Approvals.Services.ApprovalStepResolutionService(db), new Habbak.ERP.Application.Notifications.NotificationService(db, new TestCurrentCompanyContext(companyId))), new JournalEntryNumberGenerator(new CodeGenerator(db, new TestCurrentCompanyContext(companyId))));
        var postHandler = new PostTreasuryTransferCommandHandler(appDb, postingService);

        var result = await postHandler.Handle(new PostTreasuryTransferCommand(transferId), CancellationToken.None);

        Assert.Equal(nameof(JournalEntryStatus.Posted), result.Status);

        await using var verifyDb = fixture.CreateContext(new TestCurrentCompanyContext(companyId));
        var transfer = await verifyDb.TreasuryTransfers.Include(t => t.JournalEntry).ThenInclude(j => j!.Lines).ThenInclude(l => l.DimensionValues)
            .SingleAsync(t => t.Id == transferId);

        var toLine = transfer.JournalEntry!.Lines.Single(l => l.AccountId == toAccountId);
        var resolvedDimension = Assert.Single(toLine.DimensionValues);
        Assert.Equal(branchDimensionId, resolvedDimension.CostCenterDimensionId);
    }
}
