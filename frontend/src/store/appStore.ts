import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import i18n from '../i18n';

export type Theme = 'espresso' | 'habbak' | 'nozomsoft' | 'sky' | 'sage' | 'windows';

/** Order/labels the theme switcher renders (00-System-Wide-Corrections-01.md-adjacent request). */
export const THEMES: { value: Theme; labelAr: string; labelEn: string; swatch: string }[] = [
  { value: 'espresso', labelAr: 'إسبريسو', labelEn: 'Espresso', swatch: '#213a56' },
  { value: 'habbak', labelAr: 'الحبّاك', labelEn: 'Al Habbak', swatch: '#1c3454' },
  { value: 'nozomsoft', labelAr: 'NozomSoft', labelEn: 'NozomSoft', swatch: '#0891b2' },
  { value: 'sky', labelAr: 'سماوي', labelEn: 'Sky', swatch: '#0277bd' },
  { value: 'sage', labelAr: 'سيدج', labelEn: 'Sage', swatch: '#3b5a4e' },
  { value: 'windows', labelAr: 'Windows Blue', labelEn: 'Windows Blue', swatch: '#0078d7' }
];

/**
 * Global UI state (00-Frontend-Specs.md, section 2): exclusively the current user/session,
 * active company/branch, UI language, and visual theme. Never used for screen-local state.
 */
/** What POST /auth/login and /auth/refresh return (Settings & Permissions, phase 2). */
export interface AuthSession {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  userId: number;
  username: string;
  fullName: string;
  preferredLanguage: 'Arabic' | 'English';
  companyId: number;
  branchId: number | null;
  roleCodes: string[];
  passwordChangeRequired: boolean;
  /** Where the user can work. A scope with a branch limits the session's data to that branch. */
  scopes: {
    companyId: number; companyNameAr: string; companyNameEn: string;
    branchId: number | null; branchNameAr: string | null; branchNameEn: string | null;
    roleInScope: string; isDefault: boolean;
  }[];
}

interface AppState {
  accessToken: string | null;
  refreshToken: string | null;
  companyId: number | null;
  userId: number | null;
  branchId: number | null;
  username: string | null;
  fullName: string | null;
  roleCodes: string[];
  passwordChangeRequired: boolean;
  scopes: AuthSession['scopes'];
  language: 'ar' | 'en';
  theme: Theme;
  setSession: (session: AuthSession) => void;
  logout: () => void;
  setLanguage: (language: 'ar' | 'en') => void;
  toggleLanguage: () => void;
  setTheme: (theme: Theme) => void;
}

/** Applies <html lang/dir> immediately (00-Frontend-Specs.md, section 10). */
function applyDocumentLanguage(language: 'ar' | 'en') {
  document.documentElement.lang = language;
  document.documentElement.dir = language === 'ar' ? 'rtl' : 'ltr';
}

/** Applies the selected theme's CSS custom properties (ui-kit/tokens.css) — omitting the
 * attribute entirely for the default إسبريسو theme avoids needing a duplicate `:root` block. */
function applyDocumentTheme(theme: Theme) {
  if (theme === 'espresso') {
    document.documentElement.removeAttribute('data-theme');
  } else {
    document.documentElement.setAttribute('data-theme', theme);
  }
}

export const useAppStore = create<AppState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      companyId: null,
      userId: null,
      branchId: null,
      username: null,
      fullName: null,
      roleCodes: [],
      passwordChangeRequired: false,
      scopes: [],
      language: 'ar',
      theme: 'espresso',
      setSession: (session) =>
        set({
          accessToken: session.accessToken,
          refreshToken: session.refreshToken,
          companyId: session.companyId,
          userId: session.userId,
          branchId: session.branchId,
          username: session.username,
          fullName: session.fullName,
          roleCodes: session.roleCodes,
          passwordChangeRequired: session.passwordChangeRequired,
          scopes: session.scopes
        }),
      logout: () =>
        set({
          accessToken: null, refreshToken: null, companyId: null, userId: null, branchId: null,
          username: null, fullName: null, roleCodes: [], passwordChangeRequired: false, scopes: []
        }),
      setLanguage: (language) => {
        void i18n.changeLanguage(language);
        applyDocumentLanguage(language);
        set({ language });
      },
      toggleLanguage: () => {
        const next = get().language === 'ar' ? 'en' : 'ar';
        get().setLanguage(next);
      },
      setTheme: (theme) => {
        applyDocumentTheme(theme);
        set({ theme });
      }
    }),
    {
      name: 'habbak-erp-session',
      // v2 = real sign-in. A session kept from the dev login (v1) carries a token with no roles, so it is
      // dropped here and the user signs in again; language and theme are kept.
      version: 2,
      migrate: (persisted, version) => {
        const state = (persisted ?? {}) as Partial<AppState>;
        if (version < 2) {
          return { language: state.language ?? 'ar', theme: state.theme ?? 'espresso' } as AppState;
        }
        return state as AppState;
      },
      onRehydrateStorage: () => (state) => {
        if (state) {
          void i18n.changeLanguage(state.language);
          applyDocumentLanguage(state.language);
          applyDocumentTheme(state.theme);
        }
      }
    }
  )
);
