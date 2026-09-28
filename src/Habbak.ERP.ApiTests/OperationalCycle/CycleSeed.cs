using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.Organization;
using Habbak.ERP.Domain.POS;
using Habbak.ERP.Domain.Purchasing;
using Habbak.ERP.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.ApiTests.OperationalCycle;

/// <summary>
/// Master data the end-to-end operational cycle needs (Docs/End-to-End-Cycle-Test.md). Seeded
/// straight through a direct DbContext rather than over HTTP: creating a supplier/warehouse/item
/// is arrange-work for these tests, not the behaviour under test, and driving it through the API
/// would make every flow test fail for reasons unrelated to the flow it covers.
/// </summary>
public sealed record CycleIds(
    long CompanyId,
    long BranchId,
    long MainWarehouseId,
    long BranchWarehouseId,
    long SupplierId,
    long CustodyOfficerId,
    long GramUnitId,
    long MilliUnitId,
    long EspressoItemId,
    long MilkItemId,
    long LatteItemId,
    long RecipeId,
    long PosTerminalId,
    long PaymentMethodId);

public static class CycleSeed
{
    public const decimal EspressoPerLatteGrams = 9m;
    public const decimal MilkPerLatteMillilitres = 150m;

    public static async Task<CycleIds> SeedAsync(AccountingApiFactory factory, long companyId)
    {
        await using var db = factory.CreateDirectDbContext(companyId);

        // Tests share one LocalDB, and not every code index is company-scoped (Recipes are keyed
        // on RecipeFamilyCode + VersionNumber alone), so every seeded code carries the company id.
        var suffix = companyId % 100000000;

        var branch = new Branch { CompanyId = companyId, Code = $"E2E-BR-{suffix}", NameAr = "فرع الاختبار", NameEn = "Test Branch", IsActive = true };
        db.Branches.Add(branch);

        var gram = new UnitOfMeasure { CompanyId = companyId, Code = $"E2E-GM-{suffix}", NameAr = "جرام", NameEn = "Gram", Category = UnitCategory.Weight, IsActive = true };
        var milli = new UnitOfMeasure { CompanyId = companyId, Code = $"E2E-ML-{suffix}", NameAr = "مللي", NameEn = "Millilitre", Category = UnitCategory.Volume, IsActive = true };
        db.UnitsOfMeasure.AddRange(gram, milli);

        var supplier = new Supplier
        {
            CompanyId = companyId, Code = $"E2E-SUP-{suffix}", NameAr = "شركة البن الذهبي", NameEn = "Golden Coffee",
            PaymentTerms = SupplierPaymentTerms.Net30, CurrencyCode = "EGP", IsActive = true
        };
        db.Suppliers.Add(supplier);

        if (!await db.Currencies.AnyAsync(c => c.Code == "EGP"))
        {
            db.Currencies.Add(new Currency { Code = "EGP", NameAr = "جنيه مصري", NameEn = "Egyptian Pound", IsActive = true });
        }

        await db.SaveChangesAsync();

        var mainWarehouse = new Warehouse
        {
            CompanyId = companyId, Code = $"E2E-WHM-{suffix}", NameAr = "المخزن الرئيسي", NameEn = "Main",
            WarehouseType = WarehouseType.Main, BranchId = null, AllowNegativeBalance = false, IsActive = true
        };
        var branchWarehouse = new Warehouse
        {
            CompanyId = companyId, Code = $"E2E-WHB-{suffix}", NameAr = "مخزن الفرع", NameEn = "Branch Materials",
            WarehouseType = WarehouseType.BranchMaterials, BranchId = branch.Id, AllowNegativeBalance = false, IsActive = true
        };
        db.Warehouses.AddRange(mainWarehouse, branchWarehouse);

        var officer = new CustodyOfficer
        {
            CompanyId = companyId, Code = $"E2E-CO-{suffix}", NameAr = "مسؤول العهدة", NameEn = "Custody Officer",
            BranchId = branch.Id, IsActive = true
        };
        db.CustodyOfficers.Add(officer);

        // Raw materials are stocked; the latte deliberately is not, mirroring how finished goods
        // are configured in the real menu (made to order, never held as stock).
        var espresso = new Item
        {
            CompanyId = companyId, Code = $"E2E-ESP-{suffix}", NameAr = "بن اسبرسو مطحون", NameEn = "Ground Espresso",
            ItemType = ItemType.RawMaterial, BaseUnitOfMeasureId = gram.Id, IsStocked = true,
            IsPurchasable = true, IsSellable = false, Status = ItemStatus.Active, StandardCost = 0.50m
        };
        var milk = new Item
        {
            CompanyId = companyId, Code = $"E2E-MILK-{suffix}", NameAr = "لبن", NameEn = "Milk",
            ItemType = ItemType.RawMaterial, BaseUnitOfMeasureId = milli.Id, IsStocked = true,
            IsPurchasable = true, IsSellable = false, Status = ItemStatus.Active, StandardCost = 0.02m
        };
        var latte = new Item
        {
            CompanyId = companyId, Code = $"E2E-LAT-{suffix}", NameAr = "لاتيه", NameEn = "Latte",
            ItemType = ItemType.FinishedGood, BaseUnitOfMeasureId = gram.Id, IsStocked = false,
            IsPurchasable = false, IsSellable = true, IsManufacturable = true,
            Status = ItemStatus.Active, DefaultPrice = 40m
        };
        db.Items.AddRange(espresso, milk, latte);

        var paymentMethod = new PaymentMethod
        {
            CompanyId = companyId, Code = $"E2E-CASH-{suffix}", NameAr = "نقدي", NameEn = "Cash", IsActive = true
        };
        db.PaymentMethods.Add(paymentMethod);

        await db.SaveChangesAsync();

        var recipe = new Recipe
        {
            CompanyId = companyId, RecipeFamilyCode = $"E2E-REC-{suffix}", VersionNumber = 1, OutputItemId = latte.Id,
            OutputQuantity = 1m, WastePercentage = 0m, Status = RecipeStatus.Approved, IsCurrentVersion = true,
            EffectiveFromDate = DateOnly.FromDateTime(DateTime.UtcNow.Date)
        };
        recipe.Lines.Add(new RecipeLine { ComponentItemId = espresso.Id, Quantity = EspressoPerLatteGrams, UnitId = espresso.BaseUnitOfMeasureId });
        recipe.Lines.Add(new RecipeLine { ComponentItemId = milk.Id, Quantity = MilkPerLatteMillilitres, UnitId = milk.BaseUnitOfMeasureId });
        db.Recipes.Add(recipe);

        var terminal = new POSTerminal
        {
            CompanyId = companyId, Code = $"E2E-POS-{suffix}", NameAr = "كاشير الاختبار", NameEn = "Test POS",
            BranchId = branch.Id, DefaultWarehouseId = branchWarehouse.Id, IsActive = true
        };
        db.POSTerminals.Add(terminal);

        // The test cashier (user 1, the seeded admin) works in this company and is on this terminal
        // today — POS rule 31: only an assigned cashier opens a shift.
        db.UserScopes.Add(new Habbak.ERP.Domain.Settings.UserScope
        {
            UserId = 1, CompanyId = companyId, RoleInScope = "SUPER_ADMIN", IsDefault = true, IsActive = true, GrantedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        db.ShiftAssignments.Add(new ShiftAssignment
        {
            CompanyId = companyId, BranchId = branch.Id, POSTerminalId = terminal.Id, UserId = 1, AssignedDate = DateOnly.FromDateTime(DateTime.Now)
        });
        await db.SaveChangesAsync();

        return new CycleIds(
            companyId, branch.Id, mainWarehouse.Id, branchWarehouse.Id, supplier.Id, officer.Id,
            gram.Id, milli.Id, espresso.Id, milk.Id, latte.Id, recipe.Id, terminal.Id, paymentMethod.Id);
    }

    /// <summary>
    /// A Confirmed purchase order the goods-receipt flow can be driven from. Seeded directly
    /// because the PO approval chain is not what these tests are about.
    /// </summary>
    public static async Task<long> SeedConfirmedPurchaseOrderAsync(
        AccountingApiFactory factory, CycleIds ids, decimal espressoQty, decimal espressoPrice)
    {
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);

        var order = new PurchaseOrder
        {
            CompanyId = ids.CompanyId,
            BranchId = ids.BranchId,
            OrderNumber = $"E2E-PO-{Random.Shared.Next(100000, 999999)}",
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            SupplierId = ids.SupplierId,
            CurrencyCode = "EGP",
            ExchangeRate = 1m,
            PaymentTerms = SupplierPaymentTerms.Net30,
            Status = PurchaseOrderStatus.Confirmed,
            Subtotal = espressoQty * espressoPrice,
            TaxAmount = 0m,
            TotalAmount = espressoQty * espressoPrice
        };
        order.Lines.Add(new PurchaseOrderLine
        {
            LineNumber = 1,
            ItemId = ids.EspressoItemId,
            Quantity = espressoQty,
            ReceivedQuantity = 0m,
            UnitPrice = espressoPrice,
            TotalPrice = espressoQty * espressoPrice,
            UnitId = ids.GramUnitId,
            BaseQuantity = espressoQty,
            BaseUnitCost = espressoPrice
        });

        db.PurchaseOrders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    /// <summary>A purchase invoice with no order behind it — the "invoice only" cycle's starting point.</summary>
    public static async Task<long> SeedPostedInvoiceAsync(
        AccountingApiFactory factory, CycleIds ids, decimal espressoQty, decimal espressoPrice,
        PurchaseInvoiceStatus status = PurchaseInvoiceStatus.Posted, long? warehouseId = null)
    {
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);

        var invoice = new PurchaseInvoice
        {
            CompanyId = ids.CompanyId,
            BranchId = ids.BranchId,
            InvoiceNumber = $"E2E-PINV-{Random.Shared.Next(100000, 999999)}",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.Date).AddDays(30),
            SupplierId = ids.SupplierId,
            PurchaseOrderId = null,
            WarehouseId = warehouseId,
            CurrencyCode = "EGP",
            ExchangeRate = 1m,
            PaymentTerms = SupplierPaymentTerms.Net30,
            Status = status,
            Subtotal = espressoQty * espressoPrice,
            TaxAmount = 0m,
            TotalAmount = espressoQty * espressoPrice,
            AdditionalCosts = 0m
        };
        invoice.Lines.Add(new PurchaseInvoiceLine
        {
            LineNumber = 1,
            ItemId = ids.EspressoItemId,
            Quantity = espressoQty,
            ReceivedQuantity = 0m,
            UnitPrice = espressoPrice,
            TotalPrice = espressoQty * espressoPrice,
            UnitId = ids.GramUnitId,
            BaseQuantity = espressoQty,
            BaseUnitCost = espressoPrice
        });

        db.PurchaseInvoices.Add(invoice);
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    /// <summary>Upserts the purchasing cycle settings row (the endpoint is a full replace).</summary>
    public static async Task SetPurchaseCycleAsync(
        AccountingApiFactory factory, long companyId, bool autoCreateInvoiceOnReceipt)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        var row = await db.PurchaseCycleSettingsRows.FirstOrDefaultAsync(s => s.CompanyId == companyId);
        if (row is null)
        {
            row = new PurchaseCycleSettings { CompanyId = companyId };
            db.PurchaseCycleSettingsRows.Add(row);
        }
        row.AutoCreateInvoiceOnReceipt = autoCreateInvoiceOnReceipt;
        await db.SaveChangesAsync();
    }

    /// <summary>Turns on service charge and VAT for the branch, so payable differs from the line total.</summary>
    public static async Task SetBranchPOSChargesAsync(
        AccountingApiFactory factory, CycleIds ids, decimal serviceChargeRate, decimal vatRate)
    {
        await using var db = factory.CreateDirectDbContext(ids.CompanyId);
        db.BranchPOSSettingsRows.Add(new BranchPOSSettings
        {
            CompanyId = ids.CompanyId,
            BranchId = ids.BranchId,
            ServiceChargeEnabled = serviceChargeRate > 0,
            ServiceChargeRate = serviceChargeRate,
            VatEnabled = vatRate > 0,
            VatRate = vatRate
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Configures the production/sales model for one scope (rule 17).</summary>
    public static async Task SetProductionSalesModeAsync(
        AccountingApiFactory factory, long companyId, SettingScopeType scopeType, long? scopeId, ProductionSalesMode mode)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        db.ProductionSalesModeSettings.Add(new ProductionSalesModeSetting
        {
            CompanyId = companyId, ScopeType = scopeType, ScopeId = scopeId, Mode = mode
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Puts raw material into a warehouse without going through any document flow. Seeds a real
    /// AverageCost too: stock that exists at zero cost cannot legally be moved on (rule 41), so a
    /// costless balance would fail later for reasons unrelated to whatever the test is about.
    /// </summary>
    public static async Task GiveStockAsync(
        AccountingApiFactory factory, long companyId, long warehouseId, long itemId, decimal quantity, decimal averageCost = 1m)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        db.StockBalances.Add(new StockBalance
        {
            CompanyId = companyId,
            WarehouseId = warehouseId,
            ItemId = itemId,
            QuantityOnHand = quantity,
            AverageCost = averageCost,
            LastCostUpdateAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public static async Task<decimal> GetBalanceAsync(AccountingApiFactory factory, long companyId, long warehouseId, long itemId)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        var row = await db.StockBalances.FirstOrDefaultAsync(b => b.WarehouseId == warehouseId && b.ItemId == itemId);
        return row?.QuantityOnHand ?? 0m;
    }

    public static async Task<int> CountStockTransactionsAsync(AccountingApiFactory factory, long companyId, string sourceDocumentType)
    {
        await using var db = factory.CreateDirectDbContext(companyId);
        return await db.StockTransactions.CountAsync(t => t.SourceDocumentType == sourceDocumentType);
    }
}
