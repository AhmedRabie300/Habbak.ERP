export interface RecipeListItem {
  id: number;
  recipeFamilyCode: string;
  versionNumber: number;
  outputItemId: number;
  outputItemCode: string;
  outputItemNameAr: string;
  outputQuantity: number;
  isCurrentVersion: boolean;
  status: string;
}

export interface RecipeLine {
  id?: number;
  componentItemId: number;
  componentItemCode: string;
  componentItemNameAr: string;
  quantity: number;
  /** The unit quantity is in; unitFactor base units make one of it. */
  unitId: number;
  unitCode?: string;
  unitNameAr?: string;
  unitFactor: number;
  /** Per base unit of the component. */
  componentStandardCost?: number;
}

export interface RecipeDetail {
  id: number;
  recipeFamilyCode: string;
  versionNumber: number;
  previousVersionId?: number;
  isCurrentVersion: boolean;
  outputItemId: number;
  outputItemCode: string;
  outputItemNameAr: string;
  outputQuantity: number;
  wastePercentage: number;
  status: string;
  effectiveFromDate: string;
  rowVersion: string;
  estimatedComponentCost: number;
  costPerOutputUnit: number;
  lines: RecipeLine[];
}

export interface RecipeLineInput {
  componentItemId: number;
  quantity: number;
  /** The component's base unit when left out. */
  unitId?: number;
}

export interface RecipeFormValues {
  outputItemId: number;
  outputQuantity: number;
  wastePercentage: number;
  effectiveFromDate: string;
  lines: RecipeLineInput[];
}
