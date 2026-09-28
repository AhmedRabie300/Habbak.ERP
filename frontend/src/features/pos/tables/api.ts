import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { Table, TableBoardItem, TableStatus } from './types';

const BASE = '/pos/tables';

export function useTablesList() {
  return useQuery({
    queryKey: ['pos-tables'],
    queryFn: async () => (await api.get<Table[]>(BASE)).data
  });
}

export function useTableBoard(branchId: number | undefined) {
  return useQuery({
    queryKey: ['pos-tables', 'board', branchId],
    queryFn: async () => (await api.get<TableBoardItem[]>(`${BASE}/board/${branchId}`)).data,
    enabled: branchId !== undefined,
    refetchInterval: 5000
  });
}

export function useCreateTable() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; nameAr: string; nameEn: string; branchId: number }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pos-tables'] });
    }
  });
}

export function useUpdateTable(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { nameAr: string; nameEn: string; branchId: number; isActive: boolean }) =>
      api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-tables'] })
  });
}

export function useSetTableStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, status }: { id: number; status: TableStatus }) =>
      api.post(`${BASE}/${id}/status`, { status }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-tables'] })
  });
}

export function useDeleteTable() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-tables'] })
  });
}
