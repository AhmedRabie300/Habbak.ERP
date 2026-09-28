export interface PurchaseRequestListItem {
  id: number;
  requestNumber: string;
  requestDate: string;
  branchId?: number;
  priority: string;
  lineCount: number;
  status: string;
}

export interface PurchaseRequestLine {
  id?: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  quantity: number;
  unitId: number;
  unitCode: string;
  notes?: string;
}

export interface PurchaseRequestDetail {
  id: number;
  requestNumber: string;
  requestDate: string;
  branchId?: number;
  requestedByUserId: number;
  priority: string;
  reason?: string;
  status: string;
  notes?: string;
  rowVersion: string;
  lines: PurchaseRequestLine[];
}

export interface PurchaseRequestLineInput {
  itemId: number;
  quantity: number;
  /** The item's base unit when left out. */
  unitId?: number;
  notes?: string;
}

export interface PurchaseRequestFormValues {
  branchId: number;
  requestDate: string;
  priority: string;
  reason?: string;
  notes?: string;
  lines: PurchaseRequestLineInput[];
}
