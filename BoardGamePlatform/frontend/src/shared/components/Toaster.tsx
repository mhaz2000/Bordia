import { CheckCircleIcon, ExclamationCircleIcon, ExclamationTriangleIcon, InformationCircleIcon, XMarkIcon } from '@heroicons/react/24/outline'
import { useToastStore, type ToastTone } from '@/shared/state/toastStore'

const TONE_STYLES: Record<ToastTone, string> = {
  info: 'bg-slate-800 text-white ring-slate-500/40',
  success: 'bg-emerald-600 text-white ring-emerald-300/40',
  warning: 'bg-amber-500 text-amber-950 ring-amber-300/60',
  error: 'bg-rose-600 text-white ring-rose-300/40',
}

const TONE_ICONS: Record<ToastTone, typeof InformationCircleIcon> = {
  info: InformationCircleIcon,
  success: CheckCircleIcon,
  warning: ExclamationTriangleIcon,
  error: ExclamationCircleIcon,
}

/** Bottom-corner toast stack; mount once inside the game shell. */
export function Toaster() {
  const toasts = useToastStore((s) => s.toasts)
  const dismiss = useToastStore((s) => s.dismiss)

  if (toasts.length === 0) return null

  return (
    <div className="pointer-events-none fixed bottom-4 end-4 z-[60] flex w-[min(92vw,22rem)] flex-col gap-2">
      {toasts.map((toast) => {
        const Icon = TONE_ICONS[toast.tone]
        return (
          <div
            key={toast.id}
            role="status"
            className={`animate-toast-in pointer-events-auto flex items-start gap-2.5 rounded-2xl px-4 py-3 shadow-xl ring-1 backdrop-blur-sm ${TONE_STYLES[toast.tone]}`}
          >
            <Icon className="mt-0.5 h-5 w-5 shrink-0" />
            <p className="min-w-0 flex-1 text-sm font-semibold leading-snug">{toast.text}</p>
            <button
              type="button"
              onClick={() => dismiss(toast.id)}
              className="shrink-0 rounded-lg p-0.5 opacity-70 transition hover:opacity-100"
              aria-label="close"
            >
              <XMarkIcon className="h-4 w-4" />
            </button>
          </div>
        )
      })}
    </div>
  )
}
