import { useQuery } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type {
  AdditionalCostAllocationReport, ExpiredRFQQuoteRow, ExpiringSupplierContractRow, OpenPurchaseOrderRow, PurchaseExpenseReportRow,
  PurchaseExpenseTypeSummary, PurchaseReturnReportRow, PurchasesByItemRow, PurchasesBySupplierRow, SupplierEvaluationRankingRow,
  UnpaidPurchaseInvoiceRow
} from './types';

const BASE = '/purchasing/reports';

export function usePurchasesBySupplierReport(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['purchasing-reports', 'purchases-by-supplier', from, to],
    queryFn: async () => (await api.get<PurchasesBySupplierRow[]>(`${BASE}/purchases-by-supplier`, { params: { from, to } })).data,
    enabled
  });
}

export function usePurchasesByItemReport(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['purchasing-reports', 'purchases-by-item', from, to],
    queryFn: async () => (await api.get<PurchasesByItemRow[]>(`${BASE}/purchases-by-item`, { params: { from, to } })).data,
    enabled
  });
}

export function useOpenPurchaseOrdersReport(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['purchasing-reports', 'open-purchase-orders', from, to],
    queryFn: async () => (await api.get<OpenPurchaseOrderRow[]>(`${BASE}/open-purchase-orders`, { params: { from, to } })).data,
    enabled
  });
}

export function useUnpaidInvoicesReport(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['purchasing-reports', 'unpaid-invoices', from, to],
    queryFn: async () => (await api.get<UnpaidPurchaseInvoiceRow[]>(`${BASE}/unpaid-invoices`, { params: { from, to } })).data,
    enabled
  });
}

export function usePurchaseReturnsReport(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['purchasing-reports', 'purchase-returns', from, to],
    queryFn: async () => (await api.get<PurchaseReturnReportRow[]>(`${BASE}/purchase-returns`, { params: { from, to } })).data,
    enabled
  });
}

export function useSupplierEvaluationRankingReport(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['purchasing-reports', 'supplier-evaluation-ranking', from, to],
    queryFn: async () => (await api.get<SupplierEvaluationRankingRow[]>(`${BASE}/supplier-evaluation-ranking`, { params: { from, to } })).data,
    enabled
  });
}

export function usePurchaseExpensesReport(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['purchasing-reports', 'purchase-expenses', from, to],
    queryFn: async () => (await api.get<PurchaseExpenseReportRow[]>(`${BASE}/purchase-expenses`, { params: { from, to } })).data,
    enabled
  });
}

export function useExpiringContractsReport(withinDays: number) {
  return useQuery({
    queryKey: ['purchasing-reports', 'expiring-contracts', withinDays],
    queryFn: async () => (await api.get<ExpiringSupplierContractRow[]>(`${BASE}/expiring-contracts`, { params: { withinDays } })).data
  });
}

export function useAdditionalCostAllocationReport(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['purchasing-reports', 'additional-cost-allocation', from, to],
    queryFn: async () => (await api.get<AdditionalCostAllocationReport>(`${BASE}/additional-cost-allocation`, { params: { from, to } })).data,
    enabled
  });
}

export function usePurchaseExpensesByTypeReport(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['purchasing-reports', 'purchase-expenses-by-type', from, to],
    queryFn: async () => (await api.get<PurchaseExpenseTypeSummary[]>(`${BASE}/purchase-expenses-by-type`, { params: { from, to } })).data,
    enabled
  });
}

export function useExpiredQuotesReport(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['purchasing-reports', 'expired-quotes', from, to],
    queryFn: async () => (await api.get<ExpiredRFQQuoteRow[]>(`${BASE}/expired-quotes`, { params: { from, to } })).data,
    enabled
  });
}
