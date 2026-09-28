export interface GoodsReceiptListItem {
  id: number;
  receiptNumber: string;
  receiptDate: string;
  warehouseId: number;
  warehouseCode: string;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  lineCount: number;
  status: string;
}

export interface GoodsReceiptLine {
  id?: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  rejectedReason?: string;
  rejectedWarehouseId?: number;
  unitCost: number;
  unitId: number;
  unitCode: string;
  expectedQuantity?: number;
  varianceQuantity?: number;
  varianceReason?: string;
  batchNumber?: string;
  expiryDate?: string;
  qualityCheckStatus: string;
  qualityCheckNotes?: string;
}

export interface GoodsReceiptDetail {
  id: number;
  receiptNumber: string;
  receiptDate: string;
  branchId?: number;
  warehouseId: number;
  warehouseCode: string;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  purchaseOrderId: number;
  purchaseOrderNumber: string;
  status: string;
  notes?: string;
  rowVersion: string;
  lines: GoodsReceiptLine[];
}

export interface GoodsReceiptLineInput {
  itemId: number;
  quantity: number;
  acceptedQuantity: number;
  rejectedQuantity: number;
  rejectedReason?: string;
  rejectedWarehouseId?: number;
  unitCost: number;
  /** The item's base unit when left out. */
  unitId?: number;
  varianceReason?: string;
  batchNumber?: string;
  expiryDate?: string;
  qualityCheckStatus: string;
  qualityCheckNotes?: string;
}

export interface GoodsReceiptFormValues {
  branchId?: number;
  warehouseId: number;
  receiptDate: string;
  purchaseOrderId: number;
  notes?: string;
  lines: GoodsReceiptLineInput[];
}

export interface PostableOrderLine {
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  remainingQuantity: number;
  unitId: number;
  unitCode: string;
}

export interface PostablePurchaseOrder {
  id: number;
  orderNumber: string;
  supplierId: number;
  supplierCode: string;
  lines: PostableOrderLine[];
}
