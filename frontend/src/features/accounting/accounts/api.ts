import { useQuery } from '@tanstack/react-query';
import { api } from '../../../app/api';

export interface AccountOption {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  accountType: string;
  nature: string;
  isPostable: boolean;
}

export function useAccountsList(postableOnly = false) {
  return useQuery({
    queryKey: ['accounts', { postableOnly }],
    queryFn: async () => {
      const { data } = await api.get<AccountOption[]>('/accounting/accounts', {
        params: { postableOnly: postableOnly || undefined }
      });
      return data;
    }
  });
}
