import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { JournalEntryDetail, JournalEntryLineInput, JournalEntryListItem } from './types';

const BASE = '/accounting/journal-entries';

export function useJournalEntriesList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['journal-entries', params],
    queryFn: async () => {
      const { data } = await api.get<PagedResult<JournalEntryListItem>>(BASE, { params });
      return data;
    }
  });
}

export function useJournalEntry(id: number | undefined) {
  return useQuery({
    queryKey: ['journal-entries', id],
    queryFn: async () => {
      const { data } = await api.get<JournalEntryDetail>(`${BASE}/${id}`);
      return data;
    },
    enabled: id !== undefined
  });
}

interface CreateOrUpdatePayload {
  branchId?: number;
  entryDate: string;
  description: string;
  lines: JournalEntryLineInput[];
}

export function useCreateJournalEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CreateOrUpdatePayload) => {
      const { data } = await api.post<number>(BASE, payload);
      return data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['journal-entries'] })
  });
}

export function useUpdateJournalEntry(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CreateOrUpdatePayload & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['journal-entries'] });
    }
  });
}

export function usePostJournalEntry(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const { data } = await api.post<{ status: string; approvalInstanceId?: number }>(`${BASE}/${id}/post`);
      return data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['journal-entries'] })
  });
}

export function useReverseJournalEntry(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const { data } = await api.post<{ id: number }>(`${BASE}/${id}/reverse`);
      return data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['journal-entries'] })
  });
}

export function useDeleteJournalEntry() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: (_data, id) => {
      // Remove the deleted entity's own detail cache FIRST — invalidating ['journal-entries']
      // alone would still prefix-match ['journal-entries', id] and refetch a 404 for the row
      // that's gone (surfaced as an error toast) while the edit page is still mounted, mid
      // navigate-away.
      queryClient.removeQueries({ queryKey: ['journal-entries', id] });
      queryClient.invalidateQueries({ queryKey: ['journal-entries'] });
    }
  });
}
