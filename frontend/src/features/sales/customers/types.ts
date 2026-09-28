export interface CustomerListItem {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  customerType: string;
  creditLimit: number | null;
  loyaltyPointsBalance: number;
  isActive: boolean;
}

export interface CustomerDetail {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  branchId?: number;
  customerType: string;
  phone?: string;
  email?: string;
  address?: string;
  creditLimit: number | null;
  paymentTermDays: number;
  receivableAccountId?: number;
  loyaltyTierId?: number;
  loyaltyPointsBalance: number;
  isActive: boolean;
}

export interface CustomerFormValues {
  code?: string;
  branchId?: number;
  nameAr: string;
  nameEn: string;
  customerType: string;
  phone?: string;
  email?: string;
  address?: string;
  creditLimit: number | null;
  paymentTermDays: number;
  receivableAccountId?: number;
  loyaltyTierId?: number;
  isActive: boolean;
}
