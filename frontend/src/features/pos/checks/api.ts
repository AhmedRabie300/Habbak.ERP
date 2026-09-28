import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { CheckDetail, CheckListItem, CheckOrderType, ManualDiscountType } from './types';

const BASE = '/pos/checks';

function invalidateCheck(queryClient: ReturnType<typeof useQueryClient>, id: number) {
  queryClient.invalidateQueries({ queryKey: ['pos-checks', id] });
  queryClient.invalidateQueries({ queryKey: ['pos-checks', 'list'] });
  queryClient.invalidateQueries({ queryKey: ['pos-tables'] });
}

export function useOpenChecksList(posTerminalId?: number) {
  return useQuery({
    queryKey: ['pos-checks', 'list', 'open', posTerminalId],
    queryFn: async () => (await api.get<CheckListItem[]>(`${BASE}/open`, { params: { posTerminalId } })).data
  });
}

export function useHeldChecksList(posTerminalId?: number) {
  return useQuery({
    queryKey: ['pos-checks', 'list', 'held', posTerminalId],
    queryFn: async () => (await api.get<CheckListItem[]>(`${BASE}/held`, { params: { posTerminalId } })).data
  });
}

export function useCheck(id: number | undefined) {
  return useQuery({
    queryKey: ['pos-checks', id],
    queryFn: async () => (await api.get<CheckDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useOpenCheckForTable() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { posTerminalId: number; tableId: number }) =>
      (await api.post<{ id: number }>(`${BASE}/open-for-table`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-tables'] })
  });
}

export function useOpenCheckStandalone() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { posTerminalId: number; orderType: CheckOrderType; customerId?: number }) =>
      (await api.post<{ id: number }>(`${BASE}/open-standalone`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-checks', 'list'] })
  });
}

export function useHoldCheck(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/hold`),
    onSuccess: () => invalidateCheck(queryClient, id)
  });
}

export function useResumeCheck(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/resume`),
    onSuccess: () => invalidateCheck(queryClient, id)
  });
}

export function useCancelCheck(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => invalidateCheck(queryClient, id)
  });
}

export function useChangeCheckOrderType(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (orderType: CheckOrderType) => api.post(`${BASE}/${id}/order-type`, { orderType }),
    onSuccess: () => invalidateCheck(queryClient, id)
  });
}

export function useMergeChecks() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { targetCheckId: number; sourceCheckIds: number[] }) => api.post(`${BASE}/merge`, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pos-checks'] });
      queryClient.invalidateQueries({ queryKey: ['pos-tables'] });
    }
  });
}

export function useAddCheckLine(checkId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { itemId: number; quantity: number; unitPrice?: number; discountAmount: number; note?: string }) =>
      (await api.post<{ id: number }>(`${BASE}/${checkId}/lines`, payload)).data,
    onSuccess: () => invalidateCheck(queryClient, checkId)
  });
}

export function useUpdateCheckLine(checkId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { lineId: number; quantity: number; unitPrice: number; discountAmount: number; isPriceManuallyOverridden: boolean; note?: string }) =>
      api.put(`${BASE}/${checkId}/lines/${payload.lineId}`, payload),
    onSuccess: () => invalidateCheck(queryClient, checkId)
  });
}

export function useRemoveCheckLine(checkId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { lineId: number; voidReason?: string }) =>
      api.delete(`${BASE}/${checkId}/lines/${payload.lineId}`, { data: { voidReason: payload.voidReason } }),
    onSuccess: () => invalidateCheck(queryClient, checkId)
  });
}

export function useFireCheckLinesToKitchen(checkId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${checkId}/fire-to-kitchen`),
    onSuccess: () => invalidateCheck(queryClient, checkId)
  });
}

export function useApplyManualDiscount(checkId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { type: ManualDiscountType; value: number; reason: string }) =>
      api.post(`${BASE}/${checkId}/apply-discount`, payload),
    onSuccess: () => invalidateCheck(queryClient, checkId)
  });
}

export function useRemoveManualDiscount(checkId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${checkId}/remove-discount`),
    onSuccess: () => invalidateCheck(queryClient, checkId)
  });
}

export function useSetCheckCustomer(checkId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (customerId: number | null) => api.post(`${BASE}/${checkId}/customer`, { customerId }),
    onSuccess: () => invalidateCheck(queryClient, checkId)
  });
}

export function useSetLoyaltyRedemption(checkId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (points: number) => api.post(`${BASE}/${checkId}/loyalty-redemption`, { points }),
    onSuccess: () => invalidateCheck(queryClient, checkId)
  });
}
