import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { PayableInvoice, SupplierPaymentDetail, SupplierPaymentFormValues, SupplierPaymentListItem } from './types';

const BASE = '/purchasing/supplier-payments';

export function useSupplierPaymentsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['supplier-payments', params],
    queryFn: async () => (await api.get<PagedResult<SupplierPaymentListItem>>(BASE, { params })).data
  });
}

export function useSupplierPayment(id: number | undefined) {
  return useQuery({
    queryKey: ['supplier-payments', id],
    queryFn: async () => (await api.get<SupplierPaymentDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function usePayableInvoicesList(supplierId: number | undefined) {
  return useQuery({
    queryKey: ['purchase-invoices', 'payable', supplierId],
    queryFn: async () => (await api.get<PayableInvoice[]>('/purchasing/purchase-invoices/payable', { params: { supplierId } })).data,
    enabled: supplierId !== undefined && supplierId > 0
  });
}

export function useCreateSupplierPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SupplierPaymentFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['supplier-payments'] });
      queryClient.invalidateQueries({ queryKey: ['purchase-invoices'] });
    }
  });
}

export function useUpdateSupplierPayment(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SupplierPaymentFormValues & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['supplier-payments'] })
  });
}

export function usePostSupplierPayment(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => (await api.post<{ status: string }>(`${BASE}/${id}/post`)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['supplier-payments'] });
      queryClient.invalidateQueries({ queryKey: ['purchase-invoices'] });
    }
  });
}

export function useCancelSupplierPayment(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['supplier-payments'] })
  });
}

export function useReverseSupplierPayment(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/reverse`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['supplier-payments'] });
      queryClient.invalidateQueries({ queryKey: ['purchase-invoices'] });
    }
  });
}
