import { create } from 'zustand'

export type ToastTone = 'info' | 'success' | 'warning' | 'error'

export interface Toast {
  id: number
  text: string
  tone: ToastTone
}

interface ToastState {
  toasts: Toast[]
  push: (text: string, tone?: ToastTone) => void
  dismiss: (id: number) => void
}

let nextId = 1

/**
 * Minimal global toast queue; entries auto-dismiss. Game views push results
 * (e.g. the outcome of a UNO Wild Draw Four challenge) so every seat sees the
 * consequence immediately instead of digging through the event log.
 */
export const useToastStore = create<ToastState>((set) => ({
  toasts: [],

  push: (text, tone = 'info') => {
    const id = nextId++
    set((state) => ({ toasts: [...state.toasts, { id, text, tone }].slice(-4) }))
    window.setTimeout(() => {
      set((state) => ({ toasts: state.toasts.filter((toast) => toast.id !== id) }))
    }, 5000)
  },

  dismiss: (id) => set((state) => ({ toasts: state.toasts.filter((toast) => toast.id !== id) })),
}))
