import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.2 — HR_ORG_UNITS. */

export interface OrgUnit {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  parentId: number | null;
  branchId: number | null;
  managerEmployeeId: number | null;
  costCenterDimensionValueId: number | null;
}

export type OrgUnitInput = Omit<OrgUnit, 'id' | 'code'>;

const BASE = '/hr/org-units';

export function useOrgUnits() {
  return useQuery({
    queryKey: ['hr-org-units'],
    queryFn: async () => (await api.get<OrgUnit[]>(BASE)).data
  });
}

export function useSaveOrgUnit(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; data: OrgUnitInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, payload.data);
        return { id };
      }
      return (await api.post<{ id: number }>(BASE, { code: payload.code, ...payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-org-units'] })
  });
}

export function useDeleteOrgUnit() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-org-units'] })
  });
}
