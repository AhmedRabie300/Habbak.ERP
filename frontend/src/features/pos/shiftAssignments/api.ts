import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ShiftAssignment } from './types';

const BASE = '/pos/shift-assignments';

export function useShiftAssignmentsList(posTerminalId?: number) {
  return useQuery({
    queryKey: ['shift-assignments', posTerminalId],
    queryFn: async () => (await api.get<ShiftAssignment[]>(BASE, { params: posTerminalId ? { posTerminalId } : undefined })).data
  });
}

export function useCreateShiftAssignment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { posTerminalId: number; userId: number; assignedDate: string }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['shift-assignments'] })
  });
}

export function useDeleteShiftAssignment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['shift-assignments'] })
  });
}
