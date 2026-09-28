import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '../../app/api';
import type { PagedResult } from '../../app/apiTypes';
import type { Notification, NotificationFilter } from './types';

const BASE = '/notifications';

// Docs/Implementation/Phase-2.5-Research.md §1.5 — Polling, no SignalR in this project.
const PENDING_ACTION_POLL_MS = 30_000;

export function usePendingActionCount() {
  return useQuery({
    queryKey: ['notifications', 'pending-action-count'],
    queryFn: async () => (await api.get<{ count: number }>(`${BASE}/pending-action-count`)).data.count,
    refetchInterval: PENDING_ACTION_POLL_MS
  });
}

export function useUnreadCount() {
  return useQuery({
    queryKey: ['notifications', 'unread-count'],
    queryFn: async () => (await api.get<{ count: number }>(`${BASE}/unread-count`)).data.count,
    refetchInterval: PENDING_ACTION_POLL_MS
  });
}

export function useMyNotifications(filter: NotificationFilter, page: number, pageSize = 25) {
  return useQuery({
    queryKey: ['notifications', 'list', filter, page, pageSize],
    queryFn: async () => (await api.get<PagedResult<Notification>>(BASE, { params: { filter, page, pageSize } })).data
  });
}

/** The bell's own dropdown — latest 10, fetched only while it's open (not polled). */
export function useRecentNotifications(enabled: boolean) {
  return useQuery({
    queryKey: ['notifications', 'recent'],
    queryFn: async () => (await api.get<PagedResult<Notification>>(BASE, { params: { filter: 'All', page: 1, pageSize: 10 } })).data,
    enabled
  });
}

function useInvalidateNotifications() {
  const queryClient = useQueryClient();
  return () => queryClient.invalidateQueries({ queryKey: ['notifications'] });
}

export function useMarkNotificationAsRead() {
  const invalidate = useInvalidateNotifications();
  return useMutation({
    mutationFn: async (id: number) => api.post(`${BASE}/${id}/read`),
    onSuccess: invalidate
  });
}

export function useMarkAllNotificationsAsRead() {
  const invalidate = useInvalidateNotifications();
  return useMutation({
    mutationFn: async () => api.post(`${BASE}/read-all`),
    onSuccess: invalidate
  });
}

export function useDeleteNotification() {
  const invalidate = useInvalidateNotifications();
  return useMutation({
    mutationFn: async (id: number) => api.delete(`${BASE}/${id}`),
    onSuccess: invalidate
  });
}
