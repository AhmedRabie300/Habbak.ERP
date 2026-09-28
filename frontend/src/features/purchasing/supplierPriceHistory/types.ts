export interface SupplierPriceHistoryRow {
  id: number;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  unitPrice: number;
  unitCode: string;
  effectiveDate: string;
  purchaseInvoiceNumber?: string;
  priceChange?: number;
}
