import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { POSPaymentMethodConfig } from './types';

const BASE = '/pos/payment-method-configs';

export function usePOSPaymentMethodConfigsList(posTerminalId: number | undefined) {
  return useQuery({
    queryKey: ['pos-payment-method-configs', posTerminalId],
    queryFn: async () => (await api.get<POSPaymentMethodConfig[]>(BASE, { params: { posTerminalId } })).data,
    enabled: posTerminalId !== undefined
  });
}

export function useCreatePOSPaymentMethodConfig() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { posTerminalId: number; paymentMethodId: number; linkedTreasuryAccountId: number; isEnabled: boolean }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-payment-method-configs'] })
  });
}

export function useUpdatePOSPaymentMethodConfig() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { id: number; linkedTreasuryAccountId: number; isEnabled: boolean }) =>
      api.put(`${BASE}/${payload.id}`, { linkedTreasuryAccountId: payload.linkedTreasuryAccountId, isEnabled: payload.isEnabled }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-payment-method-configs'] })
  });
}

export function useDeletePOSPaymentMethodConfig() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['pos-payment-method-configs'] })
  });
}
