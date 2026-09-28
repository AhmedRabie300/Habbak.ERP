import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export type UnitCategory = 'Weight' | 'Volume' | 'Count';

export interface UnitOfMeasure {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  category: UnitCategory;
  isActive: boolean;
}

const BASE = '/inventory/units-of-measure';

export function useUnitsOfMeasureList() {
  return useQuery({
    queryKey: ['units-of-measure'],
    queryFn: async () => (await api.get<UnitOfMeasure[]>(BASE)).data
  });
}

export function useCreateUnitOfMeasure() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; nameAr: string; nameEn: string; category: UnitCategory }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['units-of-measure'] })
  });
}

export function useUpdateUnitOfMeasure(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { nameAr: string; nameEn: string; category: UnitCategory; isActive: boolean }) =>
      api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['units-of-measure'] })
  });
}

export function useDeleteUnitOfMeasure() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['units-of-measure'] })
  });
}
