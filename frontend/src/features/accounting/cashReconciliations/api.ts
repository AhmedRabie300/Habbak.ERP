import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface CashReconciliationListItem {
  id: number;
  treasuryAccountId: number;
  reconciliationDate: string;
  expectedBalance: number;
  actualBalance: number;
  differenceAmount: number;
  isApproved: boolean;
}

export interface CashReconciliationDetail extends CashReconciliationListItem {
  differenceReason?: string;
  approvedByUserId?: number;
  approvedAtUtc?: string;
}

const BASE = '/accounting/cash-reconciliations';

export function useCashReconciliationsList() {
  return useQuery({ queryKey: ['cash-reconciliations'], queryFn: async () => (await api.get<CashReconciliationListItem[]>(BASE)).data });
}

export function useCashReconciliation(id: number | undefined) {
  return useQuery({
    queryKey: ['cash-reconciliations', id],
    queryFn: async () => (await api.get<CashReconciliationDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateCashReconciliation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      treasuryAccountId: number; branchId?: number; reconciliationDate: string; actualBalance: number; differenceReason?: string;
    }) => (await api.post<number>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['cash-reconciliations'] })
  });
}

export function useApproveCashReconciliation(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/approve`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['cash-reconciliations'] });
      queryClient.invalidateQueries({ queryKey: ['cash-reconciliations', id] });
    }
  });
}
