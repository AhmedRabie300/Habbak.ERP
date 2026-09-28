export interface RFQListItem {
  id: number;
  rfqNumber: string;
  rfqDate: string;
  lineCount: number;
  supplierCount: number;
  status: string;
}

export interface RFQLine {
  id: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  unitId: number;
  unitCode: string;
}

export interface RFQSupplierQuote {
  id: number;
  rfqLineId: number;
  unitPrice: number;
  discountPercentage?: number;
  deliveryDays?: number;
  validUntil: string;
  isExpired: boolean;
  isSelected: boolean;
  notes?: string;
}

export interface RFQSupplier {
  id: number;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  status: string;
  responseDate?: string;
  quotes: RFQSupplierQuote[];
}

export interface RFQDetail {
  id: number;
  rfqNumber: string;
  rfqDate: string;
  branchId?: number;
  purchaseRequestId?: number;
  purchaseRequestNumber?: string;
  status: string;
  requiredDate?: string;
  notes?: string;
  rowVersion: string;
  lines: RFQLine[];
  suppliers: RFQSupplier[];
}

export interface RFQLineInput {
  itemId: number;
  quantity: number;
  unitId: number;
}

export interface RFQFormValues {
  branchId?: number;
  rfqDate: string;
  purchaseRequestId?: number;
  requiredDate?: string;
  notes?: string;
  lines: RFQLineInput[];
  supplierIds: number[];
}

export interface SetRFQSupplierQuoteInput {
  rfqSupplierId: number;
  rfqLineId: number;
  unitPrice: number;
  discountPercentage?: number;
  deliveryDays?: number;
  validUntil: string;
  notes?: string;
}
