import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface POSCategory {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  displayOrder: number;
  isActive: boolean;
}

const BASE = '/inventory/pos-categories';

export function usePOSCategoriesList() {
  return useQuery({
    queryKey: ['pos-categories'],
    queryFn: async () => (await api.get<POSCategory[]>(BASE)).data
  });
}

export function useCreatePOSCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; nameAr: string; nameEn: string; displayOrder: number }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-categories'] })
  });
}

export function useUpdatePOSCategory(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { nameAr: string; nameEn: string; displayOrder: number; isActive: boolean }) =>
      api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-categories'] })
  });
}

export function useDeletePOSCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-categories'] })
  });
}
