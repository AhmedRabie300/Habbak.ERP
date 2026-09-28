import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export type WarehouseType = 'Main' | 'BranchMaterials' | 'Production' | 'DamagedReturns' | 'FinishedGoods';

export interface Warehouse {
  id: number;
  branchId: number | null;
  code: string;
  nameAr: string;
  nameEn: string;
  warehouseType: WarehouseType;
  allowNegativeBalance: boolean;
  isActive: boolean;
}

const BASE = '/inventory/warehouses';

export function useWarehousesList() {
  return useQuery({
    queryKey: ['warehouses'],
    queryFn: async () => (await api.get<Warehouse[]>(BASE)).data
  });
}

export function useCreateWarehouse() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; nameAr: string; nameEn: string; warehouseType: WarehouseType; branchId: number | null; allowNegativeBalance: boolean }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['warehouses'] })
  });
}

export function useUpdateWarehouse(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { nameAr: string; nameEn: string; warehouseType: WarehouseType; branchId: number | null; allowNegativeBalance: boolean; isActive: boolean }) =>
      api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['warehouses'] })
  });
}

export function useDeleteWarehouse() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['warehouses'] })
  });
}
