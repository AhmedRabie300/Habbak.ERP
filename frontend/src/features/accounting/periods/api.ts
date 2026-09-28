import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface PeriodListItem {
  id: number;
  periodStart: string;
  periodEnd: string;
  status: string;
}

export interface ChecklistItemResult {
  itemKey: string;
  label: string;
  isSatisfied: boolean;
  detail: string;
}

export interface PeriodDetail extends PeriodListItem {
  closedByUserId?: number;
  closedAtUtc?: string;
  checklist: ChecklistItemResult[];
  canClose: boolean;
}

const BASE = '/accounting/accounting-periods';

export function usePeriodsList() {
  return useQuery({
    queryKey: ['periods'],
    queryFn: async () => (await api.get<PeriodListItem[]>(BASE)).data
  });
}

export function usePeriod(id: number | undefined) {
  return useQuery({
    queryKey: ['periods', id],
    queryFn: async () => (await api.get<PeriodDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined,
    refetchInterval: 5000
  });
}

export function useCreatePeriod() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { periodStart: string; periodEnd: string }) =>
      (await api.post<number>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['periods'] })
  });
}

export function useClosePeriod(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/close`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['periods'] })
  });
}

export function useReopenPeriod(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/reopen`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['periods'] })
  });
}
