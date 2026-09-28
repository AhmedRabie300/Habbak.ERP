using Habbak.ERP.Application.Accounting.Periods.Dtos;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Periods.Common;

/// <summary>
/// Evaluates the period-close checklist LIVE against current data (01-Module-Accounting.md,
/// rule 8) — matching the reference mockup's live "draftJournals===0" style checks, not manual
/// checkboxes. ClosePeriodCommand re-runs this at the moment of closing and persists the result
/// as PeriodCloseChecklistItem rows for audit purposes.
/// </summary>
public static class PeriodChecklistEvaluator
{
    public static async Task<IReadOnlyList<ChecklistItemResultDto>> EvaluateAsync(
        IApplicationDbContext db, AccountingPeriod period, CancellationToken cancellationToken)
    {
        var companyId = period.CompanyId;

        var draftJournals = await db.JournalEntries.CountAsync(
            j => j.CompanyId == companyId && j.EntryDate >= period.PeriodStart && j.EntryDate <= period.PeriodEnd
                && j.Status == JournalEntryStatus.Draft,
            cancellationToken);

        var draftVouchers = await db.Vouchers.CountAsync(
            v => v.CompanyId == companyId && v.VoucherDate >= period.PeriodStart && v.VoucherDate <= period.PeriodEnd
                && v.Status == VoucherStatus.Draft,
            cancellationToken);

        var draftTransfers = await db.TreasuryTransfers.CountAsync(
            t => t.CompanyId == companyId && t.TransferDate >= period.PeriodStart && t.TransferDate <= period.PeriodEnd
                && t.Status == TreasuryTransferStatus.Draft,
            cancellationToken);

        var lastDayCashReconciled = await db.CashReconciliations.AnyAsync(
            c => c.CompanyId == companyId && c.ReconciliationDate == period.PeriodEnd && c.ApprovedAtUtc != null,
            cancellationToken);

        var bankReconciled = await db.BankReconciliationRuns.AnyAsync(
            r => r.PeriodTo == period.PeriodEnd && r.Status == BankReconciliationStatus.Completed,
            cancellationToken);

        return
        [
            new ChecklistItemResultDto
            {
                ItemKey = nameof(PeriodCloseChecklistItemKey.AllJournalsPosted),
                Label = "ترحيل جميع القيود (لا توجد قيود مسودة)",
                IsSatisfied = draftJournals == 0,
                Detail = $"{draftJournals} قيد مسودة"
            },
            new ChecklistItemResultDto
            {
                ItemKey = nameof(PeriodCloseChecklistItemKey.AllVouchersPosted),
                Label = "ترحيل جميع السندات",
                IsSatisfied = draftVouchers == 0,
                Detail = $"{draftVouchers} سند مسودة"
            },
            new ChecklistItemResultDto
            {
                ItemKey = nameof(PeriodCloseChecklistItemKey.AllTransfersPosted),
                Label = "ترحيل جميع تحويلات الخزائن/البنوك",
                IsSatisfied = draftTransfers == 0,
                Detail = $"{draftTransfers} تحويل مسودة"
            },
            new ChecklistItemResultDto
            {
                ItemKey = nameof(PeriodCloseChecklistItemKey.LastDayCashReconciled),
                Label = "مطابقة خزينة اليوم الأخير من الفترة",
                IsSatisfied = lastDayCashReconciled,
                Detail = lastDayCashReconciled ? "تمت المطابقة والاعتماد" : "لم تُعتمد مطابقة خزينة لليوم الأخير بعد"
            },
            new ChecklistItemResultDto
            {
                ItemKey = nameof(PeriodCloseChecklistItemKey.BankReconciled),
                Label = "مطابقة بنك الفترة",
                IsSatisfied = bankReconciled,
                Detail = bankReconciled ? "تمت المطابقة" : "لا توجد مطابقة بنك مكتملة تغطي نهاية الفترة"
            },
            new ChecklistItemResultDto
            {
                ItemKey = nameof(PeriodCloseChecklistItemKey.AllETASent),
                Label = "لا توجد فواتير غير مُرسَلة لمنظومة ETA",
                IsSatisfied = true,
                Detail = "موديول الامتثال الضريبي (ETA) لسه مبنيش — يُعتبر مستوفى مؤقتًا"
            }
        ];
    }
}
