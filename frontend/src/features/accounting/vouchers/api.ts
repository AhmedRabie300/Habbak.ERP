import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { VoucherDetail, VoucherFormValues, VoucherKind, VoucherListItem } from './types';

export function useVouchersList(kind: VoucherKind, params: ListQueryParams) {
  return useQuery({
    queryKey: [kind, params],
    queryFn: async () => {
      const { data } = await api.get<PagedResult<VoucherListItem>>(`/accounting/${kind}`, { params });
      return data;
    }
  });
}

export function useVoucher(kind: VoucherKind, id: number | undefined) {
  return useQuery({
    queryKey: [kind, id],
    queryFn: async () => {
      const { data } = await api.get<VoucherDetail>(`/accounting/${kind}/${id}`);
      return data;
    },
    enabled: id !== undefined
  });
}

export function useCreateVoucher(kind: VoucherKind) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: VoucherFormValues) => {
      const { data } = await api.post<number>(`/accounting/${kind}`, payload);
      return data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [kind] })
  });
}

export function useUpdateVoucher(kind: VoucherKind, id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: VoucherFormValues & { rowVersion: string }) => {
      await api.put(`/accounting/${kind}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [kind] })
  });
}

export function usePostVoucher(kind: VoucherKind, id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const { data } = await api.post<{ status: string; approvalInstanceId?: number }>(`/accounting/${kind}/${id}/post`);
      return data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [kind] })
  });
}

export function useCancelVoucher(kind: VoucherKind, id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      await api.post(`/accounting/${kind}/${id}/cancel`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [kind] })
  });
}

export function useReverseVoucher(kind: VoucherKind, id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const { data } = await api.post<{ reversalJournalEntryId: number }>(`/accounting/${kind}/${id}/reverse`);
      return data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [kind] })
  });
}
