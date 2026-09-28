import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { PriceListDetail, PriceListFormValues, PriceListListItem } from './types';

const BASE = '/sales/price-lists';

export function usePriceListsList() {
  return useQuery({
    queryKey: ['price-lists'],
    queryFn: async () => (await api.get<PriceListListItem[]>(BASE)).data
  });
}

export function usePriceList(id: number | undefined) {
  return useQuery({
    queryKey: ['price-lists', id],
    queryFn: async () => (await api.get<PriceListDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreatePriceList() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: PriceListFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['price-lists'] })
  });
}

export function useUpdatePriceList(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: PriceListFormValues) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['price-lists'] })
  });
}

export function useDeletePriceList() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: (_data, id) => {
      queryClient.removeQueries({ queryKey: ['price-lists', id] });
      queryClient.invalidateQueries({ queryKey: ['price-lists'] });
    }
  });
}
