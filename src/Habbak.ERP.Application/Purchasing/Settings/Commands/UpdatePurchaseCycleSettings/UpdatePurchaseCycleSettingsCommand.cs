using Habbak.ERP.Application.Common.Exceptions;
using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.Settings.Commands.UpdatePurchaseCycleSettings;

/// <summary>Screen #11 — upserts the single PurchaseCycleSettings row for the current company.</summary>
public sealed record UpdatePurchaseCycleSettingsCommand : IRequest
{
    public required PurchaseCycleType CycleType { get; init; }
    public required bool RequiresPurchaseRequest { get; init; }
    public required bool RequiresQuotation { get; init; }
    public required bool RequiresPurchaseOrder { get; init; }
    public required bool RequiresGoodsReceipt { get; init; }
    public required bool AllowInvoiceWithoutOrder { get; init; }
    public required bool AllowReceiptWithoutInvoice { get; init; }
    public required bool AutoCreateReceiptOnInvoicePost { get; init; }
    public required bool AutoCreateInvoiceOnReceipt { get; init; }
    public required bool RequiresApprovalForPurchaseOrder { get; init; }
    public required bool RequiresApprovalForInvoice { get; init; }
    public required SupplierPaymentTerms DefaultPaymentTerms { get; init; }
    public required bool CapitalizeAdditionalCosts { get; init; }

    /// <summary>Remarks6: may an invoice raised from an order also carry lines that order never had?</summary>
    public required bool AllowManualInvoiceLines { get; init; }
}

public sealed class UpdatePurchaseCycleSettingsCommandHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<UpdatePurchaseCycleSettingsCommand>
{
    public async Task Handle(UpdatePurchaseCycleSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await db.PurchaseCycleSettingsRows
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken);

        if (settings is null)
        {
            settings = new PurchaseCycleSettings { CompanyId = currentCompanyContext.CompanyId };
            db.PurchaseCycleSettingsRows.Add(settings);
        }

        // Remarks4 item 6: the named cycle defines which documents the company requires, so a
        // combination that contradicts it is a mistake, not a preference. The screen applies the
        // preset when the type changes, so this only fires on a hand-built request.
        var conflicts = PurchaseCyclePresets.Conflicts(
            request.CycleType, request.RequiresPurchaseRequest, request.RequiresQuotation, request.RequiresPurchaseOrder,
            request.RequiresGoodsReceipt, request.AllowInvoiceWithoutOrder, request.AllowReceiptWithoutInvoice);
        if (conflicts.Count > 0)
        {
            throw new BusinessRuleException(
                "PUR-CYCLE-TYPE-MISMATCH",
                $"الإعدادات دي مش متوافقة مع نمط الدورة المختار ({request.CycleType}): {string.Join(", ", conflicts)}.");
        }

        settings.CycleType = request.CycleType;
        settings.RequiresPurchaseRequest = request.RequiresPurchaseRequest;
        settings.RequiresQuotation = request.RequiresQuotation;
        settings.RequiresPurchaseOrder = request.RequiresPurchaseOrder;
        settings.RequiresGoodsReceipt = request.RequiresGoodsReceipt;
        settings.AllowInvoiceWithoutOrder = request.AllowInvoiceWithoutOrder;
        settings.AllowReceiptWithoutInvoice = request.AllowReceiptWithoutInvoice;
        settings.AutoCreateReceiptOnInvoicePost = request.AutoCreateReceiptOnInvoicePost;
        settings.AutoCreateInvoiceOnReceipt = request.AutoCreateInvoiceOnReceipt;
        settings.RequiresApprovalForPurchaseOrder = request.RequiresApprovalForPurchaseOrder;
        settings.RequiresApprovalForInvoice = request.RequiresApprovalForInvoice;
        settings.DefaultPaymentTerms = request.DefaultPaymentTerms;
        settings.CapitalizeAdditionalCosts = request.CapitalizeAdditionalCosts;
        settings.AllowManualInvoiceLines = request.AllowManualInvoiceLines;

        await db.SaveChangesAsync(cancellationToken);
    }
}
