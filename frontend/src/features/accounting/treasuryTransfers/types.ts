export interface TreasuryTransferListItem {
  id: number;
  fromTreasuryAccountId: number;
  toTreasuryAccountId: number;
  amount: number;
  transferDate: string;
  status: string;
}

export interface TreasuryTransferDetail {
  id: number;
  branchId?: number;
  fromTreasuryAccountId: number;
  toTreasuryAccountId: number;
  amount: number;
  transferDate: string;
  status: string;
  notes?: string;
  journalEntryId?: number;
  rowVersion: string;
}

export interface TreasuryTransferFormValues {
  branchId?: number;
  fromTreasuryAccountId: number;
  toTreasuryAccountId: number;
  amount: number;
  transferDate: string;
  notes?: string;
}
