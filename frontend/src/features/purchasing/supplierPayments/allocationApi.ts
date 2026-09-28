import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Spreading one supplier payment across its invoices (Remarks4, item 7). */
export interface PaymentAllocation {
  purchaseInvoiceId: number;
  invoiceNumber: string;
  invoiceDate: string;
  dueDate: string;
  invoiceTotal: number;
  amountPaid: number;
  amount: number;
}

export interface AllocationInput {
  purchaseInvoiceId: number;
  amount: number;
}

const BASE = '/purchasing/supplier-payments';

export function usePaymentAllocations(paymentId: number | undefined) {
  return useQuery({
    queryKey: ['supplier-payments', paymentId, 'allocations'],
    queryFn: async () => (await api.get<PaymentAllocation[]>(`${BASE}/${paymentId}/allocations`)).data,
    enabled: paymentId !== undefined
  });
}

/** The server's oldest-due-first split for an amount — a suggestion, not a save. */
export function useAllocationProposal() {
  return useMutation({
    mutationFn: async (params: { supplierId: number; amount: number; currencyCode: string }) =>
      (await api.get<AllocationInput[]>(`${BASE}/allocation-proposal`, { params })).data
  });
}

export function useSetPaymentAllocations(paymentId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (allocations: AllocationInput[]) => {
      await api.put(`${BASE}/${paymentId}/allocations`, { allocations });
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['supplier-payments'] });
      queryClient.invalidateQueries({ queryKey: ['purchase-invoices'] });
    }
  });
}
