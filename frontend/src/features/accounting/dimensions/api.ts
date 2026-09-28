import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export type LinkedEntityType = 'None' | 'Branch' | 'POSTerminal' | 'Warehouse' | 'Cashier' | 'Customer' | 'Supplier';

export const LINKED_ENTITY_TYPES: LinkedEntityType[] = ['None', 'Branch', 'POSTerminal', 'Warehouse', 'Cashier', 'Customer', 'Supplier'];

/** Values mirrored from another screen's records (everything linked except cashiers, which are entered by hand). */
export const isMirroredLinkedType = (type: LinkedEntityType) => type !== 'None' && type !== 'Cashier';

export interface Dimension {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  linkedEntityType: LinkedEntityType;
}

export interface DimensionValue {
  id: number;
  costCenterDimensionId: number;
  code: string;
  nameAr: string;
  nameEn: string;
  parentId?: number;
  level: number;
  isActive: boolean;
}

export interface AccountDimensionLink {
  id: number;
  accountId: number;
  costCenterDimensionId: number;
  dimensionNameAr: string;
  dimensionNameEn: string;
  displayOrder: number;
  isMandatory: boolean;
}

export function useDimensionsList() {
  return useQuery({
    queryKey: ['dimensions'],
    queryFn: async () => (await api.get<Dimension[]>('/accounting/dimensions')).data
  });
}

export function useDimension(id: number | undefined) {
  return useQuery({
    queryKey: ['dimensions', id],
    queryFn: async () => (await api.get<Dimension>(`/accounting/dimensions/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateDimension() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; nameAr: string; nameEn: string; linkedEntityType: LinkedEntityType }) =>
      (await api.post<{ id: number }>('/accounting/dimensions', payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dimensions'] })
  });
}

export function useUpdateDimension(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { nameAr: string; nameEn: string; isActive: boolean }) =>
      api.put(`/accounting/dimensions/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dimensions'] })
  });
}

export function useDimensionValues(dimensionId: number | undefined) {
  return useQuery({
    queryKey: ['dimension-values', dimensionId],
    queryFn: async () => (await api.get<DimensionValue[]>(`/accounting/dimensions/${dimensionId}/values`)).data,
    enabled: dimensionId !== undefined
  });
}

export function useCreateDimensionValue(dimensionId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; nameAr: string; nameEn: string; parentId?: number }) =>
      (await api.post<{ id: number }>(`/accounting/dimensions/${dimensionId}/values`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dimension-values', dimensionId] })
  });
}

export function useDeleteDimension() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`/accounting/dimensions/${id}`),
    onSuccess: (_data, id) => {
      // Remove the deleted dimension's own detail cache FIRST — invalidating ['dimensions']
      // alone would still prefix-match ['dimensions', id] and refetch a 404 for the row that's gone.
      queryClient.removeQueries({ queryKey: ['dimensions', id] });
      queryClient.invalidateQueries({ queryKey: ['dimensions'] });
    }
  });
}

export function useDeleteDimensionValue(dimensionId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`/accounting/dimensions/values/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dimension-values', dimensionId] })
  });
}

export function useAccountDimensionLinks(accountId: number | undefined) {
  return useQuery({
    queryKey: ['dimension-links', accountId],
    queryFn: async () => (await api.get<AccountDimensionLink[]>('/accounting/dimensions/links', { params: { accountId } })).data,
    enabled: accountId !== undefined
  });
}

export function useCreateAccountDimensionLink() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { accountId: number; dimensionId: number; displayOrder: number; isMandatory: boolean }) =>
      (await api.post<{ id: number }>('/accounting/dimensions/links', payload)).data,
    onSuccess: (_data, variables) => queryClient.invalidateQueries({ queryKey: ['dimension-links', variables.accountId] })
  });
}

export function useDeleteAccountDimensionLink() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`/accounting/dimensions/links/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['dimension-links'] })
  });
}
