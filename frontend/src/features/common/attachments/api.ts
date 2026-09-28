import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface AttachmentListItem {
  id: number;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAtUtc: string;
  uploadedByUserId: number;
}

const BASE = '/attachments';

export function useAttachmentsList(entityType: string, entityId: number | undefined) {
  return useQuery({
    queryKey: ['attachments', entityType, entityId],
    queryFn: async () => (await api.get<AttachmentListItem[]>(BASE, { params: { entityType, entityId } })).data,
    enabled: entityId !== undefined
  });
}

export function useUploadAttachment(entityType: string, entityId: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (file: File) => {
      const formData = new FormData();
      formData.append('entityType', entityType);
      formData.append('entityId', String(entityId));
      formData.append('file', file);
      return (await api.post<{ id: number }>(BASE, formData, { headers: { 'Content-Type': 'multipart/form-data' } })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['attachments', entityType, entityId] })
  });
}

export function useDeleteAttachment(entityType: string, entityId: number | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['attachments', entityType, entityId] })
  });
}

/** Downloads through the authenticated Axios instance (a plain <a href> can't carry the Bearer
 * token) and hands the browser a temporary object URL, same trick lib/export.ts's downloadBlob
 * uses for exported files. */
export async function downloadAttachment(id: number, fileName: string) {
  const response = await api.get(`${BASE}/${id}/content`, { responseType: 'blob' });
  const url = URL.createObjectURL(response.data as Blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}
