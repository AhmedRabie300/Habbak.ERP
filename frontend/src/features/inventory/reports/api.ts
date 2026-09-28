import { useQuery } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type {
  BelowMinimumItemRow, DailyMovementRow, InventoryCountReportRow, ItemMovementRow, RawMaterialConsumptionRow,
  StockReportRow, StockTransferRow, WasteReportRow
} from './types';

const BASE = '/inventory/reports';

export function useItemMovementReport(itemId: number | undefined, warehouseId: number | undefined, from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['inventory-reports', 'item-movement', itemId, warehouseId, from, to],
    queryFn: async () => (await api.get<ItemMovementRow[]>(`${BASE}/item-movement`, { params: { itemId, warehouseId, from, to } })).data,
    enabled: enabled && itemId !== undefined
  });
}

export function useStockReport() {
  return useQuery({
    queryKey: ['inventory-reports', 'stock-report'],
    queryFn: async () => (await api.get<StockReportRow[]>(`${BASE}/stock-report`)).data
  });
}

export function useBelowMinimumItemsReport() {
  return useQuery({
    queryKey: ['inventory-reports', 'below-minimum-items'],
    queryFn: async () => (await api.get<BelowMinimumItemRow[]>(`${BASE}/below-minimum-items`)).data
  });
}

export function useInventoryCountsReport() {
  return useQuery({
    queryKey: ['inventory-reports', 'inventory-counts'],
    queryFn: async () => (await api.get<InventoryCountReportRow[]>(`${BASE}/inventory-counts`)).data
  });
}

export function useDailyMovementReport(date: string, enabled: boolean) {
  return useQuery({
    queryKey: ['inventory-reports', 'daily-movement', date],
    queryFn: async () => (await api.get<DailyMovementRow[]>(`${BASE}/daily-movement`, { params: { date } })).data,
    enabled
  });
}

export function useRawMaterialConsumptionReport(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['inventory-reports', 'raw-material-consumption', from, to],
    queryFn: async () => (await api.get<RawMaterialConsumptionRow[]>(`${BASE}/raw-material-consumption`, { params: { from, to } })).data,
    enabled
  });
}

export function useWasteReport() {
  return useQuery({
    queryKey: ['inventory-reports', 'waste'],
    queryFn: async () => (await api.get<WasteReportRow[]>(`${BASE}/waste`)).data
  });
}

export function useStockTransfersReport(from: string, to: string, enabled: boolean) {
  return useQuery({
    queryKey: ['inventory-reports', 'stock-transfers', from, to],
    queryFn: async () => (await api.get<StockTransferRow[]>(`${BASE}/stock-transfers`, { params: { from, to } })).data,
    enabled
  });
}
