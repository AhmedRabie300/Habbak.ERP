using Habbak.ERP.Application.Common.Interfaces;
using Habbak.ERP.Application.Purchasing.Settings.Dtos;
using Habbak.ERP.Domain.Purchasing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Habbak.ERP.Application.Purchasing.Settings.Queries.GetPurchaseCycleSettings;

/// <summary>Screen #11 — one row per company; returns the entity's own defaults (Full cycle,
/// PO + receipt required) when no row has been configured yet.</summary>
public sealed record GetPurchaseCycleSettingsQuery : IRequest<PurchaseCycleSettingsDto>;

public sealed class GetPurchaseCycleSettingsQueryHandler(IApplicationDbContext db, ICurrentCompanyContext currentCompanyContext)
    : IRequestHandler<GetPurchaseCycleSettingsQuery, PurchaseCycleSettingsDto>
{
    public async Task<PurchaseCycleSettingsDto> Handle(GetPurchaseCycleSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await db.PurchaseCycleSettingsRows
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == currentCompanyContext.CompanyId, cancellationToken)
            ?? new PurchaseCycleSettings();

        return new PurchaseCycleSettingsDto
        {
            CycleType = settings.CycleType.ToString(),
            RequiresPurchaseRequest = settings.RequiresPurchaseRequest,
            RequiresQuotation = settings.RequiresQuotation,
            RequiresPurchaseOrder = settings.RequiresPurchaseOrder,
            RequiresGoodsReceipt = settings.RequiresGoodsReceipt,
            AllowInvoiceWithoutOrder = settings.AllowInvoiceWithoutOrder,
            AllowReceiptWithoutInvoice = settings.AllowReceiptWithoutInvoice,
            AutoCreateReceiptOnInvoicePost = settings.AutoCreateReceiptOnInvoicePost,
            AutoCreateInvoiceOnReceipt = settings.AutoCreateInvoiceOnReceipt,
            RequiresApprovalForPurchaseOrder = settings.RequiresApprovalForPurchaseOrder,
            RequiresApprovalForInvoice = settings.RequiresApprovalForInvoice,
            DefaultPaymentTerms = settings.DefaultPaymentTerms.ToString(),
            CapitalizeAdditionalCosts = settings.CapitalizeAdditionalCosts,
            AllowManualInvoiceLines = settings.AllowManualInvoiceLines
        };
    }
}
