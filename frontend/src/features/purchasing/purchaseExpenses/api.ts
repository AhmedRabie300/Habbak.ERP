import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { PurchaseExpenseDetail, PurchaseExpenseFormValues, PurchaseExpenseListItem } from './types';

const BASE = '/purchasing/purchase-expenses';

export function usePurchaseExpensesList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['purchase-expenses', params],
    queryFn: async () => (await api.get<PagedResult<PurchaseExpenseListItem>>(BASE, { params })).data
  });
}

export function usePurchaseExpense(id: number | undefined) {
  return useQuery({
    queryKey: ['purchase-expenses', id],
    queryFn: async () => (await api.get<PurchaseExpenseDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreatePurchaseExpense() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: PurchaseExpenseFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-expenses'] })
  });
}

export function useUpdatePurchaseExpense(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: Omit<PurchaseExpenseFormValues, 'purchaseInvoiceId'> & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-expenses'] })
  });
}

export function useDeletePurchaseExpense() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['purchase-expenses'] })
  });
}
