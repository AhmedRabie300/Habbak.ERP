import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type {
  ApprovalInstance,
  ApprovalWorkflowAssignment,
  ApprovalWorkflowDetail,
  ApprovalWorkflowInput,
  ApprovalWorkflowListItem,
  PendingApproval,
  Screen
} from './types';

const WORKFLOWS = '/approvals/workflows';
const INSTANCES = '/approvals/instances';

// ------------------------------------------------------------------ workflows

export function useApprovalWorkflowsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['approval-workflows', params],
    queryFn: async () => (await api.get<PagedResult<ApprovalWorkflowListItem>>(WORKFLOWS, { params })).data
  });
}

export function useApprovalWorkflow(id: number | undefined) {
  return useQuery({
    queryKey: ['approval-workflows', id],
    queryFn: async () => (await api.get<ApprovalWorkflowDetail>(`${WORKFLOWS}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateApprovalWorkflow() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: ApprovalWorkflowInput) => (await api.post<{ id: number }>(WORKFLOWS, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['approval-workflows'] })
  });
}

export function useUpdateApprovalWorkflow(id: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: ApprovalWorkflowInput) => (await api.put(`${WORKFLOWS}/${id}`, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['approval-workflows'] })
  });
}

export function useSetApprovalWorkflowActive() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, isActive }: { id: number; isActive: boolean }) => api.post(`${WORKFLOWS}/${id}/${isActive ? 'activate' : 'deactivate'}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['approval-workflows'] })
  });
}

// ------------------------------------------------------------------ screens + assignments

export function useScreensList() {
  return useQuery({
    queryKey: ['screens'],
    queryFn: async () => (await api.get<Screen[]>('/screens')).data,
    staleTime: 5 * 60_000
  });
}

export function useWorkflowAssignments() {
  return useQuery({
    queryKey: ['approval-workflow-assignments'],
    queryFn: async () => (await api.get<ApprovalWorkflowAssignment[]>(`${WORKFLOWS}/assignments`)).data
  });
}

export function useAssignWorkflowToScreen() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { screenId: number; approvalWorkflowId: number; minAmount: number | null }) =>
      (await api.post<{ id: number }>(`${WORKFLOWS}/assignments`, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['approval-workflow-assignments'] })
  });
}

export function useUnassignWorkflowFromScreen() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (screenId: number) => api.delete(`${WORKFLOWS}/assignments/${screenId}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['approval-workflow-assignments'] })
  });
}

// ------------------------------------------------------------------ instances / my pending approvals

export function useMyPendingApprovals() {
  return useQuery({
    queryKey: ['approval-instances', 'pending-for-me'],
    queryFn: async () => (await api.get<PendingApproval[]>(`${INSTANCES}/pending-for-me`)).data,
    refetchInterval: 30_000 // Docs/Implementation/Phase-2-Research.md §1.7 — polling, no SignalR yet
  });
}

export function useApprovalInstance(id: number | undefined) {
  return useQuery({
    queryKey: ['approval-instances', id],
    queryFn: async () => (await api.get<ApprovalInstance>(`${INSTANCES}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useApprovalHistory(entityType: string | undefined, entityId: number | undefined) {
  return useQuery({
    queryKey: ['approval-instances', 'history', entityType, entityId],
    queryFn: async () => (await api.get<ApprovalInstance[]>(`${INSTANCES}/history`, { params: { entityType, entityId } })).data,
    enabled: entityType !== undefined && entityId !== undefined
  });
}

export function useApproveInstance() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, reason }: { id: number; reason?: string }) => api.post(`${INSTANCES}/${id}/approve`, { reason }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['approval-instances'] })
  });
}

export function useRejectInstance() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, reason }: { id: number; reason: string }) => api.post(`${INSTANCES}/${id}/reject`, { reason }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['approval-instances'] })
  });
}

export function useReassignInstance() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, newApproverUserId, reason }: { id: number; newApproverUserId: number; reason?: string }) =>
      api.post(`${INSTANCES}/${id}/reassign`, { newApproverUserId, reason }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['approval-instances'] })
  });
}
