import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type {
  EmployeeCertification,
  EmployeeCertificationInput,
  EmployeeDetail,
  EmployeeDocument,
  EmployeeDocumentInput,
  EmployeeInput,
  EmployeeListItem,
  EmployeeLookupItem,
  EmployeeUpdateInput,
  EmploymentContract,
  EmploymentContractInput,
  PersonalDataInput
} from './types';

const BASE = '/hr/employees';

// ------------------------------------------------------------------ Employee core

export function useEmployeesList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['hr-employees', params],
    queryFn: async () => (await api.get<PagedResult<EmployeeListItem>>(BASE, { params })).data
  });
}

export function useEmployee(id: number | undefined) {
  return useQuery({
    queryKey: ['hr-employees', id],
    queryFn: async () => (await api.get<EmployeeDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

/** [AnySignedInUser] — for picker dropdowns (manager, etc.), not the permission-gated paginated list. */
export function useEmployeesLookup() {
  return useQuery({
    queryKey: ['hr-employees-lookup'],
    queryFn: async () => (await api.get<EmployeeLookupItem[]>('/hr/employees-lookup')).data
  });
}

export function useCreateEmployee() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: EmployeeInput) => (await api.post<{ id: number }>(BASE, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-employees'] })
  });
}

export function useUpdateEmployee(id: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: EmployeeUpdateInput) => api.put(`${BASE}/${id}`, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-employees'] })
  });
}

export function useActivateEmployee() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.post(`${BASE}/${id}/activate`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-employees'] })
  });
}

export function useTerminateEmployee() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.post(`${BASE}/${id}/terminate`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-employees'] })
  });
}

export function useAssignUserToEmployee(id: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (userId: number) => api.post(`${BASE}/${id}/assign-user`, { userId }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-employees'] })
  });
}

export function useSetEmployeeManager(id: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (managerId: number | null) => api.post(`${BASE}/${id}/set-manager`, { managerId }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-employees'] })
  });
}

// ------------------------------------------------------------------ Personal data + PII reveal

export function useCreatePersonalData(employeeId: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: PersonalDataInput) => (await api.post<{ id: number }>(`${BASE}/${employeeId}/personal-data`, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-employees', employeeId] })
  });
}

export function useUpdatePersonalData(employeeId: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: PersonalDataInput) => api.put(`${BASE}/${employeeId}/personal-data`, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-employees', employeeId] })
  });
}

/** Screen HR_EMPLOYEES, button RevealPii — entityId is EmployeePersonalData.Id, not the employee's. */
export function useRevealPiiField() {
  return useMutation({
    mutationFn: async (payload: { entityId: number; fieldName: 'NationalIdEncrypted' | 'BankIbanEncrypted' }) =>
      (await api.post<{ value: string }>('/hr/pii/reveal', { entityType: 'EmployeePersonalData', ...payload })).data
  });
}

// ------------------------------------------------------------------ Employment contracts

export function useContracts(employeeId: number | undefined, params: ListQueryParams) {
  return useQuery({
    queryKey: ['hr-contracts', employeeId, params],
    queryFn: async () => (await api.get<PagedResult<EmploymentContract>>(`${BASE}/${employeeId}/contracts`, { params })).data,
    enabled: employeeId !== undefined
  });
}

export function useCreateContract(employeeId: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: EmploymentContractInput) => (await api.post<{ id: number }>(`${BASE}/${employeeId}/contracts`, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-contracts', employeeId] })
  });
}

export function useUpdateContract(employeeId: number | undefined, id: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: EmploymentContractInput) => api.put(`${BASE}/${employeeId}/contracts/${id}`, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-contracts', employeeId] })
  });
}

export function useRenewContract(employeeId: number | undefined, id: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: EmploymentContractInput) => (await api.post<{ id: number }>(`${BASE}/${employeeId}/contracts/${id}/renew`, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-contracts', employeeId] })
  });
}

export function useTerminateContract(employeeId: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.post(`${BASE}/${employeeId}/contracts/${id}/terminate`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-contracts', employeeId] })
  });
}

export function useDeleteContract(employeeId: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${employeeId}/contracts/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-contracts', employeeId] })
  });
}

// ------------------------------------------------------------------ Documents (metadata + AttachmentId)

export function useDocuments(employeeId: number | undefined) {
  return useQuery({
    queryKey: ['hr-documents', employeeId],
    queryFn: async () => (await api.get<EmployeeDocument[]>(`${BASE}/${employeeId}/documents`)).data,
    enabled: employeeId !== undefined
  });
}

export function useCreateDocument(employeeId: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: EmployeeDocumentInput) => (await api.post<{ id: number }>(`${BASE}/${employeeId}/documents`, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-documents', employeeId] })
  });
}

export function useUpdateDocument(employeeId: number | undefined, id: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: EmployeeDocumentInput) => api.put(`${BASE}/${employeeId}/documents/${id}`, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-documents', employeeId] })
  });
}

export function useDeleteDocument(employeeId: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${employeeId}/documents/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-documents', employeeId] })
  });
}

/** A document row needs an Attachment to already exist (CreateEmployeeDocumentCommand 404s
 * otherwise) — upload through the generic attachments endpoint first, same one AttachmentPanel
 * uses, then feed the returned id as attachmentId. */
export function useUploadDocumentFile(employeeId: number | undefined) {
  return useMutation({
    mutationFn: async (file: File) => {
      const formData = new FormData();
      formData.append('entityType', 'EmployeeDocument');
      formData.append('entityId', String(employeeId));
      formData.append('file', file);
      return (await api.post<{ id: number }>('/attachments', formData, { headers: { 'Content-Type': 'multipart/form-data' } })).data;
    }
  });
}

// ------------------------------------------------------------------ Certifications

export function useCertifications(employeeId: number | undefined) {
  return useQuery({
    queryKey: ['hr-certifications', employeeId],
    queryFn: async () => (await api.get<EmployeeCertification[]>(`${BASE}/${employeeId}/certifications`)).data,
    enabled: employeeId !== undefined
  });
}

export function useCreateCertification(employeeId: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: EmployeeCertificationInput) => (await api.post<{ id: number }>(`${BASE}/${employeeId}/certifications`, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-certifications', employeeId] })
  });
}

export function useUpdateCertification(employeeId: number | undefined, id: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: EmployeeCertificationInput) => api.put(`${BASE}/${employeeId}/certifications/${id}`, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-certifications', employeeId] })
  });
}

export function useDeleteCertification(employeeId: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${employeeId}/certifications/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-certifications', employeeId] })
  });
}
