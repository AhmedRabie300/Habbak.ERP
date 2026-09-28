import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../app/api';

/** 08-Module-Maintenance-FixedAssets — the maintenance side (screens 8-12). */

export type MaintenanceType = 'Preventive' | 'Corrective' | 'Inspection';
export type IssueSeverity = 'Low' | 'Medium' | 'High' | 'Critical';
export type MaintenanceIssueStatus = 'Reported' | 'UnderInspection' | 'Repairing' | 'Repaired' | 'Rejected' | 'Cancelled';
export type MaintenanceRequestStatus = 'Draft' | 'Approved' | 'InProgress' | 'Completed' | 'Rejected' | 'Cancelled';
export type MaintenanceFrequency = 'Daily' | 'Weekly' | 'Monthly' | 'Quarterly' | 'SemiAnnual' | 'Annual';
export type MaintenanceBoardColumn = 'Reported' | 'Planned' | 'InProgress' | 'Done';

export interface MaintenanceCategory {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  maintenanceType: MaintenanceType;
  isActive: boolean;
}

export interface MaintenanceIssue {
  id: number;
  issueNumber: string;
  branchId: number | null;
  branchNameAr: string | null;
  fixedAssetId: number | null;
  assetNumber: string | null;
  assetNameAr: string | null;
  deviceName: string | null;
  reportedAtUtc: string;
  reportedByUserId: number;
  description: string;
  severity: IssueSeverity;
  status: MaintenanceIssueStatus;
  notes: string | null;
  openRequestId: number | null;
}

export interface MaintenanceSparePart {
  id: number;
  itemId: number | null;
  itemNameAr: string | null;
  description: string;
  quantity: number;
  unitCost: number;
  totalCost: number;
  warehouseId: number | null;
  stockTransactionId: number | null;
  isStocked: boolean;
}

export interface MaintenanceRequestListItem {
  id: number;
  requestNumber: string;
  fixedAssetId: number;
  assetNumber: string;
  assetNameAr: string;
  categoryNameAr: string;
  status: MaintenanceRequestStatus;
  requestDate: string;
  scheduledDate: string | null;
  completedDate: string | null;
  technicianName: string | null;
  estimatedCost: number | null;
  actualCost: number;
  fromSchedule: boolean;
}

export interface MaintenanceRequestDetail {
  id: number;
  requestNumber: string;
  issueId: number | null;
  issueNumber: string | null;
  fixedAssetId: number;
  assetNumber: string;
  assetNameAr: string;
  maintenanceCategoryId: number;
  categoryNameAr: string;
  maintenanceScheduleId: number | null;
  dueDate: string | null;
  requestDate: string;
  scheduledDate: string | null;
  completedDate: string | null;
  technicianId: number | null;
  technicianName: string | null;
  supplierId: number | null;
  externalCreditAccountId: number | null;
  status: MaintenanceRequestStatus;
  approvedAtUtc: string | null;
  estimatedCost: number | null;
  laborCost: number;
  sparePartsTotalCost: number;
  actualCost: number;
  notes: string | null;
  journalEntryId: number | null;
  sparePartsJournalEntryId: number | null;
  rowVersion: string;
  spareParts: MaintenanceSparePart[];
}

export interface MaintenanceSparePartInput {
  itemId: number | null;
  warehouseId: number | null;
  description: string | null;
  quantity: number;
  unitCost: number | null;
}

export interface MaintenanceRequestInput {
  fixedAssetId: number;
  maintenanceCategoryId: number;
  requestDate: string;
  scheduledDate: string | null;
  technicianId: number | null;
  technicianName: string | null;
  supplierId: number | null;
  externalCreditAccountId: number | null;
  estimatedCost: number | null;
  laborCost: number;
  notes: string | null;
  spareParts: MaintenanceSparePartInput[];
}

export interface MaintenanceSchedule {
  id: number;
  fixedAssetId: number;
  assetNumber: string;
  assetNameAr: string;
  maintenanceCategoryId: number;
  categoryNameAr: string;
  frequency: MaintenanceFrequency;
  lastExecutedDate: string | null;
  nextDueDate: string;
  technicianId: number | null;
  technicianName: string | null;
  isActive: boolean;
  notes: string | null;
}

export interface MaintenanceBoardCard {
  kind: 'Issue' | 'Request';
  id: number;
  number: string;
  column: MaintenanceBoardColumn;
  title: string;
  assetNumber: string | null;
  assetNameAr: string | null;
  severity: IssueSeverity | null;
  technicianName: string | null;
  date: string | null;
  cost: number | null;
  status: number;
  needsApproval: boolean;
  fromSchedule: boolean;
}

const BASE = '/maintenance';

// ------------------------------------------------------------------ categories

export function useMaintenanceCategories() {
  return useQuery({
    queryKey: ['maintenance-categories'],
    queryFn: async () => (await api.get<MaintenanceCategory[]>(`${BASE}/categories`)).data
  });
}

export function useSaveMaintenanceCategory(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { code?: string; nameAr: string; nameEn: string; maintenanceType: MaintenanceType; isActive: boolean }) => {
      if (id) {
        await api.put(`${BASE}/categories/${id}`, payload);
        return { id };
      }
      return (await api.post<{ id: number }>(`${BASE}/categories`, payload)).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['maintenance-categories'] })
  });
}

export function useDeleteMaintenanceCategory() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/categories/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['maintenance-categories'] })
  });
}

// ------------------------------------------------------------------ fault reports

export function useMaintenanceIssues(status?: MaintenanceIssueStatus) {
  return useQuery({
    queryKey: ['maintenance-issues', status],
    queryFn: async () => (await api.get<MaintenanceIssue[]>(`${BASE}/issues`, { params: { status } })).data
  });
}

export function useCreateMaintenanceIssue() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      branchId: number | null;
      fixedAssetId: number | null;
      deviceName: string | null;
      description: string;
      severity: IssueSeverity;
      notes: string | null;
    }) => (await api.post<{ id: number }>(`${BASE}/issues`, payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['maintenance-issues'] });
      queryClient.invalidateQueries({ queryKey: ['maintenance-board'] });
    }
  });
}

export function useMaintenanceIssueAction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, action }: { id: number; action: 'inspect' | 'reject' | 'cancel' }) => {
      await api.post(`${BASE}/issues/${id}/${action}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['maintenance-issues'] });
      queryClient.invalidateQueries({ queryKey: ['maintenance-board'] });
    }
  });
}

// ------------------------------------------------------------------ requests

export function useMaintenanceRequests(params?: { status?: MaintenanceRequestStatus; fixedAssetId?: number }) {
  return useQuery({
    queryKey: ['maintenance-requests', params],
    queryFn: async () => (await api.get<MaintenanceRequestListItem[]>(`${BASE}/requests`, { params })).data
  });
}

export function useMaintenanceRequest(id: number | undefined) {
  return useQuery({
    queryKey: ['maintenance-requests', id],
    queryFn: async () => (await api.get<MaintenanceRequestDetail>(`${BASE}/requests/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useSaveMaintenanceRequest(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { issueId?: number | null; rowVersion?: string; data: MaintenanceRequestInput }) => {
      if (id) {
        await api.put(`${BASE}/requests/${id}`, { rowVersion: payload.rowVersion, data: payload.data });
        return { id };
      }
      return (await api.post<{ id: number }>(`${BASE}/requests`, { issueId: payload.issueId ?? null, data: payload.data })).data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['maintenance-requests'] });
      queryClient.invalidateQueries({ queryKey: ['maintenance-board'] });
    }
  });
}

export function useMaintenanceRequestAction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      action,
      completedDate
    }: {
      id: number;
      action: 'approve' | 'start' | 'complete' | 'reject' | 'cancel';
      completedDate?: string;
    }) => {
      await api.post(`${BASE}/requests/${id}/${action}`, action === 'complete' ? { completedDate } : undefined);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['maintenance-requests'] });
      queryClient.invalidateQueries({ queryKey: ['maintenance-issues'] });
      queryClient.invalidateQueries({ queryKey: ['maintenance-board'] });
      queryClient.invalidateQueries({ queryKey: ['maintenance-schedules'] });
      queryClient.invalidateQueries({ queryKey: ['fixed-assets'] });
    }
  });
}

// ------------------------------------------------------------------ preventive schedules

export function useMaintenanceSchedules() {
  return useQuery({
    queryKey: ['maintenance-schedules'],
    queryFn: async () => (await api.get<MaintenanceSchedule[]>(`${BASE}/schedules`)).data
  });
}

export function useSaveMaintenanceSchedule(id?: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      fixedAssetId: number;
      maintenanceCategoryId: number;
      frequency: MaintenanceFrequency;
      nextDueDate: string;
      technicianId: number | null;
      technicianName: string | null;
      isActive: boolean;
      notes: string | null;
    }) => {
      if (id) {
        await api.put(`${BASE}/schedules/${id}`, payload);
        return { id };
      }
      return (await api.post<{ id: number }>(`${BASE}/schedules`, payload)).data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['maintenance-schedules'] })
  });
}

export function useDeleteMaintenanceSchedule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await api.delete(`${BASE}/schedules/${id}`);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['maintenance-schedules'] })
  });
}

// ------------------------------------------------------------------ the board

export function useMaintenanceBoard(doneDays = 14) {
  return useQuery({
    queryKey: ['maintenance-board', doneDays],
    queryFn: async () => (await api.get<MaintenanceBoardCard[]>(`${BASE}/board`, { params: { doneDays } })).data
  });
}
