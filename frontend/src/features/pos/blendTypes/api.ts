import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface BlendType {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  pricePerGram: number;
  isActive: boolean;
}

const BASE = '/pos/blend-types';

export function useBlendTypesList() {
  return useQuery({
    queryKey: ['blend-types'],
    queryFn: async () => (await api.get<BlendType[]>(BASE)).data
  });
}

export function useCreateBlendType() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; nameAr: string; nameEn: string; pricePerGram: number }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['blend-types'] })
  });
}

export function useUpdateBlendType(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { nameAr: string; nameEn: string; pricePerGram: number; isActive: boolean }) =>
      api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['blend-types'] })
  });
}

export function useDeleteBlendType() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['blend-types'] })
  });
}

export interface CompositionInput {
  blendTypeId: number;
  weightGrams: number;
}

export interface GenerateBlendTicketResult {
  id: number;
  idempotencyKey: string;
  totalWeightGrams: number;
  totalPrice: number;
}

export function useGenerateBlendTicket() {
  return useMutation({
    mutationFn: async (compositions: CompositionInput[]) =>
      (await api.post<GenerateBlendTicketResult>(`${BASE}/generate-ticket`, { compositions })).data
  });
}
