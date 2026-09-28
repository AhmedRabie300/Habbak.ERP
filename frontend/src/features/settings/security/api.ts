import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../../app/api';
import type { PagedResult } from '../../../app/apiTypes';

export type UserStatus = 'PendingActivation' | 'Active' | 'Suspended' | 'Locked';
export type PreferredLanguage = 'Arabic' | 'English';
export type AuditActionType = 'Create' | 'Update' | 'Delete' | 'View' | 'Login' | 'Logout' | 'Approve' | 'Reject' | 'Export' | 'Print' | 'ButtonPress';

export interface UserListItem {
  id: number;
  username: string;
  fullName: string;
  email: string;
  status: UserStatus;
  lastLoginAtUtc: string | null;
  mustChangePassword: boolean;
  twoFactorEnabled: boolean;
  roleCodes: string[];
}

export interface UserRoleRow { id?: number; roleId: number; roleCode?: string; roleNameAr?: string; roleNameEn?: string; branchId: number | null; expiresAtUtc: string | null }
export interface UserScopeRow { id?: number; companyId: number; branchId: number | null; roleInScope: string; isDefault: boolean; isActive: boolean }

export interface UserDetail {
  id: number;
  username: string;
  email: string;
  fullName: string;
  phoneNumber: string | null;
  preferredLanguage: PreferredLanguage;
  status: UserStatus;
  failedLoginAttempts: number;
  lockedUntilUtc: string | null;
  lastLoginAtUtc: string | null;
  mustChangePassword: boolean;
  passwordChangedAtUtc: string | null;
  twoFactorEnabled: boolean;
  roles: UserRoleRow[];
  scopes: UserScopeRow[];
  rowVersion: string;
}

export interface RoleListItem {
  id: number;
  code: string;
  nameAr: string;
  nameEn: string;
  description: string | null;
  isSystemRole: boolean;
  isActive: boolean;
  isFullAccess: boolean;
  userCount: number;
  screenCount: number;
}

export interface ScreenPermissionRow {
  screenCode: string;
  canView: boolean;
  canAdd: boolean;
  canEdit: boolean;
  canDelete: boolean;
  canPrint: boolean;
  canExport: boolean;
  canApprove: boolean;
}

export interface FieldPermissionRow { screenCode: string; entityType: string; fieldName: string; canView: boolean; canEdit: boolean; requiresAuditLog: boolean }
export interface ButtonPermissionRow { screenCode: string; buttonCode: string; isEnabled: boolean; requiresAuditLog: boolean }
export type ScreenActionName = 'View' | 'Add' | 'Edit' | 'Delete' | 'Print' | 'Export' | 'Approve';
/** A catalog button as a role has it — isEnabled is the effective answer (the fallback when there is no row). */
export interface RoleButtonRight {
  screenCode: string; buttonCode: string; nameAr: string; nameEn: string; fallbackAction: ScreenActionName; serverEnforced: boolean;
  isEnabled: boolean; requiresAuditLog: boolean; isConfigured: boolean;
}

export interface RoleDetail extends Omit<RoleListItem, 'userCount' | 'screenCount'> {
  screens: ScreenPermissionRow[];
  fields: FieldPermissionRow[];
  buttons: ButtonPermissionRow[];
  rowVersion: string;
}

export interface PermissionCatalog {
  screens: { code: string; nameAr: string; nameEn: string; groupNameAr: string; groupNameEn: string; routeKey: string }[];
  fieldScreens: { screenCode: string; nameAr: string; nameEn: string; fields: { entityType: string; fieldName: string }[] }[];
  buttonScreens: {
    screenCode: string; nameAr: string; nameEn: string;
    buttons: { code: string; nameAr: string; nameEn: string; fallbackAction: ScreenActionName; serverEnforced: boolean }[];
  }[];
}

export interface ActiveSession { id: number; userId: number; username: string; fullName: string; createdAtUtc: string; expiresAtUtc: string; createdByIp: string; userAgent: string | null }
export interface LoginAttemptRow { id: number; userId: number | null; username: string; success: boolean; ipAddress: string; userAgent: string | null; failureReason: string | null; attemptedAtUtc: string }
export interface AuditLogRow {
  id: number; userId: number; username: string | null; actionType: AuditActionType; entityType: string; entityId: number | null;
  fieldName: string | null; oldValue: string | null; newValue: string | null; ipAddress: string | null; occurredAtUtc: string; additionalData: string | null;
}

export interface SecuritySettings {
  passwordMinLength: number;
  passwordRequireUppercase: boolean;
  passwordRequireLowercase: boolean;
  passwordRequireDigit: boolean;
  passwordRequireSpecial: boolean;
  passwordExpiryDays: number;
  maxFailedLoginAttempts: number;
  accountLockoutMinutes: number;
  sessionTimeoutMinutes: number;
  refreshTokenExpiryDays: number;
  auditRetentionYears: number;
  rowVersion: string | null;
}

const USERS = '/settings/users';
const ROLES = '/settings/roles';

// ------------------------------------------------------------------------------------------ users

export function useUsersList(search: string, status: UserStatus | '') {
  return useQuery({
    queryKey: ['users', search, status],
    queryFn: async () => (await api.get<UserListItem[]>(USERS, { params: { search: search || undefined, status: status || undefined } })).data
  });
}

export function useUser(id: number | undefined) {
  return useQuery({
    queryKey: ['users', 'detail', id],
    queryFn: async () => (await api.get<UserDetail>(`${USERS}/${id}`)).data,
    enabled: !!id
  });
}

function useInvalidate(...keys: string[][]) {
  const queryClient = useQueryClient();
  return () => Promise.all(keys.map((queryKey) => queryClient.invalidateQueries({ queryKey })));
}

export function useCreateUser() {
  const invalidate = useInvalidate(['users']);
  return useMutation({
    mutationFn: async (payload: {
      username: string; email: string; fullName: string; phoneNumber: string | null; preferredLanguage: PreferredLanguage;
      password: string; mustChangePassword: boolean; roleIds: number[];
    }) => (await api.post<{ id: number }>(USERS, payload)).data,
    onSuccess: invalidate
  });
}

export function useUpdateUser(id: number) {
  const invalidate = useInvalidate(['users'], ['sessions']);
  return useMutation({
    mutationFn: async (payload: {
      email: string; fullName: string; phoneNumber: string | null; preferredLanguage: PreferredLanguage; status: UserStatus; mustChangePassword: boolean; rowVersion: string;
    }) => api.put(`${USERS}/${id}`, payload),
    onSuccess: invalidate
  });
}

export function useResetPassword(id: number) {
  const invalidate = useInvalidate(['users'], ['sessions']);
  return useMutation({
    mutationFn: async (payload: { newPassword: string; mustChangePassword: boolean }) => api.post(`${USERS}/${id}/reset-password`, payload),
    onSuccess: invalidate
  });
}

/** Turns off a user's two-factor sign-in (lost phone). */
export function useResetUserTwoFactor(id: number) {
  const invalidate = useInvalidate(['users']);
  return useMutation({ mutationFn: async () => api.post(`${USERS}/${id}/reset-two-factor`), onSuccess: invalidate });
}

export function useUnlockUser(id: number) {
  const invalidate = useInvalidate(['users']);
  return useMutation({ mutationFn: async () => api.post(`${USERS}/${id}/unlock`), onSuccess: invalidate });
}

export function useSetUserRoles(id: number) {
  const invalidate = useInvalidate(['users'], ['roles']);
  return useMutation({
    mutationFn: async (rows: { roleId: number; branchId: number | null; expiresAtUtc: string | null }[]) => api.put(`${USERS}/${id}/roles`, rows),
    onSuccess: invalidate
  });
}

export function useSetUserScopes(id: number) {
  const invalidate = useInvalidate(['users']);
  return useMutation({
    mutationFn: async (rows: Omit<UserScopeRow, 'id'>[]) => api.put(`${USERS}/${id}/scopes`, rows),
    onSuccess: invalidate
  });
}

// ------------------------------------------------------------------------------------------ roles

export function useRolesList() {
  return useQuery({ queryKey: ['roles'], queryFn: async () => (await api.get<RoleListItem[]>(ROLES)).data });
}

export function useRole(id: number | undefined) {
  return useQuery({ queryKey: ['roles', 'detail', id], queryFn: async () => (await api.get<RoleDetail>(`${ROLES}/${id}`)).data, enabled: !!id });
}

/** Every grantable screen with its route, and every sensitive field. Readable by any signed-in user. */
export function usePermissionCatalog(enabled = true) {
  return useQuery({
    queryKey: ['roles', 'catalog'],
    queryFn: async () => (await api.get<PermissionCatalog>(`${ROLES}/catalog`)).data,
    staleTime: 5 * 60_000,
    enabled
  });
}

export function useCreateRole() {
  const invalidate = useInvalidate(['roles']);
  return useMutation({
    mutationFn: async (payload: { code: string; nameAr: string; nameEn: string; description: string | null; isActive: boolean }) =>
      (await api.post<{ id: number }>(ROLES, payload)).data,
    onSuccess: invalidate
  });
}

export function useUpdateRole(id: number) {
  const invalidate = useInvalidate(['roles'], ['my-access']);
  return useMutation({
    mutationFn: async (payload: { nameAr: string; nameEn: string; description: string | null; isActive: boolean; rowVersion: string }) =>
      api.put(`${ROLES}/${id}`, payload),
    onSuccess: invalidate
  });
}

export function useDeleteRole() {
  const invalidate = useInvalidate(['roles']);
  return useMutation({ mutationFn: async (id: number) => api.delete(`${ROLES}/${id}`), onSuccess: invalidate });
}

export function useSetRoleScreens(id: number) {
  const invalidate = useInvalidate(['roles'], ['my-access'], ['navigation']);
  return useMutation({ mutationFn: async (rows: ScreenPermissionRow[]) => api.put(`${ROLES}/${id}/screens`, rows), onSuccess: invalidate });
}

/** Replaces the role's field rules on one screen. */
export function useSetRoleFields(id: number) {
  const invalidate = useInvalidate(['roles'], ['my-access']);
  return useMutation({
    mutationFn: async ({ screenCode, rows }: { screenCode: string; rows: FieldPermissionRow[] }) =>
      api.put(`${ROLES}/${id}/fields`, rows, { params: { screenCode } }),
    onSuccess: invalidate
  });
}

/** Every catalog button of a screen with the role's effective right on it (GET /permissions/role/{id}/buttons). */
export function useRoleButtons(roleId: number | undefined, screenCode: string) {
  return useQuery({
    queryKey: ['roles', roleId, 'buttons', screenCode],
    queryFn: async () => (await api.get<RoleButtonRight[]>(`/permissions/role/${roleId}/buttons`, { params: { screenCode } })).data,
    enabled: !!roleId && !!screenCode
  });
}

/** Replaces the role's button rules on one screen. */
export function useSetRoleButtons(id: number) {
  const invalidate = useInvalidate(['roles'], ['my-access']);
  return useMutation({
    mutationFn: async ({ screenCode, rows }: { screenCode: string; rows: ButtonPermissionRow[] }) =>
      api.put(`${ROLES}/${id}/buttons`, rows, { params: { screenCode } }),
    onSuccess: invalidate
  });
}

// -------------------------------------------------------------------------- sessions, logs, settings

export function useActiveSessions() {
  return useQuery({ queryKey: ['sessions'], queryFn: async () => (await api.get<ActiveSession[]>('/settings/sessions')).data });
}

export function useRevokeSession() {
  const invalidate = useInvalidate(['sessions']);
  return useMutation({ mutationFn: async (id: number) => api.delete(`/settings/sessions/${id}`), onSuccess: invalidate });
}

export interface LoginAttemptFilters { username: string; success: '' | 'true' | 'false'; fromUtc: string; toUtc: string; page: number }

export function useLoginAttempts(f: LoginAttemptFilters) {
  return useQuery({
    queryKey: ['login-attempts', f],
    queryFn: async () =>
      (await api.get<PagedResult<LoginAttemptRow>>('/settings/login-attempts', {
        params: { username: f.username || undefined, success: f.success || undefined, fromUtc: f.fromUtc || undefined, toUtc: f.toUtc || undefined, page: f.page, pageSize: 50 }
      })).data
  });
}

export interface AuditFilters { userId: string; entityType: string; entityId: string; actionType: AuditActionType | ''; fromUtc: string; toUtc: string; page: number }

export function useAuditLog(f: AuditFilters) {
  return useQuery({
    queryKey: ['audit-log', f],
    queryFn: async () =>
      (await api.get<PagedResult<AuditLogRow>>('/settings/audit-log', {
        params: {
          userId: f.userId || undefined, entityType: f.entityType || undefined, entityId: f.entityId || undefined,
          actionType: f.actionType || undefined, fromUtc: f.fromUtc || undefined, toUtc: f.toUtc || undefined, page: f.page, pageSize: 50
        }
      })).data
  });
}

export function useAuditEntityTypes() {
  return useQuery({ queryKey: ['audit-log', 'entity-types'], queryFn: async () => (await api.get<string[]>('/settings/audit-log/entity-types')).data });
}

export function useSecuritySettings() {
  return useQuery({ queryKey: ['security-settings'], queryFn: async () => (await api.get<SecuritySettings>('/settings/security')).data });
}

export function useUpdateSecuritySettings() {
  const invalidate = useInvalidate(['security-settings']);
  return useMutation({ mutationFn: async (payload: SecuritySettings) => api.put('/settings/security', payload), onSuccess: invalidate });
}
