export interface ItemMovementRow {
  id: number;
  transactionDate: string;
  warehouseNameAr: string;
  transactionType: string;
  isInbound: boolean;
  quantity: number;
  unitCost: number;
  batchNumber?: string;
}

export interface StockReportRow {
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  warehouseId: number;
  warehouseNameAr: string;
  quantityOnHand: number;
  standardCost?: number;
  estimatedValue: number;
}

export interface BelowMinimumItemRow {
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  warehouseId: number;
  warehouseNameAr: string;
  quantityOnHand: number;
  minStockLevel: number;
  reorderPoint?: number;
  shortageQuantity: number;
}

export interface InventoryCountReportRow {
  id: number;
  countNumber: string;
  countDate: string;
  warehouseNameAr: string;
  countType: string;
  status: string;
  lineCount: number;
  varianceLineCount: number;
  netVarianceQuantity: number;
}

export interface DailyMovementRow {
  id: number;
  itemCode: string;
  itemNameAr: string;
  warehouseNameAr: string;
  transactionType: string;
  isInbound: boolean;
  quantity: number;
  unitCost: number;
}

export interface RawMaterialConsumptionRow {
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  totalQuantityConsumed: number;
  totalValue: number;
}

export interface WasteReportRow {
  id: number;
  wasteDate: string;
  itemCode: string;
  itemNameAr: string;
  warehouseNameAr: string;
  quantity: number;
  standardCost?: number;
  estimatedValue: number;
  reason: string;
  sourceDocumentType?: string;
}

export interface StockTransferRow {
  id: number;
  documentNumber: string;
  documentDate: string;
  documentType: string;
  sourceWarehouseNameAr?: string;
  destinationWarehouseNameAr?: string;
  status: string;
  custodyOfficerNameAr?: string;
  totalQuantity: number;
}
