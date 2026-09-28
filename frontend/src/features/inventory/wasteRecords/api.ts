import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { ListQueryParams, PagedResult } from '../../../app/apiTypes';
import type { WasteRecordDetail, WasteRecordFormValues, WasteRecordListItem } from './types';

const BASE = '/inventory/waste-records';

export function useWasteRecordsList(params: ListQueryParams) {
  return useQuery({
    queryKey: ['waste-records', params],
    queryFn: async () => (await api.get<PagedResult<WasteRecordListItem>>(BASE, { params })).data
  });
}

export function useWasteRecord(id: number | undefined) {
  return useQuery({
    queryKey: ['waste-records', id],
    queryFn: async () => (await api.get<WasteRecordDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateWasteRecord() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: WasteRecordFormValues) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['waste-records'] })
  });
}

/** Only WasteDate/Reason are editable after creation — Quantity/ItemId/WarehouseId already
 * posted a stock movement the moment the record was created and have no reversal path here. */
export function useUpdateWasteRecord(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { rowVersion: string; wasteDate: string; reason: string }) => {
      await api.put(`${BASE}/${id}`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['waste-records'] })
  });
}
