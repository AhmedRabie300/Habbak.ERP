import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export type ItemType = 'RawMaterial' | 'SemiFinished' | 'FinishedGood' | 'Consumable' | 'Service';
export type SaleMethod = 'ByPiece' | 'ByWeight' | 'ByVolume';
export type CostMethod = 'WeightedAverage' | 'Fifo';
export type ItemStatus = 'Active' | 'UnderReview' | 'Inactive';

export interface ItemListItem {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  itemType: ItemType;
  baseUnitOfMeasureCode: string;
  baseUnitOfMeasureId: number;
  /** The units a line of this item may be in: the base unit (factor 1) first, then its conversions. */
  units: ItemUnitOption[];
  taxCode?: string | null;
  status: ItemStatus;
  isActive: boolean;
  posCategoryId?: number;
  posCategoryNameAr?: string;
  defaultPrice?: number;
  isSellable: boolean;
  barcode?: string | null;
}

export interface ItemUnitOption {
  unitId: number;
  code: string;
  nameAr: string;
  /** Base units in one of this unit. */
  factor: number;
}

export interface ItemUnitConversion {
  id: number;
  alternateUnitOfMeasureId: number;
  alternateUnitOfMeasureCode: string;
  conversionFactor: number;
}

export interface ItemWarehouseSettings {
  id: number;
  warehouseId: number;
  warehouseCode: string;
  minStockLevel: number | null;
  maxStockLevel: number | null;
  reorderPoint: number | null;
}

export interface BranchItemLimit {
  id: number;
  branchId: number;
  minRequestQuantity: number | null;
  maxRequestQuantity: number;
}

export interface ItemDetail {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  itemGroupId: number | null;
  posCategoryId: number | null;
  itemType: ItemType;
  barcode: string | null;
  taxCode: string | null;
  baseUnitOfMeasureId: number;
  purchaseUnitOfMeasureId: number | null;
  sellUnitOfMeasureId: number | null;
  saleMethod: SaleMethod;
  costMethod: CostMethod;
  defaultPrice: number | null;
  isStocked: boolean;
  isTracked: boolean;
  trackSerial: boolean;
  shelfLifeDays: number | null;
  standardCost: number | null;
  isPurchasable: boolean;
  isSellable: boolean;
  isManufacturable: boolean;
  allowSubstitutes: boolean;
  status: ItemStatus;
  isActive: boolean;
  unitConversions: ItemUnitConversion[];
  warehouseSettings: ItemWarehouseSettings[];
  branchItemLimits: BranchItemLimit[];
}

export interface ItemUnitConversionInput {
  alternateUnitOfMeasureId: number;
  conversionFactor: number;
}

export interface ItemWarehouseSettingsInput {
  warehouseId: number;
  minStockLevel: number | null;
  maxStockLevel: number | null;
  reorderPoint: number | null;
}

const BASE = '/inventory/items';

export function useItemsList() {
  return useQuery({
    queryKey: ['items'],
    queryFn: async () => (await api.get<ItemListItem[]>(BASE)).data
  });
}

export function useItem(id: number | undefined) {
  return useQuery({
    queryKey: ['items', id],
    queryFn: async () => (await api.get<ItemDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export interface CreateItemPayload {
  code?: string;
  nameAr: string;
  nameEn: string;
  itemGroupId: number | null;
  posCategoryId: number | null;
  itemType: ItemType;
  barcode: string | null;
  taxCode: string | null;
  baseUnitOfMeasureId: number;
  purchaseUnitOfMeasureId: number | null;
  sellUnitOfMeasureId: number | null;
  saleMethod: SaleMethod;
  costMethod: CostMethod;
  defaultPrice: number | null;
  isStocked: boolean;
  isTracked: boolean;
  trackSerial: boolean;
  shelfLifeDays: number | null;
  standardCost: number | null;
  isPurchasable: boolean;
  isSellable: boolean;
  isManufacturable: boolean;
  allowSubstitutes: boolean;
  status: ItemStatus;
}

export interface BranchItemLimitInput {
  branchId: number;
  minRequestQuantity: number | null;
  maxRequestQuantity: number;
}

export interface UpdateItemPayload extends CreateItemPayload {
  unitConversions: ItemUnitConversionInput[];
  warehouseSettings: ItemWarehouseSettingsInput[];
  branchItemLimits: BranchItemLimitInput[];
}

export function useCreateItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CreateItemPayload) => (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['items'] })
  });
}

export function useUpdateItem(id: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: UpdateItemPayload) => api.put(`${BASE}/${id}`, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['items'] });
    }
  });
}

export function useDeleteItem() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: (_data, id) => {
      // Remove the deleted item's own detail cache FIRST — invalidating ['items'] alone would
      // still prefix-match ['items', id] and refetch a 404 for the row that's gone.
      queryClient.removeQueries({ queryKey: ['items', id] });
      queryClient.invalidateQueries({ queryKey: ['items'] });
    }
  });
}
