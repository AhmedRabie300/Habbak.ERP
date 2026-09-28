export interface PriceListListItem {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  effectiveFromDate: string;
  effectiveToDate?: string;
  branchCount: number;
  itemCount: number;
  isActive: boolean;
}

export interface PriceListLine {
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  dineInPrice: number;
  takeawayPrice: number;
  deliveryPrice: number;
}

export interface PriceListLineInput {
  itemId: number;
  dineInPrice: number;
  takeawayPrice: number;
  deliveryPrice: number;
}

export interface PriceListDetail {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  effectiveFromDate: string;
  effectiveToDate?: string;
  isActive: boolean;
  branchIds: number[];
  lines: PriceListLine[];
}

export interface PriceListFormValues {
  code?: string;
  nameAr: string;
  nameEn: string;
  effectiveFromDate: string;
  effectiveToDate?: string;
  branchIds: number[];
  lines: PriceListLineInput[];
  isActive: boolean;
}
