export interface SupplierContractListItem {
  id: number;
  contractNumber: string;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  startDate: string;
  endDate: string;
  itemCount: number;
  status: string;
}

export interface ContractItem {
  id?: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  unitPrice: number;
  minQuantity?: number;
  maxQuantity?: number;
  discountPercentage?: number;
}

export interface SupplierContractDetail {
  id: number;
  contractNumber: string;
  supplierId: number;
  supplierCode: string;
  supplierNameAr: string;
  startDate: string;
  endDate: string;
  autoRenew: boolean;
  status: string;
  notes?: string;
  rowVersion: string;
  items: ContractItem[];
}

export interface ContractItemInput {
  itemId: number;
  unitPrice: number;
  minQuantity?: number;
  maxQuantity?: number;
  discountPercentage?: number;
}

export interface SupplierContractFormValues {
  supplierId: number;
  startDate: string;
  endDate: string;
  autoRenew: boolean;
  notes?: string;
  items: ContractItemInput[];
}
