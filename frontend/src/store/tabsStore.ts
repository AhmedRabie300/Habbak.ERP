import { create } from 'zustand';

export interface WorkspaceTab {
  id: string;
  path: string;
  title: string;
}

interface TabsState {
  tabs: WorkspaceTab[];
  activeTabId: string | null;
  /** Sidebar's "open a new screen" intent — finds an already-open tab for this path, else opens
   * one (My Remarks/Remarks2.md, remark 3.4). */
  openTab: (path: string, title: string) => void;
  /** Mirrors in-page navigation (row click, Back, save-and-redirect) onto the CURRENTLY ACTIVE
   * tab in place, since drilling within a screen isn't opening a new one — unless the target path
   * is already open as its own tab, in which case that tab is reused instead. */
  trackNavigation: (path: string, title: string) => void;
  closeTab: (id: string) => void;
  setActiveTab: (id: string) => void;
}

const makeId = () => `tab-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

export const useTabsStore = create<TabsState>((set, get) => ({
  tabs: [],
  activeTabId: null,

  openTab: (path, title) => {
    const existing = get().tabs.find((t) => t.path === path);
    if (existing) {
      set({ activeTabId: existing.id });
      return;
    }
    const id = makeId();
    set((state) => ({ tabs: [...state.tabs, { id, path, title }], activeTabId: id }));
  },

  trackNavigation: (path, title) => {
    const existing = get().tabs.find((t) => t.path === path);
    if (existing) {
      set({ activeTabId: existing.id });
      return;
    }
    const activeId = get().activeTabId;
    if (!activeId) {
      const id = makeId();
      set((state) => ({ tabs: [...state.tabs, { id, path, title }], activeTabId: id }));
      return;
    }
    set((state) => ({ tabs: state.tabs.map((t) => (t.id === activeId ? { ...t, path, title } : t)) }));
  },

  closeTab: (id) => {
    set((state) => {
      const idx = state.tabs.findIndex((t) => t.id === id);
      if (idx === -1) return state;
      const remaining = state.tabs.filter((t) => t.id !== id);
      if (state.activeTabId !== id) {
        return { tabs: remaining };
      }
      if (remaining.length === 0) {
        return { tabs: remaining, activeTabId: null };
      }
      const nextIdx = Math.min(idx, remaining.length - 1);
      return { tabs: remaining, activeTabId: remaining[nextIdx].id };
    });
  },

  setActiveTab: (id) => set({ activeTabId: id })
}));
