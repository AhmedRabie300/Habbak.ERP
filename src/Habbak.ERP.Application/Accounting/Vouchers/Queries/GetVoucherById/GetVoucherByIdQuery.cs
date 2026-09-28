using Habbak.ERP.Application.Accounting.Vouchers.Dtos;
using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Vouchers.Queries.GetVoucherById;

public sealed record GetVoucherByIdQuery(long Id) : IRequest<VoucherDetailDto>;

public sealed class GetVoucherByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetVoucherByIdQuery, VoucherDetailDto>
{
    public async Task<VoucherDetailDto> Handle(GetVoucherByIdQuery request, CancellationToken cancellationToken)
    {
        var voucher = await db.Vouchers.AsNoTracking().FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Voucher), request.Id);

        return new VoucherDetailDto
        {
            Id = voucher.Id,
            PublicId = voucher.PublicId,
            BranchId = voucher.BranchId,
            VoucherType = voucher.VoucherType.ToString(),
            VoucherNumber = voucher.VoucherNumber,
            VoucherDate = voucher.VoucherDate,
            TreasuryAccountId = voucher.TreasuryAccountId,
            Description = voucher.Description,
            CounterpartyType = voucher.CounterpartyType.ToString(),
            CounterpartyId = voucher.CounterpartyId,
            DirectAccountId = voucher.DirectAccountId,
            Amount = voucher.Amount,
            CurrencyCode = voucher.CurrencyCode,
            ExchangeRate = voucher.ExchangeRate,
            BaseCurrencyAmount = voucher.BaseCurrencyAmount,
            RelatedInvoiceId = voucher.RelatedInvoiceId,
            Status = voucher.Status.ToString(),
            JournalEntryId = voucher.JournalEntryId,
            RowVersion = Convert.ToBase64String(voucher.RowVersion)
        };
    }
}
