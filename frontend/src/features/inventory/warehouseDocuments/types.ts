export type WarehouseDocumentKind = 'stock-in' | 'stock-out' | 'transfer-order' | 'inventory-adjustments' | 'opening-balances';

/** Broader than WarehouseDocumentKind — includes the read-only ProductionIssue/ProductionReceipt
 * kinds (screen #20) that only ever call the List/GetById hooks below, never Create/Update/Post,
 * so they don't need to satisfy WarehouseDocumentEditPage's per-kind Create/Edit metadata. */
export type WarehouseDocumentReadableKind = WarehouseDocumentKind | 'production-issues' | 'production-receipts';

export interface WarehouseDocumentListItem {
  id: number;
  documentNumber: string;
  documentDate: string;
  sourceWarehouseCode?: string;
  destinationWarehouseCode?: string;
  custodyOfficerCode?: string;
  lineCount: number;
  status: string;
}

export interface WarehouseDocumentLine {
  id?: number;
  lineNumber: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  unitCost: number;
  /** The unit quantity and unitCost are in; unitFactor base units make one of it. */
  unitId: number;
  unitCode?: string;
  unitNameAr?: string;
  unitFactor: number;
  batchNumber?: string;
  expiryDate?: string;
  expectedQuantity?: number;
  varianceQuantity?: number;
}

export interface WarehouseDocumentDetail {
  id: number;
  documentType: string;
  branchId?: number;
  documentNumber: string;
  documentDate: string;
  sourceWarehouseId?: number;
  destinationWarehouseId?: number;
  custodyOfficerId?: number;
  relatedWarehouseDocumentId?: number;
  relatedWarehouseDocumentNumber?: string;
  status: string;
  notes?: string;
  rowVersion: string;
  lines: WarehouseDocumentLine[];
}

export interface WarehouseDocumentLineInput {
  itemId: number;
  quantity: number;
  unitCost: number;
  /** The item's base unit when left out. */
  unitId?: number;
  batchNumber?: string;
  expiryDate?: string;
}

export interface WarehouseDocumentFormValues {
  documentDate: string;
  sourceWarehouseId?: number;
  destinationWarehouseId?: number;
  custodyOfficerId?: number;
  notes?: string;
  lines: WarehouseDocumentLineInput[];
}

export interface TransferReceiptLineInput {
  itemId: number;
  quantity: number;
  batchNumber?: string;
  expiryDate?: string;
}

export interface TransferReceiptFormValues {
  relatedWarehouseDocumentId: number;
  documentDate: string;
  destinationWarehouseId: number;
  notes?: string;
  lines: TransferReceiptLineInput[];
}

export interface PostedTransferOrder {
  id: number;
  documentNumber: string;
  sourceWarehouseCode: string;
  custodyOfficerCode: string;
}
