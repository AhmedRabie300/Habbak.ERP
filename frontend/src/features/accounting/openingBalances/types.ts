export interface AccountOpeningBalanceBatchListItem {
  id: number;
  batchNumber: string;
  transactionDate: string;
  lineCount: number;
  totalAmount: number;
  status: string;
}

export interface AccountOpeningBalanceLine {
  id?: number;
  accountId: number;
  accountCode: string;
  accountNameAr: string;
  accountNature: string;
  amount: number;
  notes?: string;
}

export interface AccountOpeningBalanceBatchDetail {
  id: number;
  batchNumber: string;
  transactionDate: string;
  status: string;
  journalEntryId?: number;
  notes?: string;
  rowVersion: string;
  lines: AccountOpeningBalanceLine[];
}

export interface AccountOpeningBalanceLineInput {
  accountId: number;
  amount: number;
  notes?: string;
  /** Frontend-only — carried alongside the input so the live debit/credit totals can be
   * computed without waiting on a server round-trip; never sent to the API. */
  accountNature?: string;
}

export interface AccountOpeningBalanceBatchFormValues {
  transactionDate: string;
  notes?: string;
  lines: AccountOpeningBalanceLineInput[];
}
