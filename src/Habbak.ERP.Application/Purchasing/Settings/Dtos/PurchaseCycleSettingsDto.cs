namespace Habbak.ERP.Application.Purchasing.Settings.Dtos;

public sealed class PurchaseCycleSettingsDto
{
    public required string CycleType { get; init; }
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
    public required string DefaultPaymentTerms { get; init; }
    public required bool CapitalizeAdditionalCosts { get; init; }
    public required bool AllowManualInvoiceLines { get; init; }
}
