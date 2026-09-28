import { actionAllowed, useScreenRights } from '../features/auth/access';

/**
 * Whether the current user may run an action on the current screen (Settings & Permissions,
 * phase 2): 'view' | 'add' | 'edit' | 'delete' | 'print' | 'export' | 'approve', or an action key
 * such as 'post' or 'save' mapped the way the server maps it. Hiding is a convenience — the API
 * refuses the call regardless.
 */
export function usePermission(permissionKey: string): boolean {
  return actionAllowed(useScreenRights(), permissionKey, undefined, 'primary');
}
