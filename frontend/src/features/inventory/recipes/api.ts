import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { RecipeDetail, RecipeFormValues, RecipeListItem } from './types';

const BASE = '/inventory/recipes';

export function useRecipesList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['recipes', params],
    queryFn: async () => (await api.get<PagedResult<RecipeListItem>>(BASE, { params })).data
  });
}

export function useRecipe(id: number | undefined) {
  return useQuery({
    queryKey: ['recipes', id],
    queryFn: async () => (await api.get<RecipeDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateRecipe() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: RecipeFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['recipes'] })
  });
}

export function useUpdateRecipe(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: Omit<RecipeFormValues, 'outputItemId'> & { rowVersion: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['recipes'] })
  });
}

export function useSubmitRecipe(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/submit`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['recipes'] })
  });
}

export function useApproveRecipe(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/approve`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['recipes'] })
  });
}

export function useRejectRecipe(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/${id}/reject`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['recipes'] })
  });
}

export function useCreateNewRecipeVersion(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => (await api.post<{ id: number }>(`${BASE}/${id}/new-version`)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['recipes'] })
  });
}
