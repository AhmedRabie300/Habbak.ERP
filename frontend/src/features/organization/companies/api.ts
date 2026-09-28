import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface Company {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  commercialRegister?: string;
  taxCard?: string;
  baseCurrencyId: number;
  baseCurrencyCode: string;
  isActive: boolean;
}

const BASE = '/organization/companies';

export function useCompaniesList() {
  return useQuery({
    queryKey: ['companies'],
    queryFn: async () => (await api.get<Company[]>(BASE)).data
  });
}

/** Read-only — the logged-in session's own company (e.g. Branches screen's "Company" field). */
export function useCurrentCompany() {
  return useQuery({
    queryKey: ['companies', 'current'],
    queryFn: async () => (await api.get<Company>(`${BASE}/current`)).data
  });
}

export interface CompanyFormValues {
  code: string;
  nameAr: string;
  nameEn: string;
  commercialRegister?: string;
  taxCard?: string;
  baseCurrencyId: number;
}

export function useCreateCompany() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CompanyFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['companies'] })
  });
}

export function useUpdateCompany(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: Omit<CompanyFormValues, 'code'> & { isActive: boolean }) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['companies'] })
  });
}

export function useDeleteCompany() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['companies'] })
  });
}
