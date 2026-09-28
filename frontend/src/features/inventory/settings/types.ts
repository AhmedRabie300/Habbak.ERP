export interface ShortagePolicy {
  allowOverrideOnShortage: boolean;
  requiresApprovalForOverride: boolean;
}

export interface InventorySettings {
  slowMovingThresholdDays: number;
}

export interface ProductionSalesModeSetting {
  id: number;
  scopeType: string;
  scopeId?: number;
  mode: string;
}

export interface CreateProductionSalesModeSettingInput {
  scopeType: string;
  scopeId?: number;
  mode: string;
}
