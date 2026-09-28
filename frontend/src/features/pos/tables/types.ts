export type TableStatus = 'Free' | 'Busy' | 'Reserved' | 'Cleaning';

export interface Table {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  branchId: number;
  status: TableStatus;
  isActive: boolean;
}

export interface TableBoardItem {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  status: TableStatus;
  openCheckId?: number;
  openCheckCode?: string;
  openCheckStatus?: string;
  openCheckTotal?: number;
}
