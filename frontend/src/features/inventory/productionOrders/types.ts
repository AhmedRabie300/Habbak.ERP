export interface ProductionOrderListItem {
  id: number;
  orderNumber: string;
  warehouseId: number;
  warehouseCode: string;
  recipeFamilyCode: string;
  recipeVersionNumber: number;
  outputItemCode: string;
  outputItemNameAr: string;
  plannedQuantity: number;
  actualQuantity?: number;
  status: string;
}

export interface ProductionOrderComponentPreview {
  componentItemId: number;
  componentItemCode: string;
  componentItemNameAr: string;
  plannedConsumption: number;
}

export interface ProductionOrderDetail {
  id: number;
  orderNumber: string;
  warehouseId: number;
  warehouseCode: string;
  recipeId: number;
  recipeFamilyCode: string;
  recipeVersionNumber: number;
  outputItemId: number;
  outputItemCode: string;
  outputItemNameAr: string;
  recipeOutputQuantity: number;
  plannedQuantity: number;
  actualQuantity?: number;
  status: string;
  startDate?: string;
  endDate?: string;
  executedByUserId: number;
  standardCost: number;
  actualCost?: number;
  productionIssueDocumentId?: number;
  productionReceiptDocumentId?: number;
  rowVersion: string;
  componentsPreview: ProductionOrderComponentPreview[];
}

export interface ProductionOrderFormValues {
  warehouseId: number;
  recipeId: number;
  plannedQuantity: number;
  startDate?: string;
}

export interface CompleteProductionOrderInput {
  actualQuantity: number;
  endDate: string;
  actualWasteQuantity?: number;
  wasteReason?: string;
}

export interface CompleteProductionOrderResult {
  status: string;
  actualCost: number;
  costVariance: number;
  productionIssueDocumentId: number;
  productionReceiptDocumentId: number;
}
