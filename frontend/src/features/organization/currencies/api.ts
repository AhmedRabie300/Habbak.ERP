import { useEffect } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface Currency {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  isActive: boolean;
  /** The one currency every new record starts with. */
  isDefault: boolean;
}

const BASE = '/organization/currencies';

export function useCurrenciesList() {
  return useQuery({
    queryKey: ['currencies'],
    queryFn: async () => (await api.get<Currency[]>(BASE)).data
  });
}

/** The default currency (Remarks3 item 1). */
export function useDefaultCurrency() {
  const { data } = useCurrenciesList();
  return data?.find((c) => c.isDefault);
}

/**
 * Fills a new record's empty currency field with the default currency once it is known — the
 * user may still pick another. Nothing happens on an existing record or a field already filled.
 */
export function useApplyDefaultCurrency<T extends 'code' | 'id'>(
  enabled: boolean, current: string | number | undefined | null, apply: (value: T extends 'code' ? string : number) => void, by: T
) {
  const currency = useDefaultCurrency();
  useEffect(() => {
    if (!enabled || !currency || (current !== undefined && current !== null && current !== '' && current !== 0)) return;
    apply((by === 'code' ? currency.code : currency.id) as T extends 'code' ? string : number);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, currency, current]);
}

export function useCreateCurrency() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code: string; nameAr: string; nameEn: string; isDefault?: boolean }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['currencies'] })
  });
}

export function useUpdateCurrency(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { nameAr: string; nameEn: string; isActive: boolean; isDefault?: boolean }) =>
      api.put(`${BASE}/${id}`, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['currencies'] })
  });
}

export function useDeleteCurrency() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['currencies'] })
  });
}
