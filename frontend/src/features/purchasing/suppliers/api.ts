import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { SupplierDetail, SupplierFormValues, SupplierListItem } from './types';

const BASE = '/purchasing/suppliers';

export function useSuppliersList() {
  return useQuery({
    queryKey: ['suppliers'],
    queryFn: async () => (await api.get<SupplierListItem[]>(BASE)).data
  });
}

export function useSupplier(id: number | undefined) {
  return useQuery({
    queryKey: ['suppliers', id],
    queryFn: async () => (await api.get<SupplierDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateSupplier() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SupplierFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['suppliers'] })
  });
}

export function useUpdateSupplier(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SupplierFormValues) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['suppliers'] })
  });
}

export function useDeleteSupplier() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: (_data, id) => {
      // Remove the deleted supplier's own detail cache FIRST — invalidating ['suppliers'] alone
      // would still prefix-match ['suppliers', id] and refetch a 404 for the row that's gone.
      queryClient.removeQueries({ queryKey: ['suppliers', id] });
      queryClient.invalidateQueries({ queryKey: ['suppliers'] });
    }
  });
}
