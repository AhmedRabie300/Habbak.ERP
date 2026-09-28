import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { DiscountDetail, DiscountFormValues, DiscountListItem } from './types';

const BASE = '/sales/discounts';

export function useDiscountsList() {
  return useQuery({
    queryKey: ['discounts'],
    queryFn: async () => (await api.get<DiscountListItem[]>(BASE)).data
  });
}

export function useDiscount(id: number | undefined) {
  return useQuery({
    queryKey: ['discounts', id],
    queryFn: async () => (await api.get<DiscountDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateDiscount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: DiscountFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['discounts'] })
  });
}

export function useUpdateDiscount(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: DiscountFormValues) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['discounts'] })
  });
}

export function useDeleteDiscount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: (_data, id) => {
      queryClient.removeQueries({ queryKey: ['discounts', id] });
      queryClient.invalidateQueries({ queryKey: ['discounts'] });
    }
  });
}
