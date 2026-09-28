import axios from 'axios';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../app/api';
import { useAppStore, type AuthSession } from '../../store/appStore';

const BASE_URL = import.meta.env.VITE_API_BASE_URL as string;

export interface ScreenRights {
  view: boolean;
  add: boolean;
  edit: boolean;
  delete: boolean;
  print: boolean;
  export: boolean;
  approve: boolean;
}

export interface MyAccess {
  userId: number;
  username: string;
  fullName: string;
  companyId: number;
  branchId: number | null;
  roleCodes: string[];
  isFullAccess: boolean;
  passwordChangeRequired: boolean;
  screens: Record<string, ScreenRights>;
  /** Screen → entity → field: the user's rights on every catalog field (FieldPermissionCatalog). */
  fieldPermissions: Record<string, Record<string, Record<string, { canView: boolean; canEdit: boolean }>>>;
  /** Screen → button → allowed, for every special button (ButtonPermissionCatalog). */
  buttonPermissions: Record<string, Record<string, boolean>>;
  scopes: AuthSession['scopes'];
}

/** What sign-in answers for a user with two-factor sign-in: the code goes with this token. */
export interface TwoFactorChallenge {
  twoFactorRequired: true;
  challengeToken: string;
  expiresAtUtc: string;
}

export const isTwoFactorChallenge = (r: AuthSession | TwoFactorChallenge): r is TwoFactorChallenge =>
  (r as TwoFactorChallenge).twoFactorRequired === true;

/** Sign-in goes through plain axios: the shared client would try to refresh on its 401. */
export async function login(username: string, password: string, companyId?: number) {
  return (await axios.post<AuthSession | TwoFactorChallenge>(`${BASE_URL}/auth/login`, { username, password, companyId })).data;
}

/** Sign-in, step two: a code from the authenticator app, or a recovery code. */
export async function loginTwoFactor(challengeToken: string, code: string | null, recoveryCode: string | null) {
  return (await axios.post<AuthSession>(`${BASE_URL}/auth/login/two-factor`, { challengeToken, code, recoveryCode })).data;
}

// ------------------------------------------------------------------ the user's own two-factor sign-in

export interface MyTwoFactor { enabled: boolean; recoveryCodesLeft: number }
export interface TwoFactorSetup { secret: string; setupUri: string }

export function useMyTwoFactor() {
  return useQuery({ queryKey: ['my-two-factor'], queryFn: async () => (await api.get<MyTwoFactor>('/auth/two-factor')).data });
}

function useTwoFactorMutation<TArgs, TResult>(fn: (args: TArgs) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({ mutationFn: fn, onSuccess: () => queryClient.invalidateQueries({ queryKey: ['my-two-factor'] }) });
}

export const useBeginTwoFactorSetup = () =>
  useTwoFactorMutation(async () => (await api.post<TwoFactorSetup>('/auth/two-factor/setup')).data);
export const useEnableTwoFactor = () =>
  useTwoFactorMutation(async (code: string) => (await api.post<{ codes: string[] }>('/auth/two-factor/enable', { code })).data.codes);
export const useDisableTwoFactor = () =>
  useTwoFactorMutation(async (args: { password: string; code: string | null; recoveryCode: string | null }) => api.post('/auth/two-factor/disable', args));
export const useRegenerateRecoveryCodes = () =>
  useTwoFactorMutation(async (code: string) => (await api.post<{ codes: string[] }>('/auth/two-factor/recovery-codes', { code })).data.codes);

export async function logout() {
  const { refreshToken } = useAppStore.getState();
  if (refreshToken) {
    try {
      await axios.post(`${BASE_URL}/auth/logout`, { refreshToken });
    } catch {
      // Ending the session locally is what matters; the token expires on its own anyway.
    }
  }
  useAppStore.getState().logout();
}

export async function changePassword(currentPassword: string, newPassword: string) {
  return (await api.post<AuthSession>('/auth/change-password', { currentPassword, newPassword })).data;
}

/** The signed-in user's rights in the current company — drives menus, buttons and screen guards. */
export function useMyAccess() {
  const companyId = useAppStore((s) => s.companyId);
  const accessToken = useAppStore((s) => s.accessToken);
  return useQuery({
    queryKey: ['my-access', companyId],
    queryFn: async () => (await api.get<MyAccess>('/auth/me')).data,
    enabled: !!accessToken,
    staleTime: 60_000
  });
}

/** The user-facing message of a failed sign-in call. */
export function authErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { message?: string; details?: { message: string }[] } | undefined;
    if (data?.details?.length) return data.details.map((d) => d.message).join(' / ');
    if (data?.message) return data.message;
  }
  return fallback;
}
