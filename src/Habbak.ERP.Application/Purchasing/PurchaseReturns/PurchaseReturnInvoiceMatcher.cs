using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.PurchaseReturns.Dtos;
using Habbak.ERP.Domain.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseReturns;

/// <summary>
/// Checks a return's lines against the invoice they came off (Remarks4, item 8). A return that
/// names an invoice may only send back what that invoice actually billed, in the quantity it billed,
/// minus whatever earlier returns already sent back — so a supplier can never be debited for goods
/// they never sold us, and the same carton cannot be returned twice.
///
/// A return with no invoice (goods received on an order that was never invoiced, a mistake found
/// long after the paperwork was archived) keeps working as before: nothing to check against.
/// </summary>
internal static class PurchaseReturnInvoiceMatcher
{
    /// <summary>Base quantity already returned per invoice line, ignoring the return being edited.</summary>
    public static async Task<Dictionary<long, decimal>> ReturnedByInvoiceLineAsync(
        IApplicationDbContext db, long invoiceId, long? excludeReturnId, CancellationToken cancellationToken) =>
        await db.PurchaseReturnLines
            .Where(l => l.PurchaseInvoiceLineId != null
                        && l.PurchaseReturn!.PurchaseInvoiceId == invoiceId
                        && l.PurchaseReturn.Status != PurchaseReturnStatus.Cancelled
                        && (excludeReturnId == null || l.PurchaseReturnId != excludeReturnId))
            .GroupBy(l => l.PurchaseInvoiceLineId!.Value)
            .Select(g => new { LineId = g.Key, BaseQuantity = g.Sum(l => l.BaseQuantity) })
            .ToDictionaryAsync(x => x.LineId, x => x.BaseQuantity, cancellationToken);

    /// <summary>
    /// Validates every line of an invoice-linked return and hands back the invoice line each one
    /// belongs to. Throws on the first problem, so the caller never half-builds a return.
    /// </summary>
    public static async Task<Dictionary<int, long>> ValidateAsync(
        IApplicationDbContext db,
        long invoiceId,
        long? excludeReturnId,
        IReadOnlyList<PurchaseReturnLineInput> lines,
        Func<int, decimal> baseQuantityOf,
        CancellationToken cancellationToken)
    {
        var invoiceLines = await db.PurchaseInvoiceLines.AsNoTracking()
            .Where(l => l.PurchaseInvoiceId == invoiceId)
            .Select(l => new { l.Id, l.ItemId, l.BaseQuantity })
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        if (invoiceLines.Count == 0)
        {
            throw new BusinessRuleException("PUR-RETURN-NO-LINES", "الفاتورة المختارة مالهاش بنود يترد منها.");
        }

        var alreadyReturned = await ReturnedByInvoiceLineAsync(db, invoiceId, excludeReturnId, cancellationToken);
        var resolved = new Dictionary<int, long>();
        var requestedByLine = new Dictionary<long, decimal>();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.PurchaseInvoiceLineId is not { } invoiceLineId)
            {
                throw new BusinessRuleException(
                    "PUR-RETURN-INVALID-ITEM", "المردود المرتبط بفاتورة لازم كل بند فيه يكون مربوط ببند من بنودها.");
            }

            if (!invoiceLines.TryGetValue(invoiceLineId, out var invoiceLine))
            {
                throw new BusinessRuleException(
                    "PUR-RETURN-INVALID-ITEM", "فيه بند مربوط ببند مش موجود في الفاتورة دي.");
            }

            if (invoiceLine.ItemId != line.ItemId)
            {
                throw new BusinessRuleException(
                    "PUR-RETURN-INVALID-ITEM", "الصنف في المردود مختلف عن صنف بند الفاتورة المربوط بيه.");
            }

            requestedByLine[invoiceLineId] = requestedByLine.GetValueOrDefault(invoiceLineId) + baseQuantityOf(index);
            resolved[index] = invoiceLineId;
        }

        foreach (var (invoiceLineId, requested) in requestedByLine)
        {
            var returnable = invoiceLines[invoiceLineId].BaseQuantity - alreadyReturned.GetValueOrDefault(invoiceLineId);
            if (requested > returnable)
            {
                throw new BusinessRuleException(
                    "PUR-RETURN-EXCEEDS-INVOICE",
                    $"الكمية المردودة أكبر من المتبقي في بند الفاتورة (المتاح للرد: {Math.Max(0m, returnable):N4} بالوحدة الأساسية).");
            }
        }

        return resolved;
    }
}
