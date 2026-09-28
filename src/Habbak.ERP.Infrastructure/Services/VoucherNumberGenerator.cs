using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;

namespace Habbak.ERP.Infrastructure.Services;

/// <summary>
/// Delegates to the system-wide ICodeGenerator (screens "ACCOUNTING_RECEIPT_VOUCHERS" /
/// "ACCOUNTING_PAYMENT_VOUCHERS") — see JournalEntryNumberGenerator for the same
/// companyId/year-now-unused and per-year-reset-dropped notes.
/// </summary>
public class VoucherNumberGenerator(ICodeGenerator codeGenerator) : IVoucherNumberGenerator
{
    public Task<string> GenerateAsync(
        long companyId, VoucherType voucherType, int year, CancellationToken cancellationToken = default)
    {
        var screenCode = voucherType == VoucherType.Receipt ? "ACCOUNTING_RECEIPT_VOUCHERS" : "ACCOUNTING_PAYMENT_VOUCHERS";
        return codeGenerator.ResolveCodeAsync(screenCode, manualCode: null, cancellationToken);
    }
}
