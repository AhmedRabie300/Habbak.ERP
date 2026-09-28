import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { GeneratedQRTicket, QRTicketItemInput, QRTicketPreview } from './types';

const BASE = '/pos/qr-tickets';

export function useQRTicketPreview(idempotencyKey: string | undefined) {
  return useQuery({
    queryKey: ['qr-tickets', idempotencyKey],
    queryFn: async () => (await api.get<QRTicketPreview>(`${BASE}/${idempotencyKey}`)).data,
    enabled: !!idempotencyKey,
    retry: false
  });
}

export function useGenerateQRTicket() {
  return useMutation({
    mutationFn: async (items: QRTicketItemInput[]) => (await api.post<GeneratedQRTicket>(`${BASE}/generate`, { items })).data
  });
}

export function useRedeemQRTicket() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { checkId: number; idempotencyKey: string }) => api.post(`${BASE}/redeem`, payload),
    onSuccess: (_data, variables) => queryClient.invalidateQueries({ queryKey: ['pos-checks', variables.checkId] })
  });
}
