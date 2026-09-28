export interface POSTerminal {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  branchId: number;
  defaultWarehouseId?: number;
  isActive: boolean;
}

export interface POSTerminalFormValues {
  code?: string;
  nameAr: string;
  nameEn: string;
  branchId: number;
  defaultWarehouseId?: number;
  isActive: boolean;
}
