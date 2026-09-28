using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Posting;
using Habbak.ERP.Application.Posting.Screens;
using Habbak.ERP.Domain.Accounting;
using Habbak.ERP.Domain.POS;
using Habbak.ERP.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.POS.Posting;

/// <summary>
/// Everything POS sends to the ledger, in one place so every trigger builds its entry the same way
/// (Docs/Posting-Engine-Implementation-Plan.md, section 9).
///
/// Sales are posted per the branch's PostingMode: with each invoice, at shift close, or when someone
/// presses "إقفال اليوم". Whatever the trigger, it only ever takes invoices that have no entry yet
/// (POSInvoice.JournalEntryId), which is what makes switching mode at any time safe: nothing is
/// posted twice and nothing falls through. One entry never mixes two branches.
///
/// Takings post to each terminal's own treasury for the payment method used
/// (POSPaymentMethodConfig.LinkedTreasuryAccountId) — that is how the ledger tells branches' cash
/// apart. Returns, drawer expenses and shift differences are separate entries in every mode.
///
/// None of this saves: callers commit the entry with their own changes.
/// </summary>
public interface IPOSPostingService
{
    /// <summary>Posts the given invoices as one entry (they must share a branch). Skips those already posted.</summary>
    Task<JournalEntry?> PostInvoicesAsync(
        IReadOnlyList<POSInvoice> invoices, POSPostingScope scope, CancellationToken cancellationToken = default);

    /// <summary>The shift's invoices that are still unposted — at shift close, in PerShift mode and as a sweep otherwise.</summary>
    Task<JournalEntry?> PostShiftInvoicesAsync(Shift shift, CancellationToken cancellationToken = default);

    Task<JournalEntry?> PostShiftVarianceAsync(Shift shift, CancellationToken cancellationToken = default);

    Task<JournalEntry?> PostReturnAsync(POSReturn posReturn, decimal costAmount, CancellationToken cancellationToken = default);

    Task<JournalEntry?> PostDrawerExpenseAsync(DrawerExpense expense, CancellationToken cancellationToken = default);

    Task<POSPostingMode> GetModeAsync(long? branchId, CancellationToken cancellationToken = default);
}

public enum POSPostingScope
{
    Transaction = 1,
    Shift = 2,
    Day = 3
}

internal sealed class POSPostingService(
    IApplicationDbContext db,
    IPostingTemplateEngine postingEngine,
    IPostingFailureRecorder? failureRecorder = null) : IPOSPostingService
{
    public async Task<POSPostingMode> GetModeAsync(long? branchId, CancellationToken cancellationToken = default) =>
        await db.BranchPOSSettingsRows
            .Where(s => s.BranchId == branchId)
            .Select(s => (POSPostingMode?)s.PostingMode)
            .FirstOrDefaultAsync(cancellationToken)
        ?? new BranchPOSSettings().PostingMode;

    public async Task<JournalEntry?> PostShiftInvoicesAsync(Shift shift, CancellationToken cancellationToken = default)
    {
        var invoices = await db.POSInvoices
            .Include(i => i.Lines)
            .Include(i => i.Payments)
            .Where(i => i.ShiftId == shift.Id && i.JournalEntryId == null && i.Status == POSInvoiceStatus.Posted)
            .ToListAsync(cancellationToken);

        return await PostInvoicesAsync(invoices, POSPostingScope.Shift, cancellationToken);
    }

    public async Task<JournalEntry?> PostInvoicesAsync(
        IReadOnlyList<POSInvoice> invoices, POSPostingScope scope, CancellationToken cancellationToken = default)
    {
        var pending = invoices.Where(i => i.JournalEntryId is null && i.JournalEntry is null).OrderBy(i => i.Id).ToList();
        if (pending.Count == 0)
        {
            return null;
        }

        if (pending.Select(i => i.BranchId).Distinct().Count() > 1)
        {
            // Rule from 2026-09-18: one POS entry never spans two branches.
            throw new InvalidOperationException("A POS entry cannot mix branches.");
        }

        var first = pending[0];
        var companyId = first.CompanyId!.Value;
        var branchId = first.BranchId;

        // Checked before anything else: a branch whose POS is not posting yet must keep selling
        // even if its terminals' payment methods were never linked to treasuries.
        if (!await postingEngine.IsConfiguredAsync(companyId, PostingScreenCatalog.PosInvoice, cancellationToken: cancellationToken))
        {
            return null;
        }

        var terminalIds = pending.Select(i => i.POSTerminalId).Distinct().ToList();
        var shiftIds = pending.Select(i => i.ShiftId).Distinct().ToList();

        var treasuries = await db.POSPaymentMethodConfigs
            .Where(c => terminalIds.Contains(c.POSTerminalId))
            .Select(c => new { c.POSTerminalId, c.PaymentMethodId, c.LinkedTreasuryAccountId })
            .ToListAsync(cancellationToken);

        long TreasuryOf(POSInvoice invoice, POSPayment payment) =>
            treasuries.FirstOrDefault(t => t.POSTerminalId == invoice.POSTerminalId && t.PaymentMethodId == payment.PaymentMethodId)
                ?.LinkedTreasuryAccountId
            ?? throw new BusinessRuleException(
                "POS-POSTING-NO-TREASURY",
                $"طريقة الدفع رقم {payment.PaymentMethodId} مش مربوطة بخزينة على الجهاز رقم {invoice.POSTerminalId} — اربطها من إعداد طرق الدفع.");

        var payments = pending
            .SelectMany(i => i.Payments.Where(p => p.Status == POSPaymentStatus.Completed).Select(p => (Invoice: i, Payment: p)))
            .ToList();

        List<PostingGroupItem> paymentsGroup;
        List<PostingGroupItem> commissionsGroup;
        try
        {
            paymentsGroup = payments
                .GroupBy(x => TreasuryOf(x.Invoice, x.Payment))
                .Select(g => new PostingGroupItem(g.Key, Math.Round(g.Sum(x => x.Payment.Amount), 2)))
                .ToList();
            commissionsGroup = payments
                .Where(x => x.Payment.MachineCommission is > 0)
                .GroupBy(x => TreasuryOf(x.Invoice, x.Payment))
                .Select(g => new PostingGroupItem(g.Key, Math.Round(g.Sum(x => x.Payment.MachineCommission!.Value), 2)))
                .ToList();
        }
        catch (BusinessRuleException e) when (failureRecorder is not null)
        {
            // Fails before the engine is reached, so the engine cannot log it; it belongs in the
            // failed-postings report all the same.
            await failureRecorder.RecordAsync(new PostingFailureRecord(
                companyId, branchId, PostingScreenCatalog.PosInvoice, SourceModule.POS, first.Id,
                $"مبيعات نقطة البيع — {pending.Count} فاتورة", e.Code, e.Message), CancellationToken.None);
            throw;
        }

        var costAmount = Math.Round(pending.SelectMany(i => i.Lines).Sum(l => l.Quantity * l.UnitCost), 2);
        var gross = pending.Sum(i => i.Subtotal);
        var discount = pending.Sum(i => i.DiscountAmount + i.ManualDiscountAmount + i.LoyaltyDiscountAmount);
        long? singleShiftId = shiftIds.Count == 1 ? shiftIds[0] : null;
        long? cashierUserId = singleShiftId is null
            ? null
            : await db.Shifts.Where(s => s.Id == singleShiftId).Select(s => (long?)s.CashierUserId).FirstOrDefaultAsync(cancellationToken);

        var (sourceType, sourceId, date, purpose, description) = scope switch
        {
            POSPostingScope.Transaction => (SourceDocumentType.Invoice, first.Id, first.InvoiceDate, "POS.Invoice",
                $"مبيعات نقطة البيع — فاتورة {first.InvoiceNumber}"),
            POSPostingScope.Shift => (SourceDocumentType.Shift, first.ShiftId, pending.Max(i => i.InvoiceDate), "POS.Shift",
                $"مبيعات نقطة البيع — وردية رقم {first.ShiftId} ({pending.Count} فاتورة)"),
            _ => (SourceDocumentType.POSDay, first.Id, first.InvoiceDate, "POS.Day",
                $"مبيعات نقطة البيع — إقفال يوم {first.InvoiceDate:yyyy-MM-dd} ({pending.Count} فاتورة)")
        };

        var entry = (await postingEngine.PostAsync(new TemplatePostingRequest
        {
            CompanyId = companyId,
            BranchId = branchId,
            ScreenCode = PostingScreenCatalog.PosInvoice,
            SourceModule = SourceModule.POS,
            SourceDocumentType = sourceType,
            SourceDocumentId = sourceId,
            EntryDate = date,
            Description = description,
            // The first invoice identifies the batch: a retry of the same close picks up the same
            // unposted invoices and so the same key, while a later batch starts from a later invoice.
            IdempotencyKey = PostingKeys.For(companyId, purpose, first.Id),
            Context = PostingContext.Create(
                new Dictionary<string, object?>
                {
                    ["BranchId"] = branchId,
                    ["POSTerminalId"] = terminalIds.Count == 1 ? terminalIds[0] : null,
                    ["ShiftId"] = singleShiftId,
                    ["CashierUserId"] = cashierUserId,
                    ["GrossSales"] = gross,
                    ["TotalDiscount"] = discount,
                    ["NetSales"] = gross - discount,
                    ["ServiceCharge"] = pending.Sum(i => i.ServiceChargeAmount),
                    ["Tips"] = pending.Sum(i => i.TipAmount),
                    ["TaxAmount"] = pending.Sum(i => i.TaxAmount),
                    ["TotalAmount"] = pending.Sum(i => i.Total),
                    ["CostAmount"] = costAmount,
                    [PostingScreenCatalog.HasStockMovementField] = costAmount > 0,
                    ["CommissionAmount"] = commissionsGroup.Sum(c => c.Amount)
                },
                pending.SelectMany(i => i.Lines).Select(l => new PostingContextLine(l.ItemId, l.Quantity, l.UnitPrice, l.UnitCost)),
                new Dictionary<string, IReadOnlyList<PostingGroupItem>>
                {
                    [PostingScreenCatalog.PaymentsGroup] = paymentsGroup,
                    [PostingScreenCatalog.CommissionsGroup] = commissionsGroup
                })
        }, cancellationToken)).JournalEntry;

        foreach (var invoice in pending)
        {
            invoice.JournalEntry = entry;
        }

        return entry;
    }

    public async Task<JournalEntry?> PostShiftVarianceAsync(Shift shift, CancellationToken cancellationToken = default)
    {
        var difference = shift.DifferenceAmount ?? 0m;
        if (difference == 0 || shift.VarianceJournalEntryId is not null || shift.VarianceJournalEntry is not null)
        {
            return null;
        }

        var threshold = await db.BranchPOSSettingsRows
            .Where(s => s.BranchId == shift.BranchId)
            .Select(s => (decimal?)s.ShiftVarianceEmployeeLiabilityThreshold)
            .FirstOrDefaultAsync(cancellationToken) ?? new BranchPOSSettings().ShiftVarianceEmployeeLiabilityThreshold;

        // Rule 20: a shortage up to the threshold is the company's; above it, the whole shortage is
        // the cashier's. A surplus is always income, whatever its size.
        var shortage = difference < 0 ? -difference : 0m;
        var minor = shortage > 0 && shortage <= threshold ? shortage : 0m;
        var major = shortage > threshold ? shortage : 0m;
        var surplus = difference > 0 ? difference : 0m;
        var companyId = shift.CompanyId!.Value;

        var entry = await postingEngine.PostIfConfiguredAsync(new TemplatePostingRequest
        {
            CompanyId = companyId,
            BranchId = shift.BranchId,
            ScreenCode = PostingScreenCatalog.PosShiftVariance,
            SourceModule = SourceModule.POS,
            SourceDocumentType = SourceDocumentType.Shift,
            SourceDocumentId = shift.Id,
            EntryDate = DateOnly.FromDateTime(shift.ClosedAtUtc ?? DateTime.UtcNow),
            Description = $"فروق وردية رقم {shift.Id}",
            IdempotencyKey = PostingKeys.For(companyId, "POS.ShiftVariance", shift.Id),
            Context = PostingContext.Create(new Dictionary<string, object?>
            {
                ["BranchId"] = shift.BranchId,
                ["POSTerminalId"] = shift.POSTerminalId,
                ["ShiftId"] = shift.Id,
                ["CashierUserId"] = shift.CashierUserId,
                ["MinorShortage"] = minor,
                ["MajorShortage"] = major,
                ["TotalShortage"] = minor + major,
                ["Surplus"] = surplus
            })
        }, cancellationToken);

        shift.VarianceJournalEntry = entry;
        return entry;
    }

    public async Task<JournalEntry?> PostReturnAsync(POSReturn posReturn, decimal costAmount, CancellationToken cancellationToken = default)
    {
        var source = await db.POSInvoices
            .Where(i => i.Id == posReturn.SourceInvoiceId)
            .Select(i => new { i.TaxAmount, Net = i.Subtotal - i.DiscountAmount - i.ManualDiscountAmount - i.LoyaltyDiscountAmount })
            .FirstOrDefaultAsync(cancellationToken);

        var returnValue = Math.Round(posReturn.Lines.Sum(l => l.Quantity * l.UnitPrice), 2);
        var taxRate = source is { Net: > 0 } ? source.TaxAmount / source.Net : 0m;
        var taxAmount = Math.Round(returnValue * taxRate, 2);
        var companyId = posReturn.CompanyId!.Value;

        var entry = await postingEngine.PostIfConfiguredAsync(new TemplatePostingRequest
        {
            CompanyId = companyId,
            BranchId = posReturn.BranchId,
            ScreenCode = PostingScreenCatalog.PosReturn,
            SourceModule = SourceModule.POS,
            SourceDocumentType = SourceDocumentType.Return,
            SourceDocumentId = posReturn.Id,
            EntryDate = posReturn.ReturnDate,
            Description = $"مرتجع نقطة البيع {posReturn.ReturnNumber}",
            IdempotencyKey = PostingKeys.For(companyId, "POS.Return", posReturn.Id),
            Context = PostingContext.Create(new Dictionary<string, object?>
            {
                ["BranchId"] = posReturn.BranchId,
                ["POSTerminalId"] = posReturn.POSTerminalId,
                ["ShiftId"] = posReturn.ShiftId,
                ["ReturnValue"] = returnValue,
                ["TaxAmount"] = taxAmount,
                ["RefundTotal"] = returnValue + taxAmount,
                ["CostAmount"] = Math.Round(costAmount, 2),
                [PostingScreenCatalog.HasStockMovementField] = Math.Round(costAmount, 2) > 0
            })
        }, cancellationToken);

        posReturn.JournalEntry = entry;
        return entry;
    }

    public async Task<JournalEntry?> PostDrawerExpenseAsync(DrawerExpense expense, CancellationToken cancellationToken = default)
    {
        var terminalId = await db.Shifts.Where(s => s.Id == expense.ShiftId).Select(s => s.POSTerminalId).FirstAsync(cancellationToken);
        var companyId = expense.CompanyId!.Value;

        var entry = await postingEngine.PostIfConfiguredAsync(new TemplatePostingRequest
        {
            CompanyId = companyId,
            BranchId = expense.BranchId,
            ScreenCode = PostingScreenCatalog.PosDrawerExpense,
            SourceModule = SourceModule.POS,
            SourceDocumentType = SourceDocumentType.DrawerExpense,
            SourceDocumentId = expense.Id,
            EntryDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Description = $"مصروف درج: {expense.Description}",
            IdempotencyKey = PostingKeys.For(companyId, "POS.DrawerExpense", expense.Id),
            Context = PostingContext.Create(new Dictionary<string, object?>
            {
                ["BranchId"] = expense.BranchId,
                ["POSTerminalId"] = terminalId,
                ["ShiftId"] = expense.ShiftId,
                ["Amount"] = expense.Amount,
                ["ExpenseAccountId"] = expense.ExpenseAccountId
            })
        }, cancellationToken);

        expense.JournalEntry = entry;
        return entry;
    }
}
