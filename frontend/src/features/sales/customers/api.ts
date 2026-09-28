import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { CustomerDetail, CustomerFormValues, CustomerListItem } from './types';

const BASE = '/sales/customers';

export function useCustomersList() {
  return useQuery({
    queryKey: ['customers'],
    queryFn: async () => (await api.get<CustomerListItem[]>(BASE)).data
  });
}

export function useCustomer(id: number | undefined) {
  return useQuery({
    queryKey: ['customers', id],
    queryFn: async () => (await api.get<CustomerDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateCustomer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CustomerFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['customers'] })
  });
}

export function useUpdateCustomer(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CustomerFormValues) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['customers'] })
  });
}

export function useDeleteCustomer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: (_data, id) => {
      queryClient.removeQueries({ queryKey: ['customers', id] });
      queryClient.invalidateQueries({ queryKey: ['customers'] });
    }
  });
}
