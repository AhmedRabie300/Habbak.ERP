export interface WasteRecordListItem {
  id: number;
  warehouseId: number;
  warehouseCode: string;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  wasteDate: string;
  reason: string;
  sourceDocumentType?: string;
}

export interface WasteRecordDetail {
  id: number;
  warehouseId: number;
  warehouseCode: string;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  wasteDate: string;
  reason: string;
  sourceDocumentType?: string;
  sourceDocumentId?: number;
  rowVersion: string;
}

export interface WasteRecordFormValues {
  warehouseId: number;
  itemId: number;
  quantity: number;
  wasteDate: string;
  reason: string;
}
