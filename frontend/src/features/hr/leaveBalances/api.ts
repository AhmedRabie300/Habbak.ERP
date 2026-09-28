import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 3 — HR_LEAVE_BALANCES. */

export type LeaveBalanceMovementType = 'Accrual' | 'Usage' | 'Reversal' | 'CarryOver' | 'Expiry' | 'CashOut' | 'Adjustment';

export interface LeaveBalance {
  id: number;
  employeeId: number;
  leaveTypeId: number;
  year: number;
  accruedThisYear: number;
  carriedOver: number;
  used: number;
  pending: number;
  available: number;
}

export interface LeaveBalanceHistoryEntry {
  id: number;
  movementType: LeaveBalanceMovementType;
  days: number;
  effectiveDate: string;
  sourceType: string | null;
  sourceId: number | null;
  reason: string | null;
}

const BASE = '/hr/leave-balances';

export function useLeaveBalances(params: { employeeId?: number; year?: number }) {
  return useQuery({
    queryKey: ['hr-leave-balances', params],
    queryFn: async () => (await api.get<LeaveBalance[]>(BASE, { params })).data
  });
}

export function useLeaveBalanceHistory(id: number | undefined) {
  return useQuery({
    queryKey: ['hr-leave-balance-history', id],
    queryFn: async () => (await api.get<LeaveBalanceHistoryEntry[]>(`${BASE}/${id}/history`)).data,
    enabled: id !== undefined
  });
}

export function useAdjustLeaveBalance() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { employeeId: number; leaveTypeId: number; year: number; days: number; reason: string }) =>
      (await api.post<{ id: number }>(`${BASE}/adjust`, data)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['hr-leave-balances'] });
      queryClient.invalidateQueries({ queryKey: ['hr-leave-balance-history'] });
    }
  });
}

export function useAccrueMonthlyLeave() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: { year: number; month: number }) => (await api.post<{ created: number }>(`${BASE}/accrue-monthly`, data)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-leave-balances'] })
  });
}
