import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../app/api';

/** 08-Module-Maintenance-FixedAssets — the assets side (screens 1-7 and 13). */

export type DepreciationMethod = 'StraightLine' | 'DecliningBalance' | 'NoDepreciation';
export type FixedAssetStatus = 'Draft' | 'Active' | 'InMaintenance' | 'Transferred' | 'Disposed' | 'WrittenOff';
export type DepreciationScheduleStatus = 'Scheduled' | 'Posted' | 'Cancelled';
export type DepreciationRunStatus = 'Draft' | 'Posted' | 'Reversed';
export type DisposalType = 'Sale' | 'Scrap' | 'Loss';
export type AssetTransferStatus = 'Draft' | 'Posted' | 'Rejected' | 'Cancelled';
export type AssetDisposalStatus = 'Draft' | 'Posted' | 'Rejected' | 'Cancelled';
export type AssetPhysicalCountStatus = 'Draft' | 'InProgress' | 'Completed' | 'Rejected';
export type AssetCondition = 'Good' | 'Fair' | 'Damaged';
export type AssetPhysicalCountFrequency = 'Annual' | 'SemiAnnual' | 'Quarterly';

export interface FixedAssetCategory {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  depreciationMethod: DepreciationMethod;
  defaultDepreciationRate: number | null;
  defaultUsefulLifeYears: number | null;
  defaultSalvagePercentage: number | null;
  assetAccountId: number;
  accumulatedDepreciationAccountId: number;
  depreciationExpenseAccountId: number;
  disposalGainAccountId: number | null;
  disposalLossAccountId: number | null;
  maintenanceExpenseAccountId: number;
  isActive: boolean;
  rowVersion: string;
}

export type FixedAssetCategoryInput = Omit<FixedAssetCategory, 'id' | 'code' | 'rowVersion'>;

export interface FixedAssetListItem {
  id: number;
  assetNumber: string;
  nameAr: string;
  nameEn: string;
  categoryId: number;
  categoryNameAr: string;
  branchId: number | null;
  branchNameAr: string | null;
  status: FixedAssetStatus;
  acquisitionDate: string;
  baseCurrencyAmount: number;
  accumulatedDepreciation: number;
  netBookValue: number;
  location: string | null;
}

export interface DepreciationPeriod {
  id: number;
  periodNumber: number;
  periodStart: string;
  periodEnd: string;
  amount: number;
  accumulatedAfter: number;
  bookValueAfter: number;
  status: DepreciationScheduleStatus;
  depreciationRunId: number | null;
  runNumber: string | null;
  journalEntryId: number | null;
}

export interface FixedAssetDetail {
  id: number;
  assetNumber: string;
  nameAr: string;
  nameEn: string;
  branchId: number | null;
  categoryId: number;
  categoryNameAr: string;
  serialNumber: string | null;
  barcode: string | null;
  location: string | null;
  acquisitionDate: string;
  acquisitionCost: number;
  currencyCode: string;
  exchangeRate: number;
  baseCurrencyAmount: number;
  supplierId: number | null;
  purchaseInvoiceId: number | null;
  fundingAccountId: number | null;
  acquisitionJournalEntryId: number | null;
  usefulLifeYears: number | null;
  salvageValue: number;
  depreciationMethod: DepreciationMethod;
  depreciationRate: number | null;
  depreciationStartDate: string;
  firstMonthProrated: boolean;
  accumulatedDepreciation: number;
  netBookValue: number;
  status: FixedAssetStatus;
  disposalDate: string | null;
  disposalReason: string | null;
  disposalProceeds: number | null;
  disposalJournalEntryId: number | null;
  custodyOfficerId: number | null;
  custodyOfficerName: string | null;
  costCenterValueId: number | null;
  notes: string | null;
  rowVersion: string;
  schedule: DepreciationPeriod[];
}

export interface FixedAssetInput {
  nameAr: string;
  nameEn: string;
  branchId: number | null;
  categoryId: number;
  serialNumber: string | null;
  barcode: string | null;
  location: string | null;
  acquisitionDate: string;
  acquisitionCost: number;
  currencyCode: string | null;
  exchangeRate: number;
  supplierId: number | null;
  purchaseInvoiceId: number | null;
  fundingAccountId: number | null;
  usefulLifeYears: number | null;
  salvageValue: number | null;
  depreciationMethod: DepreciationMethod | null;
  depreciationRate: number | null;
  depreciationStartDate: string | null;
  firstMonthProrated: boolean | null;
  custodyOfficerId: number | null;
  costCenterValueId: number | null;
  notes: string | null;
}

export interface DepreciationScheduleRow {
  fixedAssetId: number;
  assetNumber: string;
  assetNameAr: string;
  period: DepreciationPeriod;
}

export interface DepreciationRun {
  id: number;
  runNumber: string;
  runDate: string;
  year: number;
  month: number;
  status: DepreciationRunStatus;
  totalDepreciation: number;
  assetCount: number;
  journalEntryId: number | null;
  postedAtUtc: string | null;
  reversedAtUtc: string | null;
  reversalJournalEntryId: number | null;
}

export interface DepreciationRunDetail {
  run: DepreciationRun;
  lines: {
    scheduleId: number;
    fixedAssetId: number;
    assetNumber: string;
    assetNameAr: string;
    periodNumber: number;
    periodStart: string;
    periodEnd: string;
    amount: number;
  }[];
}

export interface AssetTransfer {
  id: number;
  transferNumber: string;
  fixedAssetId: number;
  assetNumber: string;
  assetNameAr: string;
  fromBranchId: number | null;
  fromBranchNameAr: string | null;
  toBranchId: number;
  toBranchNameAr: string;
  transferDate: string;
  reason: string | null;
  custodyOfficerId: number;
  custodyOfficerName: string;
  status: AssetTransferStatus;
  postedAtUtc: string | null;
  notes: string | null;
}

export interface AssetDisposal {
  id: number;
  disposalNumber: string;
  fixedAssetId: number;
  assetNumber: string;
  assetNameAr: string;
  disposalDate: string;
  disposalType: DisposalType;
  proceeds: number | null;
  proceedsAccountId: number | null;
  buyerName: string | null;
  costAtDisposal: number;
  accumulatedAtDisposal: number;
  bookValueAtDisposal: number;
  gainOrLoss: number;
  status: AssetDisposalStatus;
  journalEntryId: number | null;
  postedAtUtc: string | null;
  notes: string | null;
}

export interface AssetPhysicalCount {
  id: number;
  countNumber: string;
  branchId: number | null;
  branchNameAr: string | null;
  countDate: string;
  status: AssetPhysicalCountStatus;
  lineCount: number;
  foundCount: number;
  missingCount: number;
  damagedCount: number;
  notes: string | null;
}

export interface AssetPhysicalCountLine {
  id: number;
  fixedAssetId: number;
  assetNumber: string;
  assetNameAr: string;
  expectedLocation: string | null;
  actualLocation: string | null;
  isFound: boolean | null;
  condition: AssetCondition | null;
  notes: string | null;
}

export interface AssetPhysicalCountDetail {
  count: AssetPhysicalCount;
  lines: AssetPhysicalCountLine[];
}

export interface AssetCountSchedule {
  frequency: AssetPhysicalCountFrequency | null;
  lastCountDate: string | null;
  nextDueDate: string | null;
  isOverdue: boolean;
}

export interface AssetSettings {
  autoDepreciationEnabled: boolean;
  depreciationRunDay: number;
  requireApprovalForDisposal: boolean;
  requireApprovalForTransfer: boolean;
  maintenanceApprovalThreshold: number | null;
  physicalCountFrequency: AssetPhysicalCountFrequency | null;
  defaultFirstMonthProrated: boolean;
}

const BASE = '/fixed-assets';

// ------------------------------------------------------------------ categories

export function useAssetCategories() {
  return useQuery({
    queryKey: ['asset-categories'],
    queryFn: async () => (await api.get<FixedAssetCategory[]>(`${BASE}/categories`)).data
  });
}

export function useSaveAssetCategory(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; rowVersion?: string; data: FixedAssetCategoryInput }) => {
      if (id) {
        await api.put(`${BASE}/categories/${id}`, { rowVersion: payload.rowVersion, data: payload.data });
        return { id };
      }
      return (await api.post<{ id: number }>(`${BASE}/categories`, { code: payload.code, data: payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['asset-categories'] })
  });
}

export function useDeleteAssetCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/categories/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['asset-categories'] })
  });
}

// ------------------------------------------------------------------ assets

export function useFixedAssets(params?: { status?: FixedAssetStatus; categoryId?: number }) {
  return useQuery({
    queryKey: ['fixed-assets', params],
    queryFn: async () => (await api.get<FixedAssetListItem[]>(BASE, { params })).data
  });
}

export function useFixedAsset(id: number | undefined) {
  return useQuery({
    queryKey: ['fixed-assets', id],
    queryFn: async () => (await api.get<FixedAssetDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useSaveFixedAsset(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { assetNumber?: string; rowVersion?: string; data: FixedAssetInput }) => {
      if (id) {
        await api.put(`${BASE}/${id}`, { rowVersion: payload.rowVersion, data: payload.data });
        return { id };
      }
      return (await api.post<{ id: number }>(BASE, { assetNumber: payload.assetNumber, data: payload.data })).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['fixed-assets'] })
  });
}

/** Capitalises the asset: posts the acquisition and lays out the depreciation schedule. */
export function useActivateFixedAsset() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.post(`${BASE}/${id}/activate`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['fixed-assets'] })
  });
}

export function useDeleteFixedAsset() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['fixed-assets'] })
  });
}

// ------------------------------------------------------------------ schedule and runs

export function useDepreciationSchedule(params: { fixedAssetId?: number; year?: number; month?: number; status?: DepreciationScheduleStatus }) {
  return useQuery({
    queryKey: ['depreciation-schedule', params],
    queryFn: async () => (await api.get<DepreciationScheduleRow[]>(`${BASE}/depreciation-schedule`, { params })).data
  });
}

export function useDepreciationRuns() {
  return useQuery({
    queryKey: ['depreciation-runs'],
    queryFn: async () => (await api.get<DepreciationRun[]>(`${BASE}/depreciation-runs`)).data
  });
}

export function useDepreciationRun(id: number | undefined) {
  return useQuery({
    queryKey: ['depreciation-runs', id],
    queryFn: async () => (await api.get<DepreciationRunDetail>(`${BASE}/depreciation-runs/${id}`)).data,
    enabled: id !== undefined
  });
}

/** Creating the month twice answers with the run it already has (alreadyExisted). */
export function useCreateDepreciationRun() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { year: number; month: number }) =>
      (await api.post<{ id: number; alreadyExisted: boolean; status: DepreciationRunStatus }>(`${BASE}/depreciation-runs`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['depreciation-runs'] })
  });
}

export function useDepreciationRunAction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, action, reason }: { id: number; action: 'post' | 'reverse' | 'delete'; reason?: string }) => {
      if (action === 'delete') {
        await api.delete(`${BASE}/depreciation-runs/${id}`);
      } else if (action === 'reverse') {
        await api.post(`${BASE}/depreciation-runs/${id}/reverse`, { reason });
      } else {
        await api.post(`${BASE}/depreciation-runs/${id}/post`);
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['depreciation-runs'] });
      queryClient.invalidateQueries({ queryKey: ['depreciation-schedule'] });
      queryClient.invalidateQueries({ queryKey: ['fixed-assets'] });
    }
  });
}

// ------------------------------------------------------------------ transfers

export function useAssetTransfers(fixedAssetId?: number) {
  return useQuery({
    queryKey: ['asset-transfers', fixedAssetId],
    queryFn: async () => (await api.get<AssetTransfer[]>(`${BASE}/transfers`, { params: { fixedAssetId } })).data
  });
}

export function useCreateAssetTransfer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      fixedAssetId: number;
      toBranchId: number;
      transferDate: string;
      reason: string | null;
      custodyOfficerId: number;
      notes: string | null;
    }) => (await api.post<{ id: number }>(`${BASE}/transfers`, payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['asset-transfers'] });
      queryClient.invalidateQueries({ queryKey: ['fixed-assets'] });
    }
  });
}

export function useAssetTransferAction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, action }: { id: number; action: 'post' | 'reject' | 'cancel' }) => {
      await api.post(`${BASE}/transfers/${id}/${action}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['asset-transfers'] });
      queryClient.invalidateQueries({ queryKey: ['fixed-assets'] });
    }
  });
}

// ------------------------------------------------------------------ disposals

export function useAssetDisposals(fixedAssetId?: number) {
  return useQuery({
    queryKey: ['asset-disposals', fixedAssetId],
    queryFn: async () => (await api.get<AssetDisposal[]>(`${BASE}/disposals`, { params: { fixedAssetId } })).data
  });
}

export function useCreateAssetDisposal() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      fixedAssetId: number;
      disposalDate: string;
      disposalType: DisposalType;
      proceeds: number | null;
      proceedsAccountId: number | null;
      buyerName: string | null;
      notes: string | null;
    }) => (await api.post<{ id: number }>(`${BASE}/disposals`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['asset-disposals'] })
  });
}

export function useAssetDisposalAction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, action }: { id: number; action: 'post' | 'reject' | 'cancel' }) => {
      await api.post(`${BASE}/disposals/${id}/${action}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['asset-disposals'] });
      queryClient.invalidateQueries({ queryKey: ['fixed-assets'] });
      queryClient.invalidateQueries({ queryKey: ['depreciation-schedule'] });
    }
  });
}

// ------------------------------------------------------------------ physical counts

export function useAssetPhysicalCounts() {
  return useQuery({
    queryKey: ['asset-counts'],
    queryFn: async () => (await api.get<AssetPhysicalCount[]>(`${BASE}/physical-counts`)).data
  });
}

export function useAssetPhysicalCount(id: number | undefined) {
  return useQuery({
    queryKey: ['asset-counts', id],
    queryFn: async () => (await api.get<AssetPhysicalCountDetail>(`${BASE}/physical-counts/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useAssetCountSchedule() {
  return useQuery({
    queryKey: ['asset-count-schedule'],
    queryFn: async () => (await api.get<AssetCountSchedule>(`${BASE}/physical-counts/schedule`)).data
  });
}

export function useCreateAssetPhysicalCount() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { branchId: number; countDate: string; notes: string | null }) =>
      (await api.post<{ id: number }>(`${BASE}/physical-counts`, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['asset-counts'] })
  });
}

export function useAssetPhysicalCountAction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      action,
      lines
    }: {
      id: number;
      action: 'start' | 'record' | 'complete' | 'reject';
      lines?: { lineId: number; isFound: boolean | null; actualLocation: string | null; condition: AssetCondition | null; notes: string | null }[];
    }) => {
      await api.post(`${BASE}/physical-counts/${id}/${action}`, action === 'record' ? { lines } : undefined);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['asset-counts'] });
      queryClient.invalidateQueries({ queryKey: ['fixed-assets'] });
    }
  });
}

// ------------------------------------------------------------------ settings

export function useAssetSettings() {
  return useQuery({
    queryKey: ['asset-settings'],
    queryFn: async () => (await api.get<AssetSettings>(`${BASE}/settings`)).data
  });
}

export function useUpdateAssetSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: AssetSettings) => {
      await api.put(`${BASE}/settings`, payload);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['asset-settings'] })
  });
}
