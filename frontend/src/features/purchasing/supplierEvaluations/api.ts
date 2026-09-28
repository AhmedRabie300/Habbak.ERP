import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { SupplierEvaluationDetail, SupplierEvaluationFormValues, SupplierEvaluationListItem } from './types';

const BASE = '/purchasing/supplier-evaluations';

export function useSupplierEvaluationsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['supplier-evaluations', params],
    queryFn: async () => (await api.get<PagedResult<SupplierEvaluationListItem>>(BASE, { params })).data
  });
}

export function useSupplierEvaluation(id: number | undefined) {
  return useQuery({
    queryKey: ['supplier-evaluations', id],
    queryFn: async () => (await api.get<SupplierEvaluationDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateSupplierEvaluation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SupplierEvaluationFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['supplier-evaluations'] })
  });
}

export function useUpdateSupplierEvaluation(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: SupplierEvaluationFormValues & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['supplier-evaluations'] })
  });
}

export function useDeleteSupplierEvaluation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: (_data, id) => {
      // Remove the deleted evaluation's own detail cache FIRST — invalidating
      // ['supplier-evaluations'] alone would still prefix-match ['supplier-evaluations', id] and
      // refetch a 404 for the row that's gone.
      queryClient.removeQueries({ queryKey: ['supplier-evaluations', id] });
      queryClient.invalidateQueries({ queryKey: ['supplier-evaluations'] });
    }
  });
}
