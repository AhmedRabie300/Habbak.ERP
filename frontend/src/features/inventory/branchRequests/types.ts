export interface BranchRequestListItem {
  id: number;
  requestNumber: string;
  requestDate: string;
  branchId?: number;
  lineCount: number;
  status: string;
}

export interface BranchRequestLine {
  id?: number;
  itemId: number;
  itemCode: string;
  itemNameAr: string;
  requestedQuantity: number;
  approvedQuantity?: number;
  /** The unit both quantities are in. */
  unitId: number;
  unitCode?: string;
  unitNameAr?: string;
  unitFactor: number;
  minRequestQuantity?: number;
  maxRequestQuantity?: number;
}

export interface BranchRequestDetail {
  id: number;
  branchId?: number;
  requestNumber: string;
  requestDate: string;
  requestedByUserId: number;
  status: string;
  rowVersion: string;
  lines: BranchRequestLine[];
}

export interface BranchRequestLineInput {
  itemId: number;
  requestedQuantity: number;
  /** The item's base unit when left out. */
  unitId?: number;
}

export interface BranchRequestFormValues {
  branchId: number;
  requestDate: string;
  lines: BranchRequestLineInput[];
}

export interface ApproveBranchRequestLineInput {
  lineId: number;
  approvedQuantity: number;
}

export interface ApproveBranchRequestFormValues {
  sourceWarehouseId: number;
  custodyOfficerId: number;
  transferDocumentDate: string;
  lines: ApproveBranchRequestLineInput[];
}
