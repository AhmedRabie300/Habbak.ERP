using Habbak.ERP.Domain.Accounting;

namespace Habbak.ERP.Application.Common.Interfaces;

/// <summary>Yearly sequence per company AND per VoucherType (01-Module-Accounting.md, Voucher.VoucherNumber).</summary>
public interface IVoucherNumberGenerator
{
    Task<string> GenerateAsync(long companyId, VoucherType voucherType, int year, CancellationToken cancellationToken = default);
}
