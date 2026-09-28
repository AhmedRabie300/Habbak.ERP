import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { DrawerMovement, DrawerMovementType } from './types';

const BASE = '/pos/drawer-movements';

export function useDrawerMovementsList(shiftId: number | undefined) {
  return useQuery({
    queryKey: ['drawer-movements', shiftId],
    queryFn: async () => (await api.get<DrawerMovement[]>(BASE, { params: { shiftId } })).data,
    enabled: shiftId !== undefined
  });
}

export function useCreateDrawerMovement() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { shiftId: number; movementType: DrawerMovementType; amount: number; reason?: string }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['drawer-movements'] })
  });
}

export function useApproveDrawerMovement() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.post(`${BASE}/${id}/approve`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['drawer-movements'] })
  });
}

export function useRejectDrawerMovement() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.post(`${BASE}/${id}/reject`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['drawer-movements'] })
  });
}
