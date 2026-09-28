import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { POSReturnDetail, POSReturnLineInput, POSReturnListItem } from './types';

const BASE = '/pos/returns';

export function usePOSReturnsList() {
  return useQuery({
    queryKey: ['pos-returns'],
    queryFn: async () => (await api.get<POSReturnListItem[]>(BASE)).data
  });
}

export function usePOSReturn(id: number | undefined) {
  return useQuery({
    queryKey: ['pos-returns', id],
    queryFn: async () => (await api.get<POSReturnDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreatePOSReturn() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { shiftId: number; sourceInvoiceId: number; returnDate: string; reason: string; lines: POSReturnLineInput[] }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-returns'] })
  });
}
