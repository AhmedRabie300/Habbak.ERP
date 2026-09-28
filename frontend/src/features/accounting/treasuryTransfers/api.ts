import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { TreasuryTransferDetail, TreasuryTransferFormValues, TreasuryTransferListItem } from './types';

const BASE = '/accounting/treasury-transfers';

export function useTreasuryTransfersList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['treasury-transfers', params],
    queryFn: async () => (await api.get<PagedResult<TreasuryTransferListItem>>(BASE, { params })).data
  });
}

export function useTreasuryTransfer(id: number | undefined) {
  return useQuery({
    queryKey: ['treasury-transfers', id],
    queryFn: async () => (await api.get<TreasuryTransferDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateTreasuryTransfer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: TreasuryTransferFormValues) => (await api.post<number>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['treasury-transfers'] })
  });
}

export function useUpdateTreasuryTransfer(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: TreasuryTransferFormValues & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['treasury-transfers'] })
  });
}

export function usePostTreasuryTransfer(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => (await api.post<{ status: string; approvalInstanceId?: number }>(`${BASE}/${id}/post`)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['treasury-transfers'] })
  });
}
