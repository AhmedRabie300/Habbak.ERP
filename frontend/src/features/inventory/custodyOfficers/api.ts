import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface CustodyOfficer {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  branchId: number | null;
  isActive: boolean;
}

const BASE = '/inventory/custody-officers';

export function useCustodyOfficersList() {
  return useQuery({
    queryKey: ['custody-officers'],
    queryFn: async () => (await api.get<CustodyOfficer[]>(BASE)).data
  });
}

export function useCreateCustodyOfficer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; nameAr: string; nameEn: string; branchId: number | null }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['custody-officers'] })
  });
}

export function useUpdateCustodyOfficer(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { nameAr: string; nameEn: string; branchId: number | null; isActive: boolean }) =>
      api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['custody-officers'] })
  });
}

export function useDeleteCustodyOfficer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['custody-officers'] })
  });
}
