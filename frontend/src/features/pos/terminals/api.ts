import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { POSTerminal, POSTerminalFormValues } from './types';

const BASE = '/pos/terminals';

export function usePOSTerminalsList() {
  return useQuery({
    queryKey: ['pos-terminals'],
    queryFn: async () => (await api.get<POSTerminal[]>(BASE)).data
  });
}

export function usePOSTerminal(id: number | undefined) {
  return useQuery({
    queryKey: ['pos-terminals', id],
    queryFn: async () => (await api.get<POSTerminal>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreatePOSTerminal() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: POSTerminalFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-terminals'] })
  });
}

export function useUpdatePOSTerminal(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: POSTerminalFormValues) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-terminals'] })
  });
}

export function useDeletePOSTerminal() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: (_data, id) => {
      queryClient.removeQueries({ queryKey: ['pos-terminals', id] });
      queryClient.invalidateQueries({ queryKey: ['pos-terminals'] });
    }
  });
}
