import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface AccountTreeNode {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  parentId?: number;
  level: number;
  accountType: string;
  nature: string;
  isPostable: boolean;
  isActive: boolean;
  isSharedAcrossCompanies: boolean;
}

export interface AccountDetail extends AccountTreeNode {
  currencyCode?: string;
  rowVersion: string;
}

export interface CreateAccountPayload {
  code?: string;
  nameAr: string;
  nameEn: string;
  parentId?: number;
  accountType: string;
  nature: string;
  isPostable: boolean;
  currencyCode?: string;
  isSharedAcrossCompanies: boolean;
}

export interface UpdateAccountPayload {
  rowVersion: string;
  nameAr: string;
  nameEn: string;
  accountType: string;
  nature: string;
  isPostable: boolean;
  currencyCode?: string;
  isActive: boolean;
}

const BASE = '/accounting/accounts';

export function useAccountTree() {
  return useQuery({
    queryKey: ['account-tree'],
    queryFn: async () => (await api.get<AccountTreeNode[]>(`${BASE}/tree`)).data
  });
}

export function useAccountDetail(id: number | undefined) {
  return useQuery({
    queryKey: ['account-tree', id],
    queryFn: async () => (await api.get<AccountDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CreateAccountPayload) => (await api.post<number>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['account-tree'] })
  });
}

export function useUpdateAccount(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: UpdateAccountPayload) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['account-tree'] })
  });
}

export function useDeleteAccount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: (_data, id) => {
      // Remove the deleted account's own detail cache FIRST — invalidating ['account-tree']
      // alone would still prefix-match ['account-tree', id] and refetch a 404 for the row
      // that's gone.
      queryClient.removeQueries({ queryKey: ['account-tree', id] });
      queryClient.invalidateQueries({ queryKey: ['account-tree'] });
    }
  });
}
