import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface ItemGroup {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  parentId: number | null;
  isActive: boolean;
}

const BASE = '/inventory/item-groups';

export function useItemGroupsList() {
  return useQuery({
    queryKey: ['item-groups'],
    queryFn: async () => (await api.get<ItemGroup[]>(BASE)).data
  });
}

export function useCreateItemGroup() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; nameAr: string; nameEn: string; parentId: number | null }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['item-groups'] })
  });
}

export function useUpdateItemGroup(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { nameAr: string; nameEn: string; parentId: number | null; isActive: boolean }) =>
      api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['item-groups'] })
  });
}

export function useDeleteItemGroup() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['item-groups'] })
  });
}
