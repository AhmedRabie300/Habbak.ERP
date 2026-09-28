import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface PaymentMethod {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
}

const BASE = '/accounting/payment-methods';

export function usePaymentMethodsList() {
  return useQuery({
    queryKey: ['payment-methods'],
    queryFn: async () => (await api.get<PaymentMethod[]>(BASE)).data
  });
}

export function useCreatePaymentMethod() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; nameAr: string; nameEn: string }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['payment-methods'] })
  });
}

export function useUpdatePaymentMethod(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { nameAr: string; nameEn: string; isActive: boolean }) =>
      api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['payment-methods'] })
  });
}

export function useDeletePaymentMethod() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['payment-methods'] })
  });
}
