import { create } from 'zustand';

export interface ToastMessage {
  id: number;
  variant: 'error' | 'success' | 'info';
  message: string;
}

interface ToastState {
  toasts: ToastMessage[];
  show: (message: string, variant?: ToastMessage['variant']) => void;
  dismiss: (id: number) => void;
}

let nextId = 1;

/**
 * Toast is the single default pattern for transient status messages
 * (00-Frontend-Specs.md, section 3.3/12.2).
 */
export const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  show: (message, variant = 'info') =>
    set((state) => {
      const id = nextId++;
      setTimeout(() => {
        useToastStore.getState().dismiss(id);
      }, 5000);
      return { toasts: [...state.toasts, { id, variant, message }] };
    }),
  dismiss: (id) => set((state) => ({ toasts: state.toasts.filter((t) => t.id !== id) }))
}));
