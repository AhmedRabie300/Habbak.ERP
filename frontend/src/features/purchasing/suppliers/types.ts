export interface SupplierListItem {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  paymentTerms: string;
  currencyCode: string;
  isActive: boolean;
}

export interface SupplierDetail {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  taxNumber?: string;
  phone?: string;
  email?: string;
  address?: string;
  paymentTerms: string;
  creditLimit?: number;
  currencyCode: string;
  defaultWarehouseId?: number;
  payableAccountId?: number;
  expenseAccountId?: number;
  isActive: boolean;
}

export interface SupplierFormValues {
  code?: string;
  nameAr: string;
  nameEn: string;
  taxNumber?: string;
  phone?: string;
  email?: string;
  address?: string;
  paymentTerms: string;
  creditLimit?: number;
  currencyCode: string;
  defaultWarehouseId?: number;
  payableAccountId?: number;
  expenseAccountId?: number;
  isActive: boolean;
}
