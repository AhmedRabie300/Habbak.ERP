using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Inventory.Common;
using Habbak.ERP.Application.Common.Models;
using Habbak.ERP.Application.Inventory.Settings;
using Habbak.ERP.Application.POS.Checks;
using Habbak.ERP.Application.POS.Posting;
using Habbak.ERP.Domain.Inventory;
using Habbak.ERP.Domain.POS;
using Habbak.ERP.Domain.Sales;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Invoices.Commands.CompleteCheckPayment;

public sealed record PaymentInput(long PaymentMethodId, decimal Amount, string? CardTransactionReference, decimal? AmountTendered);

/// <summary>قاعدة 14: إتمام الدفع الكامل على شيك يحوّل Check.Status = Completed وينشئ POSInvoice
/// Posted مباشرة (قاعدة 15)، ويرحّل خصم المخزون داخل نفس الـTransaction. القيد المحاسبي: لو وضع
/// ترحيل الفرع PerTransaction بيترحّل هنا مع الفاتورة (IPOSPostingService)، وفي الوضعين التانيين
/// بيترحّل عند إقفال الوردية أو بزرار إقفال اليوم.
///
/// اللي بيتخصم بيتحدد حسب نموذج الإنتاج/البيع (قاعدة 17، أولوية صنف &gt; نقطة بيع &gt; فرع &gt; شركة):
/// النموذج اللحظي بيستهلك مكوّنات وصفة الصنف (ProductionIssue) لأن المشروب بيتحضّر عند البيع،
/// والنموذج المخزني بيخصم الصنف التام نفسه (POSSale). وقاعدة 18: اللحظي بيمنع البيع أصلًا لو أي
/// مكوّن ناقص — الفحص بيتم قبل أي خصم عشان مايحصلش استهلاك جزئي. قاعدة 12: إجمالي
/// الدفعات لازم يساوي Total بالضبط. TipAmount/ServiceChargeAmount إضافات على الحقول الأصلية
/// (قرارات جلسة الاستشارة 3/4) — بتتحسب هنا مرة واحدة وقت إتمام الدفع، مش قابلة للتعديل بعدها.
///
/// يحفظ على مرحلتين جوه Transaction صريحة واحدة — من غيرها، أي فشل بعد الحفظ الأول (نقص مكوّن،
/// فشل القيد) كان بيسيب فاتورة محفوظة من غير دفعات ولا خصم مخزون. الأولى تنشئ POSInvoice+POSInvoiceLines عشان ناخد الـId الحقيقي (مش 0) قبل
/// ما نمرّره كـSourceDocumentId لحركات المخزون — نفس ضرورة الحفظ على مرحلتين المُتَّبعة مع تعديل
/// سطور أي مستند في الموديول. قاعدة 34: IdempotencyKey اختياري — لو موجود ومتكرر (Retry أو
/// مزامنة معاملة Offline اتبعتت مرتين)، IdempotencyBehavior بيرجّع نفس رقم الفاتورة الأصلي بدل
/// ما يرحّل فاتورة تانية ويخصم مخزون تاني بالغلط.</summary>
public sealed record CompleteCheckPaymentCommand(long CheckId, decimal TipAmount, IReadOnlyList<PaymentInput> Payments, Guid? IdempotencyKey = null)
    : IRequest<long>, IIdempotentRequest;

public sealed class CompleteCheckPaymentCommandValidator : AbstractValidator<CompleteCheckPaymentCommand>
{
    public CompleteCheckPaymentCommandValidator()
    {
        RuleFor(x => x.CheckId).GreaterThan(0);
        RuleFor(x => x.TipAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Payments).NotEmpty().WithMessage("لا يمكن إتمام الدفع بدون دفعة واحدة على الأقل.");

        RuleForEach(x => x.Payments).ChildRules(payment =>
        {
            payment.RuleFor(p => p.PaymentMethodId).GreaterThan(0);
            payment.RuleFor(p => p.Amount).GreaterThan(0);
            payment.RuleFor(p => p.AmountTendered).GreaterThanOrEqualTo(0).When(p => p.AmountTendered.HasValue);
        });
    }
}

public sealed class CompleteCheckPaymentCommandHandler(
    IApplicationDbContext db,
    ICodeGenerator codeGenerator,
    IStockMovementService stockMovementService,
    IPOSPostingService posPosting)
    : IRequestHandler<CompleteCheckPaymentCommand, long>
{
    public async Task<long> Handle(CompleteCheckPaymentCommand request, CancellationToken cancellationToken)
    {
        var check = await db.Checks
            .Include(c => c.Lines)
            .Include(c => c.Table)
            .FirstOrDefaultAsync(c => c.Id == request.CheckId, cancellationToken)
            ?? throw new NotFoundException(nameof(Check), request.CheckId);

        if (check.Status != CheckStatus.Open && check.Status != CheckStatus.Held)
        {
            throw new BusinessRuleException("POS-CHECK-NOT-ACTIVE", "لا يمكن الدفع على شيك منتهٍ.");
        }

        if (check.Lines.Count == 0)
        {
            throw new BusinessRuleException("POS-CHECK-NO-LINES", "لا يمكن إتمام الدفع على شيك بدون بنود.");
        }

        var terminal = await db.POSTerminals.FirstOrDefaultAsync(t => t.Id == check.POSTerminalId, cancellationToken)
            ?? throw new NotFoundException(nameof(POSTerminal), check.POSTerminalId);

        if (terminal.DefaultWarehouseId is null)
        {
            throw new BusinessRuleException("POS-TERMINAL-NO-WAREHOUSE", "هذا الجهاز مش محدَّد له مخزن افتراضي — لا يمكن ترحيل الفاتورة.");
        }

        var settings = await db.BranchPOSSettingsRows.FirstOrDefaultAsync(s => s.BranchId == check.BranchId, cancellationToken)
            ?? new BranchPOSSettings { BranchId = check.BranchId };

        if (request.TipAmount > 0 && !settings.TipsEnabled)
        {
            throw new BusinessRuleException("POS-TIPS-DISABLED", "البقشيش غير مفعَّل على هذا الفرع.");
        }

        var groupedByMethod = request.Payments.GroupBy(p => p.PaymentMethodId);
        if (groupedByMethod.Any(g => g.Count() > 1) && !settings.AllowSplitPayment)
        {
            throw new BusinessRuleException("POS-SPLIT-PAYMENT-DISABLED", "تقسيم الدفع غير مفعَّل على هذا الفرع.");
        }

        var subtotal = check.Lines.Sum(l => l.Quantity * l.UnitPrice);
        var discountAmount = check.Lines.Sum(l => l.DiscountAmount);
        var netAfterLineDiscount = subtotal - discountAmount;
        // الخصم اليدوي (مراجعة 2026-09-13) بيتطرح قبل الخدمة والضريبة، زي خصم البنود بالظبط —
        // نفس الحساب المُستخدَم في معاينة السلة (GetCheckByIdQuery) عشان الرقمين ميتفرقوش.
        var manualDiscountAmount = ManualDiscountCalculator.Calculate(check.ManualDiscountType, check.ManualDiscountValue, netAfterLineDiscount);
        var remainingAfterManualDiscount = netAfterLineDiscount - manualDiscountAmount;

        // استبدال نقاط الولاء (مراجعة 2026-09-13، قاعدة 12) — يُعاد التحقق من الرصيد هنا حتى لو
        // اتحقق وقت SetLoyaltyPointsRedemptionCommand، لاحتمال تغيّر الرصيد بين الوقتين (بيع/استبدال
        // تاني على نفس العميل في نفس اللحظة). الخصم الفعلي من Customer.LoyaltyPointsBalance بيحصل
        // هنا فقط، مش قبل كده — الشيك ممكن يتلغي أو يتعدَّل بعد تسجيل نية الاستبدال بدون أي أثر فعلي.
        Customer? redeemingCustomer = null;
        var loyaltyDiscountAmount = 0m;
        if (check.LoyaltyPointsToRedeem is { } pointsToRedeem && pointsToRedeem > 0)
        {
            if (check.CustomerId is not { } loyaltyCustomerId)
            {
                throw new BusinessRuleException("POS-CHECK-NO-CUSTOMER", "لا يمكن استبدال نقاط ولاء بدون ربط عميل بالشيك.");
            }

            redeemingCustomer = await db.Customers.FirstOrDefaultAsync(c => c.Id == loyaltyCustomerId, cancellationToken)
                ?? throw new NotFoundException(nameof(Customer), loyaltyCustomerId);

            if (pointsToRedeem > redeemingCustomer.LoyaltyPointsBalance)
            {
                throw new BusinessRuleException("POS-LOYALTY-INSUFFICIENT-BALANCE", $"رصيد العميل الحالي {redeemingCustomer.LoyaltyPointsBalance:0.##} نقطة فقط.");
            }

            var loyaltyProgramSettings = await db.LoyaltyProgramSettingsRows.FirstOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleException("POS-LOYALTY-NOT-CONFIGURED", "إعدادات برنامج الولاء غير مُهيَّأة بعد.");

            loyaltyDiscountAmount = LoyaltyRedemptionCalculator.Calculate(pointsToRedeem, loyaltyProgramSettings.PointsRedemptionValue, remainingAfterManualDiscount);
        }

        var netAfterDiscount = remainingAfterManualDiscount - loyaltyDiscountAmount;
        var (serviceChargeAmount, taxAmount, total) =
            CheckTotalsCalculator.Calculate(netAfterDiscount, settings, request.TipAmount);

        var paymentsTotal = Math.Round(request.Payments.Sum(p => p.Amount), 2);
        if (paymentsTotal != total)
        {
            throw new BusinessRuleException("POS-PAYMENT-AMOUNT-MISMATCH", $"إجمالي الدفعات ({paymentsTotal:0.00}) لازم يساوي إجمالي الفاتورة ({total:0.00}) بالضبط.");
        }

        // G-9: an unknown payment method used to sail past here and die on the foreign key, which
        // surfaced to the cashier as a bare 500. Checked before anything is created.
        var methodIds = request.Payments.Select(p => p.PaymentMethodId).Distinct().ToList();
        var knownMethodIds = await db.PaymentMethods
            .Where(m => methodIds.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (methodIds.Except(knownMethodIds).Any())
        {
            throw new BusinessRuleException(
                "POS-PAYMENT-METHOD-NOT-FOUND",
                "طريقة دفع غير موجودة أو غير متاحة على هذه الشركة.");
        }

        // بيتحسبوا قبل إنشاء أي كيان عمدًا: أي فشل في التحقق من الدفعات (مبلغ نقدي غير كافٍ) لازم
        // يحصل قبل حفظ POSInvoice — بعد الحفظ الأول (اللي بناخد بيه الـId الحقيقي لحركات المخزون)
        // مفيش رجوع سهل من غير Transaction صريحة، والكود ده مفيهوش وحدة تحكم بالـTransaction بعد.
        var preparedPayments = new List<(PaymentInput Input, decimal? ChangeGiven, decimal CashRoundingAdjustment)>();
        foreach (var payment in request.Payments)
        {
            decimal? changeGiven = null;
            var cashRoundingAdjustment = 0m;

            if (payment.AmountTendered is { } tendered)
            {
                var roundedAmount = settings.CashRoundingIncrement > 0
                    ? Math.Round(payment.Amount / settings.CashRoundingIncrement, MidpointRounding.AwayFromZero) * settings.CashRoundingIncrement
                    : payment.Amount;

                cashRoundingAdjustment = roundedAmount - payment.Amount;

                if (tendered < roundedAmount)
                {
                    throw new BusinessRuleException("POS-PAYMENT-TENDERED-INSUFFICIENT", "المبلغ المدفوع نقدًا أقل من المطلوب.");
                }

                changeGiven = tendered - roundedAmount;
            }

            preparedPayments.Add((payment, changeGiven, cashRoundingAdjustment));
        }

        var invoiceNumber = await codeGenerator.ResolveCodeAsync("POS_INVOICES", null, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var invoice = new POSInvoice
        {
            CompanyId = check.CompanyId,
            BranchId = check.BranchId,
            POSTerminalId = check.POSTerminalId,
            ShiftId = check.ShiftId,
            CheckId = check.Id,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = today,
            CustomerId = check.CustomerId,
            OrderType = check.OrderType,
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            ManualDiscountAmount = manualDiscountAmount,
            LoyaltyPointsRedeemed = check.LoyaltyPointsToRedeem is > 0 ? check.LoyaltyPointsToRedeem.Value : 0m,
            LoyaltyDiscountAmount = loyaltyDiscountAmount,
            ServiceChargeAmount = serviceChargeAmount,
            TipAmount = request.TipAmount,
            TaxAmount = taxAmount,
            Total = total,
            Status = POSInvoiceStatus.Posted,
            ETAReceiptStatus = settings.ETAReceiptEnabled ? ETAReceiptStatus.Pending : ETAReceiptStatus.NotApplicable
        };

        var lineNumber = 1;
        foreach (var line in check.Lines)
        {
            invoice.Lines.Add(new POSInvoiceLine
            {
                LineNumber = lineNumber++,
                ItemId = line.ItemId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountAmount = line.DiscountAmount
            });
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        db.POSInvoices.Add(invoice);
        await db.SaveChangesAsync(cancellationToken);

        if (redeemingCustomer is not null && loyaltyDiscountAmount > 0)
        {
            var pointsRedeemed = check.LoyaltyPointsToRedeem!.Value;
            redeemingCustomer.LoyaltyPointsBalance -= pointsRedeemed;

            db.LoyaltyTransactions.Add(new LoyaltyTransaction
            {
                CompanyId = check.CompanyId,
                CustomerId = redeemingCustomer.Id,
                TransactionType = LoyaltyTransactionType.Redeem,
                Points = -pointsRedeemed,
                SourceDocumentType = "POSInvoice",
                SourceDocumentId = invoice.Id,
                TransactionDate = today
            });
        }

        var consumption = await BuildStockConsumptionAsync(check, terminal, cancellationToken);
        await GuardAgainstShortagesAsync(consumption, terminal.DefaultWarehouseId.Value, cancellationToken);

        // Cost per check line, accumulated from what the movements actually drew down — for a
        // real-time item that is the sum of its components, which is the only figure that makes
        // COGS mean anything for a drink that is never stocked as a finished good.
        var costByCheckLineId = new Dictionary<long, decimal>();

        foreach (var movement in consumption)
        {
            var applied = await stockMovementService.ApplyMovementAsync(new StockMovementRequest
            {
                CompanyId = check.CompanyId!.Value,
                WarehouseId = terminal.DefaultWarehouseId.Value,
                ItemId = movement.ItemId,
                TransactionType = movement.TransactionType,
                Quantity = movement.Quantity,
                // Outbound: StockMovementService costs this from the warehouse's average and
                // ignores whatever is passed here (rule 41).
                UnitCost = 0m,
                TransactionDate = today,
                SourceDocumentType = "POSSale",
                SourceDocumentId = invoice.Id
            }, cancellationToken);

            costByCheckLineId[movement.CheckLineId] =
                costByCheckLineId.GetValueOrDefault(movement.CheckLineId) + (applied.Quantity * applied.UnitCost);
        }

        // Rule 42: freeze it on the invoice line. The lines were written in check-line order above.
        foreach (var (checkLine, invoiceLine) in check.Lines.Zip(invoice.Lines))
        {
            var lineCost = costByCheckLineId.GetValueOrDefault(checkLine.Id);
            invoiceLine.UnitCost = checkLine.Quantity > 0 ? lineCost / checkLine.Quantity : 0m;
        }

        foreach (var (input, changeGiven, cashRoundingAdjustment) in preparedPayments)
        {
            invoice.Payments.Add(new POSPayment
            {
                POSInvoiceId = invoice.Id,
                PaymentMethodId = input.PaymentMethodId,
                Amount = input.Amount,
                CardTransactionReference = input.CardTransactionReference,
                AmountTendered = input.AmountTendered,
                ChangeGiven = changeGiven,
                CashRoundingAdjustment = cashRoundingAdjustment,
                Status = POSPaymentStatus.Completed
            });
        }

        check.Status = CheckStatus.Completed;
        if (check.Table is { Status: TableStatus.Busy } table)
        {
            table.Status = TableStatus.Cleaning;
        }

        if (settings.PostingMode == POSPostingMode.PerTransaction)
        {
            await posPosting.PostInvoicesAsync([invoice], POSPostingScope.Transaction, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return invoice.Id;
    }

    /// <summary>One stock movement a sale requires, tagged with the check line that caused it so
    /// its actual cost can be attributed back to that line for COGS (rule 42).</summary>
    private sealed record PlannedMovement(long CheckLineId, long ItemId, decimal Quantity, TransactionType TransactionType);

    /// <summary>
    /// Works out what a sale actually takes out of the warehouse, per rules 17 and 18
    /// (03-Inventory-Module.md).
    ///
    /// Real-time lines consume the sold item's recipe components instead of the item itself: a
    /// latte is assembled at the till, so what leaves stock is coffee and milk, never "a latte".
    /// Stocked lines keep the original behaviour of deducting the finished item.
    ///
    /// An item with no approved recipe falls back to deducting itself even under the real-time
    /// model — a bottle of water sold at the same till has nothing to assemble.
    /// </summary>
    private async Task<IReadOnlyList<PlannedMovement>> BuildStockConsumptionAsync(
        Check check, POSTerminal terminal, CancellationToken cancellationToken)
    {
        var itemIds = check.Lines.Select(l => l.ItemId).Distinct().ToList();

        var items = await db.Items
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var modeSettings = await db.ProductionSalesModeSettings.AsNoTracking().ToListAsync(cancellationToken);

        var recipes = await db.Recipes
            .AsNoTracking()
            .Include(r => r.Lines)
            .Where(r => itemIds.Contains(r.OutputItemId)
                        && r.Status == RecipeStatus.Approved
                        && r.IsCurrentVersion)
            .ToDictionaryAsync(r => r.OutputItemId, cancellationToken);

        var planned = new List<PlannedMovement>();

        foreach (var line in check.Lines)
        {
            if (!items.TryGetValue(line.ItemId, out var item))
            {
                throw new NotFoundException(nameof(Item), line.ItemId);
            }

            var mode = ProductionSalesModeResolver.Resolve(
                modeSettings, item.Id, terminal.Id, check.BranchId);

            if (mode == ProductionSalesMode.RealTime && recipes.TryGetValue(item.Id, out var recipe))
            {
                // Rule 15's scaling, borrowed from CompleteProductionOrderCommand so a drink costs
                // the same whether it is produced by a production order or straight at the till.
                var scalingFactor = recipe.OutputQuantity > 0 ? line.Quantity / recipe.OutputQuantity : 0m;

                foreach (var recipeLine in recipe.Lines)
                {
                    planned.Add(new PlannedMovement(
                        line.Id,
                        recipeLine.ComponentItemId,
                        ItemUnits.ToBase(recipeLine.Quantity, recipeLine.UnitFactor) * scalingFactor,
                        TransactionType.ProductionIssue));
                }

                continue;
            }

            if (!item.IsStocked)
            {
                continue;
            }

            planned.Add(new PlannedMovement(line.Id, item.Id, line.Quantity, TransactionType.POSSale));
        }

        return planned;
    }

    /// <summary>
    /// Rule 18: the real-time model refuses the sale when any component is short. StockMovementService
    /// would block it anyway on the first component that goes negative, but only one at a time and
    /// without naming it — a cashier needs to know which ingredient ran out, and needs to know it
    /// before half the recipe has already been deducted.
    ///
    /// Quantities are summed per item first: two lattes and a cappuccino on one check draw on the
    /// same milk, and only the total tells you whether it is actually there.
    /// </summary>
    private async Task GuardAgainstShortagesAsync(
        IReadOnlyList<PlannedMovement> planned, long warehouseId, CancellationToken cancellationToken)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(Warehouse), warehouseId);

        if (warehouse.AllowNegativeBalance)
        {
            return;
        }

        var required = planned
            .GroupBy(p => p.ItemId)
            .Select(g => new { ItemId = g.Key, Quantity = g.Sum(p => p.Quantity) })
            .ToList();

        var requiredIds = required.Select(r => r.ItemId).ToList();

        var balances = await db.StockBalances
            .AsNoTracking()
            .Where(b => b.WarehouseId == warehouseId && requiredIds.Contains(b.ItemId))
            .ToDictionaryAsync(b => b.ItemId, b => b.QuantityOnHand, cancellationToken);

        var shortages = required
            .Where(r => balances.GetValueOrDefault(r.ItemId) < r.Quantity)
            .ToList();

        if (shortages.Count == 0)
        {
            return;
        }

        var shortageIds = shortages.Select(s => s.ItemId).ToList();
        var names = await db.Items
            .AsNoTracking()
            .Where(i => shortageIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.NameAr, cancellationToken);

        var detail = string.Join("، ", shortages.Select(s =>
            $"{names.GetValueOrDefault(s.ItemId, s.ItemId.ToString())} (متاح {balances.GetValueOrDefault(s.ItemId):0.##} / مطلوب {s.Quantity:0.##})"));

        throw new BusinessRuleException(
            "POS-COMPONENT-SHORTAGE",
            $"لا يمكن إتمام البيع — مكوّنات ناقصة: {detail}");
    }
}
