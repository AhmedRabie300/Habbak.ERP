import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { BranchPOSSettings } from './types';

const BASE = '/pos/branch-settings';

export function useBranchPOSSettings(branchId: number | undefined) {
  return useQuery({
    queryKey: ['pos-branch-settings', branchId],
    queryFn: async () => (await api.get<BranchPOSSettings>(`${BASE}/${branchId}`)).data,
    enabled: branchId !== undefined
  });
}

export function useUpdateBranchPOSSettings(branchId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: Omit<BranchPOSSettings, 'branchId'>) => api.put(`${BASE}/${branchId}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-branch-settings', branchId] })
  });
}
