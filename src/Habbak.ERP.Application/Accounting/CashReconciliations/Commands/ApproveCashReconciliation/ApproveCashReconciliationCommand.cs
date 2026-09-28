using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Accounting.CashReconciliations.Commands.ApproveCashReconciliation;

/// <summary>Rule 19: explicit approval required even when the difference is zero.</summary>
public sealed record ApproveCashReconciliationCommand(long Id) : IRequest;

public sealed class ApproveCashReconciliationCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<ApproveCashReconciliationCommand>
{
    public async Task Handle(ApproveCashReconciliationCommand request, CancellationToken cancellationToken)
    {
        var reconciliation = await db.CashReconciliations.FindAsync([request.Id], cancellationToken)
            ?? throw new NotFoundException(nameof(CashReconciliation), request.Id);

        if (reconciliation.ApprovedAtUtc is not null)
        {
            throw new BusinessRuleException("ACC-CASH-RECON-ALREADY-APPROVED", "المطابقة معتمدة بالفعل.");
        }

        // Rule 18: no Draft vouchers on this treasury account, same day, still unposted.
        var hasDraftVouchers = await db.Vouchers.AnyAsync(
            v => v.TreasuryAccountId == reconciliation.TreasuryAccountId
                && v.VoucherDate == reconciliation.ReconciliationDate
                && v.Status == VoucherStatus.Draft,
            cancellationToken);

        if (hasDraftVouchers)
        {
            throw new BusinessRuleException(
                "ACC-R18-DRAFT-VOUCHERS-SAME-DAY", "لا يمكن اعتماد المطابقة — يوجد سندات بنفس اليوم لسه في حالة مسودة.");
        }

        reconciliation.ApprovedByUserId = currentCompanyContext.UserId;
        reconciliation.ApprovedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}
