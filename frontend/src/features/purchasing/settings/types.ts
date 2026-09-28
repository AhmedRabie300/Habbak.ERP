/** The document flags a named cycle implies (Remarks4, item 6). */
export interface PurchaseCyclePreset {
  cycleType: string;
  requiresPurchaseRequest: boolean;
  requiresQuotation: boolean;
  requiresPurchaseOrder: boolean;
  requiresGoodsReceipt: boolean;
  allowInvoiceWithoutOrder: boolean;
  allowReceiptWithoutInvoice: boolean;
}

export interface PurchaseCycleSettings {
  cycleType: string;
  requiresPurchaseRequest: boolean;
  requiresQuotation: boolean;
  requiresPurchaseOrder: boolean;
  requiresGoodsReceipt: boolean;
  allowInvoiceWithoutOrder: boolean;
  allowReceiptWithoutInvoice: boolean;
  autoCreateReceiptOnInvoicePost: boolean;
  autoCreateInvoiceOnReceipt: boolean;
  requiresApprovalForPurchaseOrder: boolean;
  requiresApprovalForInvoice: boolean;
  defaultPaymentTerms: string;
  capitalizeAdditionalCosts: boolean;
  allowManualInvoiceLines: boolean;
}
