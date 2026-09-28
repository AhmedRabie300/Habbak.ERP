using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.Vouchers.Commands.CancelVoucher;

/// <summary>
/// Cancels a Draft voucher (section 4.2: "إلغاء فقط لو لسه مسودة" — a Posted voucher cannot be
/// cancelled directly; it is corrected via ReverseVoucherCommand instead).
/// </summary>
public sealed record CancelVoucherCommand(long Id) : IRequest;

public sealed class CancelVoucherCommandHandler(IApplicationDbContext db) : IRequestHandler<CancelVoucherCommand>
{
    public async Task Handle(CancelVoucherCommand request, CancellationToken cancellationToken)
    {
        var voucher = await db.Vouchers.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Voucher), request.Id);

        if (voucher.Status != VoucherStatus.Draft)
        {
            throw new BusinessRuleException(
                "ACC-VOUCHER-NOT-DRAFT", "لا يمكن إلغاء سند إلا وهو في حالة مسودة — السند المرحّل يُصحَّح بقيد عكسي بدل إلغائه.");
        }

        voucher.Status = VoucherStatus.Cancelled;
        await db.SaveChangesAsync(cancellationToken);
    }
}
