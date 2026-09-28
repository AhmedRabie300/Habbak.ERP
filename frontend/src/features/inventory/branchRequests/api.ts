import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type {
  ApproveBranchRequestFormValues,
  BranchRequestDetail,
  BranchRequestFormValues,
  BranchRequestListItem
} from './types';

const BASE = '/inventory/branch-requests';

export function useBranchRequestsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['branch-requests', params],
    queryFn: async () => (await api.get<PagedResult<BranchRequestListItem>>(BASE, { params })).data
  });
}

export function useBranchRequest(id: number | undefined) {
  return useQuery({
    queryKey: ['branch-requests', id],
    queryFn: async () => (await api.get<BranchRequestDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateBranchRequest() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: BranchRequestFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['branch-requests'] })
  });
}

export function useUpdateBranchRequest(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: Omit<BranchRequestFormValues, 'branchId'> & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['branch-requests'] })
  });
}

export function useSubmitBranchRequest(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/submit`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['branch-requests'] })
  });
}

export function useApproveBranchRequest(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: ApproveBranchRequestFormValues) =>
      (await api.post<{ status: string; transferOrderId?: number }>(`${BASE}/${id}/approve`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['branch-requests'] })
  });
}
