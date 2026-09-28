import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { DeliveryOrderListItem, ReceiveDeliveryOrderPayload } from './types';

const BASE = '/pos/delivery-orders';

export function useDeliveryOrdersList(posTerminalId?: number) {
  return useQuery({
    queryKey: ['delivery-orders', posTerminalId],
    queryFn: async () => (await api.get<DeliveryOrderListItem[]>(BASE, { params: { posTerminalId } })).data
  });
}

export function useReceiveDeliveryOrder() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: ReceiveDeliveryOrderPayload) =>
      (await api.post<{ id: number; checkId: number }>(`${BASE}/receive`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['delivery-orders'] })
  });
}
