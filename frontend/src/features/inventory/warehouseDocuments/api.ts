import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type {
  PostedTransferOrder,
  TransferReceiptFormValues,
  WarehouseDocumentDetail,
  WarehouseDocumentFormValues,
  WarehouseDocumentKind,
  WarehouseDocumentListItem,
  WarehouseDocumentReadableKind
} from './types';

const TRANSFER_RECEIPT_BASE = '/inventory/transfer-receipt';

export function useWarehouseDocumentsList(kind: WarehouseDocumentReadableKind, params: ListQueryParams) {
  return useQuery({
    queryKey: [kind, params],
    queryFn: async () => {
      const { data } = await api.get<PagedResult<WarehouseDocumentListItem>>(`/inventory/${kind}`, { params });
      return data;
    }
  });
}

export function useWarehouseDocument(kind: WarehouseDocumentReadableKind, id: number | undefined) {
  return useQuery({
    queryKey: [kind, id],
    queryFn: async () => {
      const { data } = await api.get<WarehouseDocumentDetail>(`/inventory/${kind}/${id}`);
      return data;
    },
    enabled: id !== undefined
  });
}

export function useCreateWarehouseDocument(kind: WarehouseDocumentKind) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: WarehouseDocumentFormValues) => (await api.post<{ id: number }>(`/inventory/${kind}`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [kind] })
  });
}

export function useUpdateWarehouseDocument(kind: WarehouseDocumentKind, id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: WarehouseDocumentFormValues & { rowVersion: string }) => {
      await api.put(`/inventory/${kind}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [kind] })
  });
}

export function usePostWarehouseDocument(kind: WarehouseDocumentKind, id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => (await api.post<{ status: string }>(`/inventory/${kind}/${id}/post`)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: [kind] })
  });
}

/** TransferReceipt (screen #11) — dedicated endpoints: Create/Update copy fields from the
 * related, already-Posted TransferOrder (rule 34/37), a shape too different from the generic
 * warehouse-document hooks above to share. */
export function usePostedTransferOrdersList() {
  return useQuery({
    queryKey: ['transfer-receipt', 'postable-orders'],
    queryFn: async () => (await api.get<PostedTransferOrder[]>(`${TRANSFER_RECEIPT_BASE}/postable-orders`)).data
  });
}

export function useTransferReceiptsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['transfer-receipt', params],
    queryFn: async () => (await api.get<PagedResult<WarehouseDocumentListItem>>(TRANSFER_RECEIPT_BASE, { params })).data
  });
}

export function useTransferReceipt(id: number | undefined) {
  return useQuery({
    queryKey: ['transfer-receipt', id],
    queryFn: async () => (await api.get<WarehouseDocumentDetail>(`${TRANSFER_RECEIPT_BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateTransferReceipt() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: TransferReceiptFormValues) => (await api.post<{ id: number }>(TRANSFER_RECEIPT_BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['transfer-receipt'] })
  });
}

export function useUpdateTransferReceipt(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: Omit<TransferReceiptFormValues, 'relatedWarehouseDocumentId'> & { rowVersion: string }) => {
      await api.put(`${TRANSFER_RECEIPT_BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['transfer-receipt'] })
  });
}

export function usePostTransferReceipt(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => (await api.post<{ status: string }>(`${TRANSFER_RECEIPT_BASE}/${id}/post`)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['transfer-receipt'] })
  });
}
