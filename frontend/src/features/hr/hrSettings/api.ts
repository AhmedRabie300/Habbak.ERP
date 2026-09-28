import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

/** Docs/Implementation/HR-MASTER-PLAN.md §Phase 1.5 Sub-Batch 1.5.0 + Phase 3 (LeaveDayCountingMode).
 * الصفحة نفسها مكانتش موجودة في الـFrontend قبل كده رغم إن الـBackend كامل — اتبنت هنا لأول مرة
 * عشان الدروب داون المطلوب في Phase 3 محتاجها. */

export type LeaveDayCountingMode = 'Calendar' | 'WorkingDays';

export interface HrSettings {
  defaultProbationDays: number;
  defaultBranchId: number | null;
  requireNationalIdForActivation: boolean;
  leaveDayCountingMode: LeaveDayCountingMode;
}

const BASE = '/hr/settings';

export function useHrSettings() {
  return useQuery({
    queryKey: ['hr-settings'],
    queryFn: async () => (await api.get<HrSettings>(BASE)).data
  });
}

export function useUpdateHrSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (data: HrSettings) => {
      await api.put(BASE, data);
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['hr-settings'] })
  });
}
