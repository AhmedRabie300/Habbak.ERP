import { useQuery } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface TrialBalanceLine {
  accountId: number;
  code: string;
  nameAr: string;
  nameEn: string;
  debit: number;
  credit: number;
}

export interface AccountStatementLine {
  date: string;
  entryNumber: string;
  description: string;
  debit: number;
  credit: number;
  runningBalance: number;
}

export interface AccountStatement {
  openingBalance: number;
  closingBalance: number;
  lines: AccountStatementLine[];
}

export interface IncomeStatementLine {
  accountId: number;
  code: string;
  name: string;
  amount: number;
}

export interface IncomeStatement {
  revenue: IncomeStatementLine[];
  expenses: IncomeStatementLine[];
  totalRevenue: number;
  totalExpenses: number;
  netIncome: number;
}

export interface BalanceSheet {
  assets: IncomeStatementLine[];
  liabilities: IncomeStatementLine[];
  equity: IncomeStatementLine[];
  totalAssets: number;
  totalLiabilities: number;
  totalEquity: number;
}

export interface CashFlow {
  openingBalance: number;
  totalDebit: number;
  totalCredit: number;
  closingBalance: number;
}

export interface TreasuryPositionLine {
  accountId: number;
  code: string;
  nameAr: string;
  nameEn: string;
  balance: number;
}

export interface CostCenterExpenseLine {
  dimensionValueId: number;
  nameAr: string;
  nameEn: string;
  amount: number;
}

export interface JournalBookLine {
  accountId: number;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
}

export interface JournalBookEntry {
  id: number;
  entryNumber: string;
  entryDate: string;
  description: string;
  status: string;
  totalDebit: number;
  totalCredit: number;
  lines: JournalBookLine[];
}

export interface TreasuryAccountStatement {
  accountId: number;
  code: string;
  nameAr: string;
  nameEn: string;
  openingBalance: number;
  closingBalance: number;
  lines: AccountStatementLine[];
}

export interface CustodyReportSettlementLine {
  accountId: number;
  accountCode: string;
  accountName: string;
  amount: number;
  description: string | null;
}

export interface CustodyReportSettlement {
  settlementDate: string;
  totalAmount: number;
  lines: CustodyReportSettlementLine[];
}

export interface CustodyReportLine {
  id: number;
  employeeId: number;
  branchId: number | null;
  amount: number;
  issueDate: string;
  status: string;
  totalSettled: number;
  settlements: CustodyReportSettlement[];
}

export interface BankReconciliationReportLine {
  runId: number;
  periodFrom: string;
  periodTo: string;
  runStatus: string;
  systemTransactionType: string | null;
  systemTransactionId: number | null;
  bankStatementLineId: number | null;
  matchedAmount: number;
  isAutoMatched: boolean;
}

const BASE = '/accounting/reports';

export function useTrialBalance(asOf: string, enabled: boolean) {
  return useQuery({
    queryKey: ['reports', 'trial-balance', asOf],
    queryFn: async () => (await api.get<TrialBalanceLine[]>(`${BASE}/trial-balance`, { params: { asOf } })).data,
    enabled
  });
}

export function useAccountStatement(accountId: number, from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['reports', 'account-statement', accountId, from, to],
    queryFn: async () => (await api.get<AccountStatement>(`${BASE}/account-statement`, { params: { accountId, from, to } })).data,
    enabled
  });
}

export function useIncomeStatement(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['reports', 'income-statement', from, to],
    queryFn: async () => (await api.get<IncomeStatement>(`${BASE}/income-statement`, { params: { from, to } })).data,
    enabled
  });
}

export function useBalanceSheet(asOf: string, enabled: boolean) {
  return useQuery({
    queryKey: ['reports', 'balance-sheet', asOf],
    queryFn: async () => (await api.get<BalanceSheet>(`${BASE}/balance-sheet`, { params: { asOf } })).data,
    enabled
  });
}

export function useCashFlow(accountIds: number[], from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['reports', 'cash-flow', accountIds, from, to],
    queryFn: async () => (await api.get<CashFlow>(`${BASE}/cash-flow`, { params: { accountIds, from, to } })).data,
    enabled
  });
}

export function useTreasuryPosition(asOf: string, enabled: boolean) {
  return useQuery({
    queryKey: ['reports', 'treasury-position', asOf],
    queryFn: async () => (await api.get<TreasuryPositionLine[]>(`${BASE}/treasury-position`, { params: { asOf } })).data,
    enabled
  });
}

export function useCostCenterStatement(dimensionValueId: number, from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['reports', 'cost-center-statement', dimensionValueId, from, to],
    queryFn: async () => (await api.get<AccountStatement>(`${BASE}/cost-center-statement`, { params: { dimensionValueId, from, to } })).data,
    enabled
  });
}

export function useExpensesByCostCenter(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['reports', 'expenses-by-cost-center', from, to],
    queryFn: async () => (await api.get<CostCenterExpenseLine[]>(`${BASE}/expenses-by-cost-center`, { params: { from, to } })).data,
    enabled
  });
}

export function useJournalBook(from: string, to: string, status: string | undefined, enabled: boolean) {
  return useQuery({
    queryKey: ['reports', 'journal-book', from, to, status],
    queryFn: async () => (await api.get<JournalBookEntry[]>(`${BASE}/journal-book`, { params: { from, to, status } })).data,
    enabled
  });
}

export function useTreasuryBankStatement(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['reports', 'treasury-bank-statement', from, to],
    queryFn: async () => (await api.get<TreasuryAccountStatement[]>(`${BASE}/treasury-bank-statement`, { params: { from, to } })).data,
    enabled
  });
}

export function useCustodyReport(enabled: boolean) {
  return useQuery({
    queryKey: ['reports', 'custody-report'],
    queryFn: async () => (await api.get<CustodyReportLine[]>(`${BASE}/custody-report`)).data,
    enabled
  });
}

export function useBankReconciliationReport(bankAccountId: number, from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['reports', 'bank-reconciliation-report', bankAccountId, from, to],
    queryFn: async () => (await api.get<BankReconciliationReportLine[]>(`${BASE}/bank-reconciliation-report`, { params: { bankAccountId, from, to } })).data,
    enabled
  });
}
