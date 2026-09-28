import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios';
import { useAppStore, type AuthSession } from '../store/appStore';
import { useToastStore } from '../store/toastStore';
import type { ApiErrorResponse, AspNetProblemDetails } from './apiTypes';

/** The one central Axios instance (00-Frontend-Specs.md, section 2/12.1) — no screen calls axios directly. */
export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL as string
});

/**
 * The screen (MenuItem code) of the open tab, sent as X-Screen-Code so the server applies that
 * screen's field and button rules — it honours only a screen the user may view. Set by AppLayout;
 * a request made from a hidden tab carries the open tab's screen.
 */
let activeScreenCode: string | null = null;

export function setActiveScreenCode(code: string | null) {
  activeScreenCode = code;
}

api.interceptors.request.use((config) => {
  const token = useAppStore.getState().accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  if (activeScreenCode) {
    config.headers['X-Screen-Code'] = activeScreenCode;
  }
  return config;
});

/**
 * Trades the refresh token for a new session (the token rotates on every call). Pass a company to
 * switch the session to it. Concurrent callers share one request, so a burst of 401s from screens
 * loading in parallel spends the refresh token once, not once per request.
 */
let refreshInFlight: Promise<AuthSession | null> | null = null;

export function refreshSession(companyId?: number | null, branchId?: number | null): Promise<AuthSession | null> {
  const { refreshToken } = useAppStore.getState();
  if (!refreshToken) return Promise.resolve(null);
  if (refreshInFlight && companyId === undefined) return refreshInFlight;

  // A plain refresh keeps the session where it is — same company, same branch.
  const state = useAppStore.getState();
  const request = axios
    .post<AuthSession>(`${import.meta.env.VITE_API_BASE_URL}/auth/refresh`, {
      refreshToken,
      companyId: companyId === undefined ? state.companyId : companyId,
      branchId: companyId === undefined ? state.branchId : branchId ?? null
    })
    .then(({ data }) => {
      useAppStore.getState().setSession(data);
      return data;
    })
    .catch(() => null)
    .finally(() => {
      if (refreshInFlight === request) refreshInFlight = null;
    });

  if (companyId === undefined) refreshInFlight = request;
  return request;
}

function endSession(target: '/login' | '/change-password') {
  if (target === '/login') useAppStore.getState().logout();
  if (window.location.pathname !== target) window.location.assign(target);
}

// The sign-in calls themselves: their 401 means wrong credentials or a dead refresh token, never an
// expired access token — so no refresh-and-retry, and the sign-in screens show the error inline.
const isAuthCall = (url?: string) => !!url && /^\/auth\/(login|refresh|logout)\b/.test(url);

/** These carry field-level details the calling form displays itself — never also a Toast (section 12.2). */
const FIELD_LEVEL_ERROR_CODES = new Set(['VALIDATION_ERROR', 'POSTING_VALIDATION_ERROR']);

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiErrorResponse | AspNetProblemDetails>) => {
    const data = error.response?.data;
    const config = error.config as (InternalAxiosRequestConfig & { _retried?: boolean }) | undefined;

    // An expired access token: refresh once and replay the request.
    if (error.response?.status === 401 && config && !config._retried && !isAuthCall(config.url)) {
      config._retried = true;
      const session = await refreshSession();
      if (session) {
        config.headers.Authorization = `Bearer ${session.accessToken}`;
        return api(config);
      }
      endSession('/login');
      return Promise.reject(error);
    }

    if (data && 'errorCode' in data && data.errorCode === 'AUTH-PASSWORD-CHANGE-REQUIRED') {
      endSession('/change-password');
      return Promise.reject(error);
    }

    // Sign-in screens show their own errors inline.
    if (isAuthCall(config?.url) && error.response?.status === 401) {
      return Promise.reject(error);
    }

    if (data && 'errorCode' in data && data.errorCode && !FIELD_LEVEL_ERROR_CODES.has(data.errorCode)) {
      useToastStore.getState().show(data.message, 'error');
    } else if (data && 'errors' in data && data.errors) {
      // Raw ASP.NET ProblemDetails — the request never reached our own validation pipeline
      // (e.g. an empty date field failing JSON conversion), so it has no errorCode/message of ours.
      const messages = Object.values(data.errors).flat();
      useToastStore.getState().show(messages.length ? messages.join(' / ') : data.title, 'error');
    } else if (!data) {
      useToastStore.getState().show('تعذّر الاتصال بالسيرفر.', 'error');
    }

    return Promise.reject(error);
  }
);

export function getApiError(error: unknown): ApiErrorResponse | undefined {
  if (axios.isAxiosError(error)) {
    return error.response?.data as ApiErrorResponse | undefined;
  }
  return undefined;
}

/**
 * Field-level errors (VALIDATION_ERROR/POSTING_VALIDATION_ERROR) are deliberately excluded from
 * the central Axios-interceptor toast above — every other error code is toasted there already.
 * A form's own catch block calls this to surface the field-level ones instead of swallowing them.
 */
export function getFieldErrorMessage(error: unknown): string | undefined {
  const apiError = getApiError(error);
  if (apiError?.details?.length) {
    return apiError.details.map((d) => d.message).join(' / ');
  }
  return apiError?.message;
}
