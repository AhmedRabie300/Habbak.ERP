import { useQuery } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { SupplierPriceHistoryRow } from './types';

export function useSupplierPriceHistoryReport(supplierId: number | undefined, itemId: number | undefined) {
  return useQuery({
    queryKey: ['supplier-price-history', supplierId, itemId],
    queryFn: async () => (await api.get<SupplierPriceHistoryRow[]>('/purchasing/supplier-price-history', {
      params: { supplierId, itemId }
    })).data
  });
}
