export interface InventoryCountListItem {
  id: number;
  countNumber: string;
  countDate: string;
  warehouseId: number;
  warehouseCode: string;
  countType: string;
  status: string;
  lineCount: number;
}

export interface InventoryCountLine {
  id: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  systemQuantity: number;
  /** In the unit below (unitFactor base units make one of it). */
  countedQuantity?: number;
  /** systemQuantity and varianceQuantity are in the item's base unit. */
  varianceQuantity?: number;
  unitId: number;
  unitCode?: string;
  unitNameAr?: string;
  unitFactor: number;
  baseUnitCode?: string;
  settlementDecision: string;
  settlementReason?: string;
}

export interface InventoryCountDetail {
  id: number;
  countNumber: string;
  countDate: string;
  warehouseId: number;
  warehouseCode: string;
  countType: string;
  status: string;
  rowVersion: string;
  lines: InventoryCountLine[];
}

export interface CreateInventoryCountInput {
  warehouseId: number;
  countDate: string;
  countType: string;
  itemIds?: number[];
}

export interface CountedQuantityInput {
  lineId: number;
  countedQuantity: number;
  /** The line's current unit when left out. */
  unitId?: number;
}

export interface SettleLineInput {
  decision: 'Approved' | 'Rejected';
  settlementReason?: string;
}
