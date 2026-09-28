import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { DenominationCountInput, ShiftDetail, ShiftListItem } from './types';

const BASE = '/pos/shifts';

export function useShiftsList(posTerminalId?: number, status?: string) {
  return useQuery({
    queryKey: ['pos-shifts', posTerminalId, status],
    queryFn: async () => (await api.get<ShiftListItem[]>(BASE, { params: { posTerminalId, status } })).data
  });
}

export function useShift(id: number | undefined) {
  return useQuery({
    queryKey: ['pos-shifts', 'detail', id],
    queryFn: async () => (await api.get<ShiftDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useOpenShiftForTerminal(posTerminalId: number | undefined) {
  return useQuery({
    queryKey: ['pos-shifts', 'open-for-terminal', posTerminalId],
    queryFn: async () => (await api.get<ShiftDetail | null>(`${BASE}/open-for-terminal/${posTerminalId}`)).data,
    enabled: posTerminalId !== undefined
  });
}

export function useOpenShift() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { posTerminalId: number; cashierUserId?: number; openingCounts: DenominationCountInput[] }) =>
      (await api.post<{ id: number }>(`${BASE}/open`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-shifts'] })
  });
}

export function useCloseShift(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { rowVersion: string; closingCounts: DenominationCountInput[] }) =>
      api.post(`${BASE}/${id}/close`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-shifts'] })
  });
}

export function useApproveShiftClose(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/approve-close`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-shifts'] })
  });
}
