import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { api } from '../../../app/api';

export interface UserLookup { id: number; username: string; fullName: string; isActive: boolean }

/** Everyone with access to the current company, for names and pickers. Any signed-in user may read it. */
export function useUsersLookup() {
  return useQuery({
    queryKey: ['users-lookup'],
    queryFn: async () => (await api.get<UserLookup[]>('/settings/users-lookup')).data,
    staleTime: 5 * 60_000
  });
}

/** A user id shown as the person's name — "the system" for 0, the bare id while loading or if unknown. */
export function UserName({ id }: { id: number | null | undefined }) {
  const { t } = useTranslation();
  const { data } = useUsersLookup();
  if (id === null || id === undefined) return <>—</>;
  if (id === 0) return <>{t('security.systemUser')}</>;
  return <>{data?.find((u) => u.id === id)?.fullName ?? `#${id}`}</>;
}

/** Same as UserName, as a string (grid export values). */
export function useUserNameOf() {
  const { t } = useTranslation();
  const { data } = useUsersLookup();
  return (id: number | null | undefined) =>
    id === null || id === undefined ? '' : id === 0 ? t('security.systemUser') : data?.find((u) => u.id === id)?.fullName ?? `#${id}`;
}
