import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { GoodsReceiptDetail, GoodsReceiptFormValues, GoodsReceiptListItem, PostablePurchaseOrder } from './types';

const BASE = '/purchasing/goods-receipts';

export function useGoodsReceiptsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['goods-receipts', params],
    queryFn: async () => (await api.get<PagedResult<GoodsReceiptListItem>>(BASE, { params })).data
  });
}

export function useGoodsReceipt(id: number | undefined) {
  return useQuery({
    queryKey: ['goods-receipts', id],
    queryFn: async () => (await api.get<GoodsReceiptDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function usePostablePurchaseOrdersList() {
  return useQuery({
    queryKey: ['goods-receipts', 'postable-orders'],
    queryFn: async () => (await api.get<PostablePurchaseOrder[]>(`${BASE}/postable-orders`)).data
  });
}

export function useCreateGoodsReceipt() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: GoodsReceiptFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['goods-receipts'] });
      queryClient.invalidateQueries({ queryKey: ['purchase-orders'] });
    }
  });
}

export function usePostGoodsReceipt(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/post`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['goods-receipts'] });
      queryClient.invalidateQueries({ queryKey: ['purchase-orders'] });
    }
  });
}

export function useCancelGoodsReceipt(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['goods-receipts'] })
  });
}
