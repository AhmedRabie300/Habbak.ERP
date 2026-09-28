import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface AccountMapping {
  role: string;
  expectedAccountType: string;
  accountId: number | null;
  accountCode: string | null;
  accountNameAr: string | null;
}

const URL = '/accounting/settings/account-mappings';
const KEY = ['accounting-settings', 'account-mappings'];

export function useAccountMappings() {
  return useQuery({
    queryKey: KEY,
    queryFn: async () => (await api.get<AccountMapping[]>(URL)).data
  });
}

/** Partial by design: only the roles in `mappings` change, the rest stay as they were. */
export function useUpdateAccountMappings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (mappings: { role: string; accountId: number | null }[]) => api.put(URL, { mappings }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: KEY })
  });
}
