using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Posting.Resolvers;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Application.Posting.Templates;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.Posting;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Infrastructure.Persistence;
using Habbak.ERP.Infrastructure.Services;
using Habbak.ERP.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Habbak.ERP.IntegrationTests;

/// <summary>
/// The posting template engine (Docs/Posting-Engine-Implementation-Plan.md, stage 1) against a real
/// database: the filtered unique indexes behind versioning and idempotency only exist in SQL Server.
///
/// One template stands in for a purchase invoice throughout:
///   L1  Dr Inventory            Subtotal   branch cost center (resolver)
///   L2  Dr company VatReceivable TaxAmount  only when TaxAmount &gt; 0
///   L3  Cr supplier payable      TotalAmount supplier's own account, else company DefaultPayable
/// </summary>
public class PostingTemplateEngineTests(PostingServiceFixture fixture) : IClassFixture<PostingServiceFixture>
{
    private const string Screen = PostingScreenCatalog.PurchaseInvoice;

    private sealed record Seed(
        long CompanyId, long BranchId, long BareBranchId, long InventoryId, long VatId, long DefaultPayableId,
        long OwnPayableId, long BranchDimensionId, long BranchValueId, long PlainDimensionId,
        long SupplierWithoutAccountId, long SupplierWithAccountId, long TemplateId);

    private async Task<Seed> SeedAsync(bool mapVat = false)
    {
        long companyId;
        await using (var db = fixture.CreateContext())
        {
            var currency = await db.Currencies.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Code == "EGP")
                ?? new Currency { Code = "EGP", NameAr = "جنيه مصري", NameEn = "Egyptian Pound" };
            var company = new Company
            {
                Code = $"T{Random.Shared.Next(1_000_000, 9_999_999)}", NameAr = "شركة اختبار", NameEn = "Test Co",
                BaseCurrency = currency
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            companyId = company.Id;
        }

        await using var ctx = fixture.CreateContext(new TestCurrentCompanyContext(companyId));

        Account NewAccount(string code, AccountType type) => new()
        {
            CompanyId = companyId, Code = code, NameAr = code, NameEn = code, AccountType = type,
            Nature = type is AccountType.Asset or AccountType.Expense ? AccountNature.Debit : AccountNature.Credit,
            IsPostable = true, IsActive = true
        };

        var inventory = NewAccount("1-3-01", AccountType.Asset);
        var vat = NewAccount("1-4-01", AccountType.Asset);
        var defaultPayable = NewAccount("2-1-01", AccountType.Liability);
        var ownPayable = NewAccount("2-1-02", AccountType.Liability);
        ctx.Accounts.AddRange(inventory, vat, defaultPayable, ownPayable);

        var branch = new Branch { CompanyId = companyId, Code = "BR1", NameAr = "فرع 1", NameEn = "Branch 1" };
        var bareBranch = new Branch { CompanyId = companyId, Code = "BR2", NameAr = "فرع 2", NameEn = "Branch 2" };
        ctx.Branches.AddRange(branch, bareBranch);

        var branchDimension = new CostCenterDimension
        {
            CompanyId = companyId, Code = "BRANCH", NameAr = "الفرع", NameEn = "Branch",
            LinkedEntityType = CostCenterLinkedEntityType.Branch
        };
        var plainDimension = new CostCenterDimension
        {
            CompanyId = companyId, Code = CostCenterDimensionCodes.CostCenter, NameAr = "مركز التكلفة", NameEn = "Cost Center"
        };
        ctx.CostCenterDimensions.AddRange(branchDimension, plainDimension);
        await ctx.SaveChangesAsync();

        // Only BR1 has its mirrored value, as BranchDimensionSync would leave it; BR2 is the gap.
        var branchValue = new CostCenterDimensionValue
        {
            CostCenterDimensionId = branchDimension.Id, Code = "BR1", NameAr = "فرع 1", NameEn = "Branch 1"
        };
        ctx.CostCenterDimensionValues.Add(branchValue);
        ctx.AccountDimensionLinks.Add(new AccountDimensionLink
        {
            AccountId = inventory.Id, CostCenterDimensionId = branchDimension.Id, DisplayOrder = 1, IsMandatory = true
        });

        var withoutAccount = new Supplier
        {
            CompanyId = companyId, Code = "S1", NameAr = "مورد 1", NameEn = "S1",
            PaymentTerms = SupplierPaymentTerms.Cash, CurrencyCode = "EGP"
        };
        var withAccount = new Supplier
        {
            CompanyId = companyId, Code = "S2", NameAr = "مورد 2", NameEn = "S2",
            PaymentTerms = SupplierPaymentTerms.Cash, CurrencyCode = "EGP", PayableAccountId = ownPayable.Id
        };
        ctx.Suppliers.AddRange(withoutAccount, withAccount);

        ctx.CompanyAccountMappings.Add(new CompanyAccountMapping
        {
            CompanyId = companyId, Role = CompanyAccountRole.DefaultPayable, AccountId = defaultPayable.Id
        });
        if (mapVat)
        {
            ctx.CompanyAccountMappings.Add(new CompanyAccountMapping
            {
                CompanyId = companyId, Role = CompanyAccountRole.VatReceivable, AccountId = vat.Id
            });
        }
        await ctx.SaveChangesAsync();

        var templateId = await CreateHandler(ctx, companyId).Handle(
            new CreatePostingTemplateCommand(Screen, PurchaseDefinition(inventory.Id, branchDimension.Id)),
            CancellationToken.None);

        return new Seed(companyId, branch.Id, bareBranch.Id, inventory.Id, vat.Id, defaultPayable.Id, ownPayable.Id,
            branchDimension.Id, branchValue.Id, plainDimension.Id, withoutAccount.Id, withAccount.Id, templateId);
    }

    private static PostingTemplateDefinition PurchaseDefinition(long inventoryId, long branchDimensionId, string name = "فاتورة مشتريات") =>
        new(name, "Purchase invoice", null,
        [
            new PostingTemplateLineInput(1, PostingDirection.Debit, AccountSourceType.Fixed, inventoryId, null, null,
                AmountFormulaType.DirectField, "Subtotal", ConditionType.None, null, null, null,
                [new PostingTemplateLineCostCenterInput(branchDimensionId, CostCenterSourceType.Dynamic, null, null, "Branch.ToCostCenterValue", 1)]),
            new PostingTemplateLineInput(2, PostingDirection.Debit, AccountSourceType.FromCompany, null, null, nameof(CompanyAccountRole.VatReceivable),
                AmountFormulaType.DirectField, "TaxAmount", ConditionType.FieldGreaterThanZero, "TaxAmount", null, null, []),
            new PostingTemplateLineInput(3, PostingDirection.Credit, AccountSourceType.Resolver, null, null, "Supplier.PayableAccountId",
                AmountFormulaType.DirectField, "TotalAmount", ConditionType.None, null, null, null, [])
        ]);

    // ------------------------------------------------------------------ wiring

    private static PostingResolverRegistry Registry(AppDbContext db) =>
        new([new SupplierPayableAccountResolver(db), new CustomerReceivableAccountResolver(db)], [new BranchCostCenterResolver(db)]);

    private static CreatePostingTemplateCommandHandler CreateHandler(AppDbContext db, long companyId) =>
        new(db, new TestCurrentCompanyContext(companyId), Registry(db));

    private static PostingTemplateEngine Engine(AppDbContext db, long companyId, IApprovalWorkflowService? approvals = null) =>
        new(db,
            new PostingService(db, approvals ?? new Habbak.ERP.Application.Approvals.Services.ApprovalWorkflowService(db, new Habbak.ERP.Application.Approvals.Services.ApprovalStepResolutionService(db), new Habbak.ERP.Application.Notifications.NotificationService(db, new TestCurrentCompanyContext(companyId))),
                new JournalEntryNumberGenerator(new CodeGenerator(db, new TestCurrentCompanyContext(companyId)))),
            Registry(db));

    private static TemplatePostingRequest Invoice(
        Seed seed, decimal subtotal, decimal tax, decimal? total = null, long? supplierId = null, long? branchId = null,
        long documentId = 1, Guid? key = null, string paymentTerms = "Net30", long? warehouseId = null) => new()
    {
        CompanyId = seed.CompanyId,
        BranchId = branchId ?? seed.BranchId,
        ScreenCode = Screen,
        SourceModule = SourceModule.Purchasing,
        SourceDocumentType = SourceDocumentType.Invoice,
        SourceDocumentId = documentId,
        EntryDate = new DateOnly(2026, 9, 18),
        Description = "فاتورة مشتريات",
        IdempotencyKey = key ?? Guid.NewGuid(),
        Context = PostingContext.Create(new Dictionary<string, object?>
        {
            ["Subtotal"] = subtotal,
            ["TaxAmount"] = tax,
            ["TotalAmount"] = total ?? subtotal + tax,
            ["SupplierId"] = supplierId ?? seed.SupplierWithoutAccountId,
            ["BranchId"] = branchId ?? seed.BranchId,
            ["WarehouseId"] = warehouseId,
            ["NetAmount"] = subtotal,
            ["DiscountAmount"] = 0m,
            ["AdditionalCosts"] = 0m,
            ["PaymentTerms"] = paymentTerms
        })
    };

    private static async Task<string> FailureCode(Func<Task> act) =>
        (await Assert.ThrowsAsync<BusinessRuleException>(act)).Code;

    // ------------------------------------------------------------------ building the entry

    [Fact]
    public async Task Post_SkipsFalseCondition_ResolvesBranchAndFallbackPayable_AndSavesSnapshot()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));

        var result = await Engine(db, seed.CompanyId).PostAsync(Invoice(seed, subtotal: 1000m, tax: 0m));
        await db.SaveChangesAsync();

        await using var check = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));
        var entry = await check.JournalEntries
            .Include(e => e.Lines).ThenInclude(l => l.DimensionValues)
            .SingleAsync(e => e.Id == result.JournalEntry!.Id);

        Assert.Equal(JournalEntryStatus.Posted, entry.Status);
        Assert.True(entry.IsAutoGenerated);
        Assert.Equal(seed.BranchId, entry.BranchId);
        Assert.Equal(2, entry.Lines.Count); // the VAT line was skipped by its condition, not failed

        var inventoryLine = entry.Lines.Single(l => l.AccountId == seed.InventoryId);
        Assert.Equal(1000m, inventoryLine.DebitAmount);
        Assert.Equal(seed.BranchValueId, inventoryLine.DimensionValues.Single().CostCenterDimensionValueId);

        var payableLine = entry.Lines.Single(l => l.CreditAmount > 0);
        Assert.Equal(seed.DefaultPayableId, payableLine.AccountId);

        var snapshot = await check.JournalEntryTemplateSnapshots.SingleAsync(s => s.JournalEntryId == entry.Id);
        Assert.Equal(seed.TemplateId, snapshot.PostingTemplateId);
        Assert.Equal(1, snapshot.TemplateVersionNumber);
        Assert.Contains("Supplier.PayableAccountId", snapshot.TemplateSnapshotJson);
    }

    [Fact]
    public async Task Build_SupplierOwnPayableAccount_WinsOverCompanyDefault()
    {
        var seed = await SeedAsync(mapVat: true);
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));

        var built = (await Engine(db, seed.CompanyId).BuildAsync(
            Invoice(seed, subtotal: 1000m, tax: 140m, supplierId: seed.SupplierWithAccountId))).Single();

        Assert.Equal(3, built.Request.Lines.Count);
        Assert.Equal(seed.OwnPayableId, built.Request.Lines.Single(l => l.CreditAmount > 0).AccountId);
        Assert.Equal(140m, built.Request.Lines.Single(l => l.AccountId == seed.VatId).DebitAmount);
    }

    [Fact]
    public async Task Build_ConditionPassesButCompanyRoleUnmapped_FailsInsteadOfSkipping()
    {
        var seed = await SeedAsync(mapVat: false);
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));

        Assert.Equal("POST-ACCOUNT-UNRESOLVED",
            await FailureCode(() => Engine(db, seed.CompanyId).BuildAsync(Invoice(seed, subtotal: 1000m, tax: 140m))));
    }

    [Fact]
    public async Task Build_UnconditionalLineWithZeroAmount_Fails()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));

        Assert.Equal("POST-AMOUNT-NOT-POSITIVE",
            await FailureCode(() => Engine(db, seed.CompanyId).BuildAsync(Invoice(seed, subtotal: 0m, tax: 0m))));
    }

    [Fact]
    public async Task Build_DocumentTotalsThatDoNotAddUp_FailsAsUnbalanced()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));

        Assert.Equal("POST-UNBALANCED",
            await FailureCode(() => Engine(db, seed.CompanyId).BuildAsync(Invoice(seed, subtotal: 1000m, tax: 0m, total: 990m))));
    }

    [Fact]
    public async Task Build_BranchWithoutCostCenterValue_Fails()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));

        Assert.Equal("POST-COSTCENTER-UNRESOLVED",
            await FailureCode(() => Engine(db, seed.CompanyId).BuildAsync(Invoice(seed, 1000m, 0m, branchId: seed.BareBranchId))));
    }

    [Fact]
    public async Task Build_MissingDocumentField_NamesTheField()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));
        var request = Invoice(seed, 1000m, 0m);
        var withoutTotal = new TemplatePostingRequest
        {
            CompanyId = request.CompanyId, BranchId = request.BranchId, ScreenCode = request.ScreenCode,
            SourceModule = request.SourceModule, SourceDocumentId = request.SourceDocumentId, EntryDate = request.EntryDate,
            Description = request.Description, IdempotencyKey = request.IdempotencyKey,
            Context = PostingContext.Create(new Dictionary<string, object?>
            {
                ["Subtotal"] = 1000m, ["TaxAmount"] = 0m, ["SupplierId"] = seed.SupplierWithoutAccountId, ["BranchId"] = seed.BranchId
            })
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => Engine(db, seed.CompanyId).BuildAsync(withoutTotal));
        Assert.Equal("POST-FIELD-MISSING", ex.Code);
        Assert.Contains("TotalAmount", ex.Message);
    }

    [Fact]
    public async Task Build_EveryLineSkipped_FailsAsEmptyEntry()
    {
        var seed = await SeedAsync(mapVat: true);
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));
        await CreateHandler(db, seed.CompanyId).Handle(new CreatePostingTemplateCommand(PostingScreenCatalog.Waste,
            new PostingTemplateDefinition("اختياري", "Optional", null,
            [
                new PostingTemplateLineInput(1, PostingDirection.Debit, AccountSourceType.Fixed, seed.VatId, null, null,
                    AmountFormulaType.DirectField, "CostAmount", ConditionType.FieldGreaterThanZero, "CostAmount", null, null, []),
                new PostingTemplateLineInput(2, PostingDirection.Credit, AccountSourceType.Fixed, seed.DefaultPayableId, null, null,
                    AmountFormulaType.DirectField, "CostAmount", ConditionType.FieldGreaterThanZero, "CostAmount", null, null, [])
            ])), CancellationToken.None);

        var request = new TemplatePostingRequest
        {
            CompanyId = seed.CompanyId, ScreenCode = PostingScreenCatalog.Waste, SourceModule = SourceModule.Inventory, SourceDocumentId = 1,
            EntryDate = new DateOnly(2026, 9, 18), Description = "اختبار", IdempotencyKey = Guid.NewGuid(),
            Context = PostingContext.Create(new Dictionary<string, object?> { ["CostAmount"] = 0m })
        };

        Assert.Equal("POST-EMPTY-ENTRY", await FailureCode(() => Engine(db, seed.CompanyId).BuildAsync(request)));
    }

    [Fact]
    public async Task Build_InactiveTemplate_IsNotFound()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));
        await new SetPostingTemplateActiveCommandHandler(db).Handle(
            new SetPostingTemplateActiveCommand(seed.TemplateId, false), CancellationToken.None);

        Assert.Equal("POST-TEMPLATE-NOT-FOUND",
            await FailureCode(() => Engine(db, seed.CompanyId).BuildAsync(Invoice(seed, 1000m, 0m))));
    }

    // ------------------------------------------------------------------ posting guarantees

    [Fact]
    public async Task Post_SameIdempotencyKey_ReturnsTheSameEntry()
    {
        var seed = await SeedAsync();
        var key = Guid.NewGuid();

        long firstId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            var first = await Engine(db, seed.CompanyId).PostAsync(Invoice(seed, 1000m, 0m, documentId: 7, key: key));
            await db.SaveChangesAsync();
            firstId = first.JournalEntry!.Id;
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            var again = await Engine(db, seed.CompanyId).PostAsync(Invoice(seed, 1000m, 0m, documentId: 7, key: key));
            await db.SaveChangesAsync();

            Assert.True(again.IsReplay);
            Assert.Equal(firstId, again.JournalEntry!.Id);
            Assert.Equal(1, await db.JournalEntries.CountAsync(e => e.SourceDocumentId == 7));

            Assert.Equal("POST-IDEMPOTENCY-KEY-REUSED",
                await FailureCode(() => Engine(db, seed.CompanyId).PostAsync(Invoice(seed, 1000m, 0m, documentId: 8, key: key))));
        }
    }

    private sealed class AlwaysRequireApproval : IApprovalWorkflowService
    {
        public Task<long?> TryStartApprovalAsync(ApprovalWorkflowTrigger trigger, CancellationToken cancellationToken = default) =>
            Task.FromResult<long?>(1);
    }

    [Fact]
    public async Task Post_AutoGeneratedEntry_SkipsApprovalWorkflow()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));

        // Spec rules 4 and 22: an entry the system generates from an approved document is posted
        // directly — the document was the thing that needed approving.
        var result = await Engine(db, seed.CompanyId, new AlwaysRequireApproval()).PostAsync(Invoice(seed, 1000m, 0m));

        Assert.Equal(JournalEntryStatus.Posted, result.JournalEntry!.Status);
    }

    // ------------------------------------------------------------------ reversing a document's entry

    private async Task<long> PostOneAsync(Seed seed)
    {
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));
        var result = await Engine(db, seed.CompanyId).PostAsync(Invoice(seed, 1000m, 0m));
        await db.SaveChangesAsync();
        return result.JournalEntry!.Id;
    }

    [Fact]
    public async Task Reverse_PostsMirrorAtOnce_AndRepeatingReturnsTheSameReversal()
    {
        var seed = await SeedAsync();
        var originalId = await PostOneAsync(seed);
        var date = new DateOnly(2026, 9, 20);

        long reversalId;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            var reversal = await Engine(db, seed.CompanyId).ReverseAsync(originalId, date, "إلغاء");
            await db.SaveChangesAsync();
            reversalId = reversal.Id;

            Assert.Equal(JournalEntryStatus.Posted, reversal.Status);
            Assert.True(reversal.IsAutoGenerated);
            Assert.Equal(SourceModule.Purchasing, reversal.SourceModule);
            Assert.Equal(date, reversal.EntryDate);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            var again = await Engine(db, seed.CompanyId).ReverseAsync(originalId, date, "إلغاء");
            Assert.Equal(reversalId, again.Id);

            var lines = await db.JournalEntryLines.Include(l => l.DimensionValues)
                .Where(l => l.JournalEntryId == reversalId).ToListAsync();
            Assert.Equal(1000m, lines.Single(l => l.AccountId == seed.InventoryId).CreditAmount);
            // Dimensions travel with the mirror, so branch reports net to zero too.
            Assert.Equal(seed.BranchValueId, lines.Single(l => l.AccountId == seed.InventoryId).DimensionValues.Single().CostCenterDimensionValueId);
        }
    }

    [Fact]
    public async Task Reverse_WhenAManualDraftReversalIsPending_Fails()
    {
        var seed = await SeedAsync();
        var originalId = await PostOneAsync(seed);

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            var manual = new PostingService(db, new Habbak.ERP.Application.Approvals.Services.ApprovalWorkflowService(db, new Habbak.ERP.Application.Approvals.Services.ApprovalStepResolutionService(db), new Habbak.ERP.Application.Notifications.NotificationService(db, new TestCurrentCompanyContext(seed.CompanyId))),
                new JournalEntryNumberGenerator(new CodeGenerator(db, new TestCurrentCompanyContext(seed.CompanyId))));
            await manual.ReverseAsync(originalId); // leaves a Draft, as the journal screen does
            await db.SaveChangesAsync();
        }

        await using var check = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));
        Assert.Equal("POST-REVERSAL-PENDING",
            await FailureCode(() => Engine(check, seed.CompanyId).ReverseAsync(originalId, new DateOnly(2026, 9, 20), "إلغاء")));
    }

    // ------------------------------------------------------------------ template maintenance

    [Fact]
    public async Task Update_BeforeAnyPosting_EditsInPlace_AfterPosting_CreatesNextVersion()
    {
        var seed = await SeedAsync();

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            var inPlace = await new UpdatePostingTemplateCommandHandler(db, Registry(db)).Handle(
                new UpdatePostingTemplateCommand(seed.TemplateId, PurchaseDefinition(seed.InventoryId, seed.BranchDimensionId, "مشتريات - معدّل")),
                CancellationToken.None);

            Assert.Equal(seed.TemplateId, inPlace.Id);
            Assert.Equal(1, inPlace.VersionNumber);
            Assert.False(inPlace.IsNewVersion);
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            await Engine(db, seed.CompanyId).PostAsync(Invoice(seed, 1000m, 0m));
            await db.SaveChangesAsync();
        }

        UpdatePostingTemplateResult versioned;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            versioned = await new UpdatePostingTemplateCommandHandler(db, Registry(db)).Handle(
                new UpdatePostingTemplateCommand(seed.TemplateId, PurchaseDefinition(seed.InventoryId, seed.BranchDimensionId, "مشتريات - إصدار 2")),
                CancellationToken.None);
        }

        Assert.True(versioned.IsNewVersion);
        Assert.Equal(2, versioned.VersionNumber);
        Assert.NotEqual(seed.TemplateId, versioned.Id);

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            var old = await db.PostingTemplates.Include(t => t.Lines).SingleAsync(t => t.Id == seed.TemplateId);
            Assert.False(old.IsCurrentVersion);
            Assert.Equal(3, old.Lines.Count); // the version entries point at keeps its lines

            var next = await Engine(db, seed.CompanyId).PostAsync(Invoice(seed, 500m, 0m, documentId: 2));
            Assert.Equal(versioned.Id, next.Entries.Single().PostingTemplateId);
            Assert.Equal(2, next.Entries.Single().TemplateVersionNumber);

            Assert.Equal("POST-TEMPLATE-NOT-CURRENT", await FailureCode(() =>
                new UpdatePostingTemplateCommandHandler(db, Registry(db)).Handle(
                    new UpdatePostingTemplateCommand(seed.TemplateId, PurchaseDefinition(seed.InventoryId, seed.BranchDimensionId)),
                    CancellationToken.None)));
        }
    }

    // ------------------------------------------------------------------ several templates per screen (design notes 2026-09-18)

    private static PostingTemplateLineInput FixedLine(int n, PostingDirection d, long account, AmountFormulaType formula,
        string? field = null, IReadOnlyList<string>? fields = null, decimal? multiplier = null, decimal? percentage = null,
        IReadOnlyList<PostingTemplateLineCostCenterInput>? costCenters = null) =>
        new(n, d, AccountSourceType.Fixed, account, null, null, formula, field, ConditionType.None, null, null, null,
            costCenters ?? [], fields, multiplier, percentage);

    [Fact]
    public async Task SecondTemplate_PostsItsOwnEntry_InOneGroup_AndCancellingReversesBoth()
    {
        var seed = await SeedAsync();
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            // A second entry for the same invoice: 14% of the subtotal, the design notes' PercentageOf example.
            await CreateHandler(db, seed.CompanyId).Handle(new CreatePostingTemplateCommand(Screen,
                new PostingTemplateDefinition("قيد تاني", "Second", null,
                [
                    FixedLine(1, PostingDirection.Debit, seed.VatId, AmountFormulaType.PercentageOf, "Subtotal", percentage: 14m),
                    FixedLine(2, PostingDirection.Credit, seed.DefaultPayableId, AmountFormulaType.Multiply, "Subtotal", multiplier: 0.14m)
                ], ExecutionOrder: 2)), CancellationToken.None);
        }

        var key = Guid.NewGuid();
        TemplatePostingResult result;
        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            result = await Engine(db, seed.CompanyId).PostAsync(Invoice(seed, 870m, 0m, documentId: 31, key: key));
            await db.SaveChangesAsync();
        }

        Assert.Equal(2, result.Entries.Count);
        Assert.All(result.Entries, e => Assert.Equal(result.PostingGroupId, e.JournalEntry.PostingGroupId));
        await using (var check = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            var second = await check.JournalEntryLines.Where(l => l.JournalEntryId == result.Entries[1].JournalEntry.Id).ToListAsync();
            Assert.Equal(121.80m, second.Single(l => l.AccountId == seed.VatId).DebitAmount);   // 870 × 14%
        }

        await using (var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId)))
        {
            var again = await Engine(db, seed.CompanyId).PostAsync(Invoice(seed, 870m, 0m, documentId: 31, key: key));
            Assert.True(again.IsReplay);
            Assert.Equal(2, again.Entries.Count);

            await Engine(db, seed.CompanyId).ReverseAsync(result.JournalEntry!.Id, new DateOnly(2026, 9, 20), "إلغاء");
            await db.SaveChangesAsync();

            var reversed = await db.JournalEntries.Where(e => e.ReversalOfEntryId != null).Select(e => e.ReversalOfEntryId).ToListAsync();
            Assert.Equal(result.Entries.Select(e => (long?)e.JournalEntry.Id).OrderBy(x => x), reversed.OrderBy(x => x));
        }
    }

    [Fact]
    public async Task FieldConditionTrigger_RunsOnlyForItsValue_AndADocumentNoTemplateCovers_Fails()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));
        await new UpdatePostingTemplateCommandHandler(db, Registry(db)).Handle(new UpdatePostingTemplateCommand(seed.TemplateId,
            PurchaseDefinition(seed.InventoryId, seed.BranchDimensionId) with
            {
                TriggerType = PostingTriggerType.FieldCondition, TriggerFieldName = "PaymentTerms", TriggerFieldValue = "Cash"
            }), CancellationToken.None);

        Assert.Single(await Engine(db, seed.CompanyId).BuildAsync(Invoice(seed, 1000m, 0m, paymentTerms: "Cash")));
        Assert.Equal("POST-NO-TEMPLATE-MATCHED",
            await FailureCode(() => Engine(db, seed.CompanyId).BuildAsync(Invoice(seed, 1000m, 0m, paymentTerms: "Net30"))));
    }

    [Fact]
    public async Task FieldConditionTrigger_OnAValueTheFieldCannotHold_IsRefusedAtSaveTime()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));

        Assert.Equal("POST-TEMPLATE-TRIGGER-VALUE-UNKNOWN", await FailureCode(() =>
            new UpdatePostingTemplateCommandHandler(db, Registry(db)).Handle(new UpdatePostingTemplateCommand(seed.TemplateId,
                PurchaseDefinition(seed.InventoryId, seed.BranchDimensionId) with
                {
                    TriggerType = PostingTriggerType.FieldCondition, TriggerFieldName = "PaymentTerms", TriggerFieldValue = "Net90"
                }), CancellationToken.None)));
    }

    [Fact]
    public async Task StockTrigger_PostsOnlyWhenStockMoved_AndIsRefusedOnAScreenThatNeverMovesStock()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));

        var stockOnly = new PostingTemplateDefinition("تكلفة", "Cost", null,
        [
            new PostingTemplateLineInput(1, PostingDirection.Debit, AccountSourceType.Fixed, seed.VatId, null, null,
                AmountFormulaType.DirectField, "CostAmount", ConditionType.None, null, null, null, []),
            new PostingTemplateLineInput(2, PostingDirection.Credit, AccountSourceType.Fixed, seed.DefaultPayableId, null, null,
                AmountFormulaType.DirectField, "CostAmount", ConditionType.None, null, null, null, [])
        ], PostingTriggerType.HasStockMovement);

        Assert.Equal("POST-TEMPLATE-TRIGGER-NOT-APPLICABLE", await FailureCode(() =>
            CreateHandler(db, seed.CompanyId).Handle(new CreatePostingTemplateCommand(Screen, stockOnly), CancellationToken.None)));

        await CreateHandler(db, seed.CompanyId).Handle(new CreatePostingTemplateCommand(PostingScreenCatalog.Waste, stockOnly), CancellationToken.None);

        TemplatePostingRequest Waste(bool moved, long id) => new()
        {
            CompanyId = seed.CompanyId, ScreenCode = PostingScreenCatalog.Waste, SourceModule = SourceModule.Inventory, SourceDocumentId = id,
            EntryDate = new DateOnly(2026, 9, 18), Description = "هالك", IdempotencyKey = Guid.NewGuid(),
            Context = PostingContext.Create(new Dictionary<string, object?>
            {
                ["CostAmount"] = moved ? 25m : 0m, [PostingScreenCatalog.HasStockMovementField] = moved
            })
        };

        Assert.Empty((await Engine(db, seed.CompanyId).PostAsync(Waste(moved: false, 1))).Entries);
        Assert.Single((await Engine(db, seed.CompanyId).PostAsync(Waste(moved: true, 2))).Entries);
    }

    [Fact]
    public async Task FieldFormulas_AddSubtractAndDivide_AndDivideByZeroStopsThePosting()
    {
        var seed = await SeedAsync(mapVat: true);
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));
        await new UpdatePostingTemplateCommandHandler(db, Registry(db)).Handle(new UpdatePostingTemplateCommand(seed.TemplateId,
            new PostingTemplateDefinition("معادلات", "Formulas", null,
            [
                FixedLine(1, PostingDirection.Debit, seed.VatId, AmountFormulaType.AddFields, fields: ["NetAmount", "TaxAmount"]),
                FixedLine(2, PostingDirection.Credit, seed.DefaultPayableId, AmountFormulaType.SubtractFields, fields: ["TotalAmount", "AdditionalCosts"])
            ])), CancellationToken.None);

        var built = (await Engine(db, seed.CompanyId).BuildAsync(Invoice(seed, 1000m, 140m))).Single();
        Assert.Equal(1140m, built.Request.Lines.Single(l => l.AccountId == seed.VatId).DebitAmount);
        Assert.Equal(1140m, built.Request.Lines.Single(l => l.AccountId == seed.DefaultPayableId).CreditAmount);

        await new UpdatePostingTemplateCommandHandler(db, Registry(db)).Handle(new UpdatePostingTemplateCommand(seed.TemplateId,
            new PostingTemplateDefinition("قسمة", "Divide", null,
            [
                FixedLine(1, PostingDirection.Debit, seed.VatId, AmountFormulaType.DivideFields, fields: ["TotalAmount", "AdditionalCosts"]),
                FixedLine(2, PostingDirection.Credit, seed.DefaultPayableId, AmountFormulaType.DirectField, "TotalAmount")
            ])), CancellationToken.None);

        Assert.Equal("POST-DIVIDE-BY-ZERO", await FailureCode(() => Engine(db, seed.CompanyId).BuildAsync(Invoice(seed, 1000m, 140m))));
    }

    [Fact]
    public async Task CostCenter_FromADocumentEntity_MirrorsTheRecordIntoItsLinkedDimension()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));

        var warehouse = new Habbak.ERP.Domain.Inventory.Warehouse
        {
            CompanyId = seed.CompanyId, Code = "WH-MAIN", NameAr = "المخزن الرئيسي", NameEn = "Main",
            WarehouseType = Habbak.ERP.Domain.Inventory.WarehouseType.Main
        };
        var warehouses = new CostCenterDimension
        {
            CompanyId = seed.CompanyId, Code = "WH", NameAr = "المخازن", NameEn = "Warehouses", LinkedEntityType = CostCenterLinkedEntityType.Warehouse
        };
        db.Warehouses.Add(warehouse);
        db.CostCenterDimensions.Add(warehouses);
        await db.SaveChangesAsync();
        db.AccountDimensionLinks.Add(new AccountDimensionLink { AccountId = seed.VatId, CostCenterDimensionId = warehouses.Id, DisplayOrder = 1 });
        await db.SaveChangesAsync();

        var definition = new PostingTemplateDefinition("بالمخزن", "By warehouse", null,
        [
            FixedLine(1, PostingDirection.Debit, seed.VatId, AmountFormulaType.DirectField, "TotalAmount",
                costCenters: [new PostingTemplateLineCostCenterInput(warehouses.Id, CostCenterSourceType.FromDocument, null, "WarehouseId", null, 1)]),
            FixedLine(2, PostingDirection.Credit, seed.DefaultPayableId, AmountFormulaType.DirectField, "TotalAmount")
        ]);

        // A branch id can never pick a warehouse dimension's value.
        var wrong = definition with
        {
            Lines =
            [
                definition.Lines[0] with
                {
                    CostCenters = [new PostingTemplateLineCostCenterInput(warehouses.Id, CostCenterSourceType.FromDocument, null, "BranchId", null, 1)]
                },
                definition.Lines[1]
            ]
        };
        Assert.Equal("POST-TEMPLATE-DIMENSION-ENTITY-MISMATCH", await FailureCode(() =>
            new UpdatePostingTemplateCommandHandler(db, Registry(db)).Handle(new UpdatePostingTemplateCommand(seed.TemplateId, wrong), CancellationToken.None)));

        await new UpdatePostingTemplateCommandHandler(db, Registry(db)).Handle(new UpdatePostingTemplateCommand(seed.TemplateId, definition), CancellationToken.None);

        var built = (await Engine(db, seed.CompanyId).BuildAsync(Invoice(seed, 500m, 0m, warehouseId: warehouse.Id))).Single();

        var valueId = built.Request.Lines.Single(l => l.AccountId == seed.VatId).DimensionValues[warehouses.Id];
        var value = await db.CostCenterDimensionValues.SingleAsync(v => v.Id == valueId);
        Assert.Equal("WH-MAIN", value.Code);   // created on the spot, coded like the warehouse
    }

    [Fact]
    public async Task Create_DimensionNotLinkedToFixedAccount_IsRejectedAtSaveTime()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));

        // The VAT account has no dimension links, so a cost center on its line could never post.
        var definition = new PostingTemplateDefinition("خطأ", "Wrong", null,
        [
            new PostingTemplateLineInput(1, PostingDirection.Debit, AccountSourceType.Fixed, seed.VatId, null, null,
                AmountFormulaType.DirectField, "TotalAmount", ConditionType.None, null, null, null,
                [new PostingTemplateLineCostCenterInput(seed.BranchDimensionId, CostCenterSourceType.Fixed, seed.BranchValueId, null, null, 1)]),
            new PostingTemplateLineInput(2, PostingDirection.Credit, AccountSourceType.Fixed, seed.DefaultPayableId, null, null,
                AmountFormulaType.DirectField, "TotalAmount", ConditionType.None, null, null, null, [])
        ]);

        Assert.Equal("POST-TEMPLATE-DIMENSION-NOT-LINKED", await FailureCode(() =>
            CreateHandler(db, seed.CompanyId).Handle(new CreatePostingTemplateCommand(PostingScreenCatalog.PurchaseReturn, definition), CancellationToken.None)));
    }

    [Fact]
    public async Task Create_BranchResolverOnNonBranchDimension_IsRejectedAtSaveTime()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext(new TestCurrentCompanyContext(seed.CompanyId));
        db.AccountDimensionLinks.Add(new AccountDimensionLink
        {
            AccountId = seed.VatId, CostCenterDimensionId = seed.PlainDimensionId, DisplayOrder = 1
        });
        await db.SaveChangesAsync();

        var definition = new PostingTemplateDefinition("خطأ", "Wrong", null,
        [
            new PostingTemplateLineInput(1, PostingDirection.Debit, AccountSourceType.Fixed, seed.VatId, null, null,
                AmountFormulaType.DirectField, "TotalAmount", ConditionType.None, null, null, null,
                [new PostingTemplateLineCostCenterInput(seed.PlainDimensionId, CostCenterSourceType.Dynamic, null, null, "Branch.ToCostCenterValue", 1)]),
            new PostingTemplateLineInput(2, PostingDirection.Credit, AccountSourceType.Fixed, seed.DefaultPayableId, null, null,
                AmountFormulaType.DirectField, "TotalAmount", ConditionType.None, null, null, null, [])
        ]);

        Assert.Equal("POST-TEMPLATE-RESOLVER-DIMENSION-MISMATCH", await FailureCode(() =>
            CreateHandler(db, seed.CompanyId).Handle(new CreatePostingTemplateCommand(PostingScreenCatalog.PurchaseReturn, definition), CancellationToken.None)));
    }
}
