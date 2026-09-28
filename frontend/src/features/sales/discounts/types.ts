export interface DiscountListItem {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  discountType: string;
  value: number;
  applicationPriority: number;
  isStackable: boolean;
  isHappyHour: boolean;
  isActive: boolean;
}

export interface DiscountDetail {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  discountType: string;
  value: number;
  applicationPriority: number;
  isStackable: boolean;
  minInvoiceAmount?: number;
  minQuantity?: number;
  isHappyHour: boolean;
  happyHourFromTime?: string;
  happyHourToTime?: string;
  effectiveFromDate: string;
  effectiveToDate?: string;
  isActive: boolean;
}

export interface DiscountFormValues {
  code?: string;
  nameAr: string;
  nameEn: string;
  discountType: string;
  value: number;
  applicationPriority: number;
  isStackable: boolean;
  minInvoiceAmount?: number;
  minQuantity?: number;
  isHappyHour: boolean;
  happyHourFromTime?: string;
  happyHourToTime?: string;
  effectiveFromDate: string;
  effectiveToDate?: string;
  isActive: boolean;
}
