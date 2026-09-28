import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface CustodyListItem {
  id: number;
  employeeId: number;
  amount: number;
  issueDate: string;
  status: string;
}

export interface CustodySettlementLine {
  accountId: number;
  amount: number;
  description?: string;
}

export interface CustodySettlement {
  id: number;
  settlementDate: string;
  remainingAmount: number;
  journalEntryId?: number;
  lines: CustodySettlementLine[];
}

export interface CustodyDetail extends CustodyListItem {
  branchId?: number;
  journalEntryId?: number;
  settlements: CustodySettlement[];
}

const BASE = '/accounting/custody-registers';

export function useCustodyList() {
  return useQuery({ queryKey: ['custody'], queryFn: async () => (await api.get<CustodyListItem[]>(BASE)).data });
}

export function useCustody(id: number | undefined) {
  return useQuery({
    queryKey: ['custody', id],
    queryFn: async () => (await api.get<CustodyDetail>(`${BASE}/${id}`)).data,
    enabled: id !== undefined
  });
}

export function useCreateCustody() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      employeeId: number; branchId?: number; amount: number; issueDate: string;
      treasuryAccountId: number; custodyReceivableAccountId: number;
    }) => (await api.post<number>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['custody'] })
  });
}

export function useCreateCustodySettlement(custodyId: number) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: {
      settlementDate: string; custodyReceivableAccountId: number; treasuryAccountId: number;
      lines: CustodySettlementLine[];
    }) => (await api.post<{ id: number }>(`${BASE}/${custodyId}/settlements`, payload)).data,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['custody', custodyId] });
      queryClient.invalidateQueries({ queryKey: ['custody'] });
    }
  });
}
