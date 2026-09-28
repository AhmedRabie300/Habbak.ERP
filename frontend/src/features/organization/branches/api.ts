import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface Branch {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
}

const BASE = '/organization/branches';

export function useBranchesList() {
  return useQuery({
    queryKey: ['branches'],
    queryFn: async () => (await api.get<Branch[]>(BASE)).data
  });
}

export function useCreateBranch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; nameAr: string; nameEn: string }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['branches'] });
      queryClient.invalidateQueries({ queryKey: ['dimension-values'] });
    }
  });
}

export function useUpdateBranch(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { nameAr: string; nameEn: string; isActive: boolean }) =>
      api.put(`${BASE}/${id}`, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['branches'] });
      queryClient.invalidateQueries({ queryKey: ['dimension-values'] });
    }
  });
}

export function useDeleteBranch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['branches'] });
      queryClient.invalidateQueries({ queryKey: ['dimension-values'] });
    }
  });
}
