import { Fragment, ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { XMarkIcon } from '@heroicons/react/24/outline'
import { useI18n } from '@/i18n/I18nProvider'

interface ModalProps {
  isOpen: boolean
  onClose: () => void
  title?: string
  children: ReactNode
  className?: string
  /** Dark-glass variant for game tables (default: light card). */
  dark?: boolean
}

export function Modal({ isOpen, onClose, title, children, className = '', dark = false }: ModalProps) {
  const { t } = useI18n()
  if (!isOpen) return null

  const hasMaxWidth = /\bmax-w-/.test(className)

  return createPortal(
    <Fragment>
      <div
        className="fixed inset-0 bg-black/50 z-40 transition-opacity"
        onClick={onClose}
        aria-hidden="true"
      />
      <div className="fixed inset-0 z-50 overflow-y-auto">
        <div className="flex min-h-full items-center justify-center p-4">
          <div
            className={`w-full ${hasMaxWidth ? '' : 'max-w-lg'} rounded-xl transform transition-all ${
              dark ? `border border-sky-200/20 bg-slate-900 text-sky-50 shadow-[0_24px_70px_rgba(0,0,0,0.75)] ${className}` : `bg-white shadow-xl ${className}`
            }`}
            role="dialog"
            aria-modal="true"
            aria-labelledby={title ? 'modal-title' : undefined}
          >
            {(title) && (
              <div className={`flex items-center justify-between p-4 border-b ${dark ? 'border-sky-200/10' : ''}`}>
                {title && (
                  <h2 id="modal-title" className={`text-lg font-semibold ${dark ? 'text-sky-50' : 'text-gray-900'}`}>
                    {title}
                  </h2>
                )}
                <button
                  onClick={onClose}
                  className={`transition-colors rounded-lg p-1 ${dark ? 'text-sky-200/50 hover:bg-white/10 hover:text-white' : 'text-gray-400 hover:text-gray-600 hover:bg-gray-100'}`}
                  aria-label={t('common.closeModal')}
                >
                  <XMarkIcon className="w-5 h-5" />
                </button>
              </div>
            )}
            <div className="p-4">{children}</div>
          </div>
        </div>
      </div>
    </Fragment>,
    document.body
  )
}