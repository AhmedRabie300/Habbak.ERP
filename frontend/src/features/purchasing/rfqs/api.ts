import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { RFQDetail, RFQFormValues, RFQListItem, SetRFQSupplierQuoteInput } from './types';

const BASE = '/purchasing/rfqs';

export function useRFQsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['rfqs', params],
    queryFn: async () => (await api.get<PagedResult<RFQListItem>>(BASE, { params })).data
  });
}

export function useRFQ(id: number | undefined) {
  return useQuery({
    queryKey: ['rfqs', id],
    queryFn: async () => (await api.get<RFQDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateRFQ() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: RFQFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rfqs'] })
  });
}

export function useUpdateRFQ(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: RFQFormValues & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rfqs'] })
  });
}

export function useSendRFQ(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/send`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rfqs'] })
  });
}

export function useSetRFQSupplierQuote(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SetRFQSupplierQuoteInput) => (await api.post<{ id: number }>(`${BASE}/${id}/quotes`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rfqs', id] })
  });
}

export function useDeclineRFQSupplier(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (rfqSupplierId: number) => api.post(`${BASE}/${id}/suppliers/${rfqSupplierId}/decline`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rfqs', id] })
  });
}

export function useSelectRFQSupplierQuote(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (quoteId: number) => api.post(`${BASE}/${id}/quotes/${quoteId}/select`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rfqs', id] })
  });
}

export function useAwardRFQ(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/award`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rfqs'] })
  });
}

export function useCancelRFQ(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['rfqs'] })
  });
}
