using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Settings.Access;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.PurchaseOrders.Commands.SendPurchaseOrder;

/// <summary>
/// Draft → Sent (section 6.3) — marks the order as transmitted to the supplier. No actual
/// email/fax integration here, just the status transition the rest of the flow keys off of.
///
/// Remarks4 item 6 — PurchaseCycleSettings.RequiresApprovalForPurchaseOrder: when the company
/// requires an approval, sending an order to a supplier needs the Approve right on the purchase
/// orders screen (the order's own state machine has no Approved step, and the Approval Workflow
/// engine of 07-Module-Settings-Permissions is not built yet — the same stand-in the fixed assets
/// module uses for transfers and disposals).
/// </summary>
public sealed record SendPurchaseOrderCommand(long Id) : IRequest;

public sealed class SendPurchaseOrderCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext, IUserAccessService access)
    : IRequestHandler<SendPurchaseOrderCommand>
{
    public async Task Handle(SendPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.Id);

        if (order.Status != PurchaseOrderStatus.Draft)
        {
            throw new BusinessRuleException("PUR-ORDER-NOT-DRAFT", "لا يمكن إرسال أمر الشراء إلا وهو في حالة مسودة.");
        }

        var cycleSettings = await db.PurchaseCycleSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);
        if (cycleSettings?.RequiresApprovalForPurchaseOrder == true)
        {
            var rights = await access.GetCurrentAsync(cancellationToken);
            if (!rights.Can(ScreenAction.Approve, ["PURCHASING_PURCHASE_ORDERS"]))
            {
                throw new ForbiddenException(
                    "PUR-ORDER-APPROVAL-REQUIRED", "دورة المشتريات المفعّلة تتطلب اعتماد أمر الشراء — محتاج صلاحية الاعتماد على الشاشة.");
            }
        }

        order.Status = PurchaseOrderStatus.Sent;

        await db.SaveChangesAsync(cancellationToken);
    }
}
