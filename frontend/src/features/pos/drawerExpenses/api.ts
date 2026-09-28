import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { DrawerExpense } from './types';

const BASE = '/pos/drawer-expenses';

export function useDrawerExpensesList(shiftId: number | undefined) {
  return useQuery({
    queryKey: ['drawer-expenses', shiftId],
    queryFn: async () => (await api.get<DrawerExpense[]>(BASE, { params: { shiftId } })).data,
    enabled: shiftId !== undefined
  });
}

export function useCreateDrawerExpense() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: { shiftId: number; expenseAccountId: number; amount: number; description: string }) =>
      (await api.post<{ id: number }>(BASE, payload)).data,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['drawer-expenses'] })
  });
}
