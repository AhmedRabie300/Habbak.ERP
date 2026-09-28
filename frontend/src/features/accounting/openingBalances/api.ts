import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { AccountOpeningBalanceBatchDetail, AccountOpeningBalanceBatchFormValues, AccountOpeningBalanceBatchListItem } from './types';

const BASE = '/accounting/opening-balances';

export function useAccountOpeningBalanceBatchesList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['account-opening-balances', params],
    queryFn: async () => (await api.get<PagedResult<AccountOpeningBalanceBatchListItem>>(BASE, { params })).data
  });
}

export function useAccountOpeningBalanceBatch(id: number | undefined) {
  return useQuery({
    queryKey: ['account-opening-balances', id],
    queryFn: async () => (await api.get<AccountOpeningBalanceBatchDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateAccountOpeningBalanceBatch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: AccountOpeningBalanceBatchFormValues) => {
      const lines = payload.lines.map((l) => ({ accountId: l.accountId, amount: l.amount, notes: l.notes }));
      return (await api.post<{ id: number }>(BASE, { ...payload, lines })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['account-opening-balances'] })
  });
}

export function useUpdateAccountOpeningBalanceBatch(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: AccountOpeningBalanceBatchFormValues & { rowVersion: string }) => {
      const lines = payload.lines.map((l) => ({ accountId: l.accountId, amount: l.amount, notes: l.notes }));
      await api.put(`${BASE}/${id}`, { ...payload, lines });
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['account-opening-balances'] })
  });
}

export function usePostAccountOpeningBalanceBatch(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/post`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['account-opening-balances'] })
  });
}

export function useCancelAccountOpeningBalanceBatch(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['account-opening-balances'] })
  });
}
