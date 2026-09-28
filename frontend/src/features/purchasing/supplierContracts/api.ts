import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { SupplierContractDetail, SupplierContractFormValues, SupplierContractListItem } from './types';

const BASE = '/purchasing/supplier-contracts';

export function useSupplierContractsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['supplier-contracts', params],
    queryFn: async () => (await api.get<PagedResult<SupplierContractListItem>>(BASE, { params })).data
  });
}

export function useSupplierContract(id: number | undefined) {
  return useQuery({
    queryKey: ['supplier-contracts', id],
    queryFn: async () => (await api.get<SupplierContractDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateSupplierContract() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SupplierContractFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['supplier-contracts'] })
  });
}

export function useUpdateSupplierContract(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SupplierContractFormValues & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['supplier-contracts'] })
  });
}

export function useCancelSupplierContract(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/cancel`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['supplier-contracts'] })
  });
}
