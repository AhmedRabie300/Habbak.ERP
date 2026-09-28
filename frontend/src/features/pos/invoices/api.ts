import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { PaymentInput, POSInvoiceDetail, POSInvoiceListItem } from './types';

const BASE = '/pos/invoices';

export function usePOSInvoicesList(posTerminalId?: number) {
  return useQuery({
    queryKey: ['pos-invoices', 'list', posTerminalId],
    queryFn: async () => (await api.get<POSInvoiceListItem[]>(BASE, { params: { posTerminalId } })).data
  });
}

export function usePOSInvoice(id: number | undefined) {
  return useQuery({
    queryKey: ['pos-invoices', id],
    queryFn: async () => (await api.get<POSInvoiceDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCompleteCheckPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { checkId: number; tipAmount: number; payments: PaymentInput[] }) =>
      (await api.post<{ id: number }>(`${BASE}/complete-payment`, payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['pos-checks'] });
      queryClient.invalidateQueries({ queryKey: ['pos-tables'] });
      queryClient.invalidateQueries({ queryKey: ['pos-invoices'] });
    }
  });
}
