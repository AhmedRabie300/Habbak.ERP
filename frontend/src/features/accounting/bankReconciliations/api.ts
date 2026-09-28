import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface BankReconciliationListItem {
  id: number;
  bankAccountId: number;
  periodFrom: string;
  periodTo: string;
  status: string;
}

export interface BankReconciliationLine {
  id: number;
  systemTransactionType?: string;
  systemTransactionId?: number;
  bankStatementLineId?: number;
  matchedAmount: number;
  isAutoMatched: boolean;
}

export interface BankReconciliationDetail extends BankReconciliationListItem {
  adjustmentJournalEntryId?: number;
  lines: BankReconciliationLine[];
}

const BASE = '/accounting/bank-reconciliations';

export function useBankReconciliationsList() {
  return useQuery({ queryKey: ['bank-reconciliations'], queryFn: async () => (await api.get<BankReconciliationListItem[]>(BASE)).data });
}

export function useBankReconciliation(id: number | undefined) {
  return useQuery({
    queryKey: ['bank-reconciliations', id],
    queryFn: async () => (await api.get<BankReconciliationDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateBankReconciliationRun() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { bankAccountId: number; periodFrom: string; periodTo: string }) =>
      (await api.post<number>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['bank-reconciliations'] })
  });
}

export function useAddBankReconciliationLine(runId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      systemTransactionType?: string; systemTransactionId?: number; bankStatementLineId?: number;
      matchedAmount: number; isAutoMatched: boolean;
    }) => (await api.post<{ id: number }>(`${BASE}/${runId}/lines`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['bank-reconciliations', runId] })
  });
}

export function useCompleteBankReconciliation(runId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { adjustmentAmount: number; adjustmentAccountId?: number }) =>
      api.post(`${BASE}/${runId}/complete`, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['bank-reconciliations'] });
      queryClient.invalidateQueries({ queryKey: ['bank-reconciliations', runId] });
    }
  });
}
