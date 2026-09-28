import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5, Sub-Batch 1.5.2 — HR_DOCUMENT_TYPES
 * (backend entity/route name is EmployeeDocumentType, the screen code stays HR_DOCUMENT_TYPES). */

export interface DocumentType {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  requiresExpiry: boolean;
  isMandatory: boolean;
  expiryAlertDays: number | null;
}

export type DocumentTypeInput = Omit<DocumentType, 'id' | 'code'>;

const BASE = '/hr/employee-document-types';

export function useDocumentTypes() {
  return useQuery({
    queryKey: ['hr-document-types'],
    queryFn: async () => (await api.get<DocumentType[]>(BASE)).data
  });
}

export function useSaveDocumentType(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; data: DocumentTypeInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, payload.data);
        return { id };
      }
      return (await api.post<{ id: number }>(BASE, { code: payload.code, ...payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-document-types'] })
  });
}

export function useDeleteDocumentType() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-document-types'] })
  });
}
