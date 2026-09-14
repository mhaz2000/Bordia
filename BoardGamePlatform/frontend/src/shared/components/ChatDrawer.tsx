import { useEffect, useRef, useState } from 'react'
import { ChatBubbleLeftRightIcon, PaperAirplaneIcon, XMarkIcon } from '@heroicons/react/24/outline'
import { lobbyApi } from '@/shared/api/client'
import type { LobbyChatMessage } from '@/shared/api/lobby'
import { lobbyHub } from '@/shared/signalr/lobbyHub'
import { useI18n } from '@/i18n/I18nProvider'

interface ChatDrawerProps {
  roomId: string
  selfUserId?: string
  /**
   * True when no other owner of this screen joined the lobby hub group for
   * this room (the game page). The drawer then connects + joins on mount and
   * leaves on unmount. On the waiting-room page the page owns the lobby
   * connection, so the drawer only listens.
   */
  manageConnection?: boolean
}

/**
 * Floating room-chat button + slide-in drawer, shared by the waiting room and
 * every game. Sending goes through the lobby hub (rate-limited to 10
 * messages/minute per player server-side); the hub group broadcast delivers
 * messages to everyone, including the sender.
 */
export function ChatDrawer({ roomId, selfUserId, manageConnection = false }: ChatDrawerProps) {
  const { t, lang } = useI18n()
  const [open, setOpen] = useState(false)
  const [messages, setMessages] = useState<LobbyChatMessage[]>([])
  const [unread, setUnread] = useState(0)
  const [draft, setDraft] = useState('')
  const [error, setError] = useState('')
  const openRef = useRef(open)
  const listRef = useRef<HTMLDivElement>(null)
  openRef.current = open

  // Live messages from the lobby hub group.
  useEffect(() => {
    return lobbyHub.on('onRoomMessage', (msgRoomId, id, userId, displayName, text, sentAt) => {
      if (msgRoomId !== roomId) return
      setMessages((prev) => (prev.some((m) => m.id === id) ? prev : [...prev, { id, roomId: msgRoomId, userId, displayName, text, sentAt }]))
      if (!openRef.current) {
        setUnread((n) => n + 1)
      }
    })
  }, [roomId])

  // Own the hub group when the host page does not (game screen).
  useEffect(() => {
    if (!manageConnection) return
    let active = true
    lobbyHub
      .connect()
      .then(() => {
        if (active) {
          return lobbyHub.joinRoom(roomId)
        }
      })
      .catch((err: unknown) => console.error('[ChatDrawer] lobby hub join failed:', err))
    return () => {
      active = false
      if (lobbyHub.isConnected) {
        lobbyHub.leaveRoom(roomId).catch(() => {})
      }
    }
  }, [manageConnection, roomId])

  // (Re)load history when the drawer opens; reset the unread badge.
  useEffect(() => {
    if (!open) return
    setUnread(0)
    setError('')
    lobbyApi
      .chatHistory(roomId)
      .then(setMessages)
      .catch(() => setError(t('chat.loadFailed')))
  }, [open, roomId, t])

  // Stick to the newest message while open.
  useEffect(() => {
    if (open && listRef.current) {
      listRef.current.scrollTop = listRef.current.scrollHeight
    }
  }, [open, messages.length])

  const send = async () => {
    const text = draft.trim()
    if (!text) return
    setError('')
    try {
      await lobbyHub.sendRoomMessage(roomId, text)
      setDraft('')
    } catch (err) {
      setError(err instanceof Error && err.message ? err.message : t('chat.sendFailed'))
    }
  }

  const fmtTime = (iso: string) => {
    const d = new Date(iso)
    return Number.isNaN(d.getTime()) ? '' : d.toLocaleTimeString(lang === 'fa' ? 'fa-IR' : 'en-US', { hour: '2-digit', minute: '2-digit' })
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-label={t('chat.open')}
        className={`fixed bottom-5 end-5 z-40 flex h-14 w-14 items-center justify-center rounded-full bg-gradient-to-br from-emerald-500 to-teal-700 text-white shadow-xl ring-2 ring-white/60 transition-transform hover:scale-105 active:scale-95 ${
          open ? 'hidden' : ''
        }`}
      >
        <ChatBubbleLeftRightIcon className="h-6 w-6" />
        {unread > 0 && (
          <span className="absolute -end-1 -top-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-rose-500 px-1 text-[11px] font-black text-white ring-2 ring-white">
            {unread > 9 ? '9+' : unread}
          </span>
        )}
      </button>

      {open && (
        <div
          className="fixed inset-y-0 end-0 z-50 flex w-80 max-w-full flex-col border-s border-gray-200 bg-white shadow-2xl"
          role="dialog"
          aria-label={t('chat.title')}
        >
          <div className="flex items-center justify-between border-b border-gray-200 bg-gradient-to-r from-emerald-600 to-teal-700 px-4 py-3">
            <h2 className="flex items-center gap-2 text-sm font-bold text-white">
              <ChatBubbleLeftRightIcon className="h-4 w-4" />
              {t('chat.title')}
            </h2>
            <button
              type="button"
              onClick={() => setOpen(false)}
              aria-label={t('chat.close')}
              className="rounded-lg p-1 text-emerald-100 transition-colors hover:bg-white/15 hover:text-white"
            >
              <XMarkIcon className="h-5 w-5" />
            </button>
          </div>

          <div ref={listRef} className="flex-1 space-y-2 overflow-y-auto bg-gray-50 px-3 py-3">
            {messages.length === 0 && <p className="py-8 text-center text-xs text-gray-400">{t('chat.empty')}</p>}
            {messages.map((m) => {
              const mine = !!selfUserId && m.userId === selfUserId
              return (
                <div key={m.id} className={`flex ${mine ? 'justify-end' : 'justify-start'}`}>
                  <div
                    className={`max-w-[80%] rounded-2xl px-3 py-1.5 text-sm shadow-sm ${
                      mine
                        ? 'rounded-br-md bg-emerald-600 text-white'
                        : 'rounded-bl-md border border-gray-200 bg-white text-gray-800'
                    }`}
                  >
                    <p className={`text-[10px] font-bold ${mine ? 'text-emerald-100' : 'text-gray-400'}`}>
                      {mine ? t('chat.you') : m.displayName}
                      <span className="ms-1.5 font-normal tabular-nums opacity-70">{fmtTime(m.sentAt)}</span>
                    </p>
                    <p className="break-words whitespace-pre-wrap leading-snug">{m.text}</p>
                  </div>
                </div>
              )
            })}
          </div>

          <div className="border-t border-gray-200 p-3">
            {error && <p className="mb-2 text-xs font-medium text-rose-600">{error}</p>}
            <div className="flex items-center gap-2">
              <input
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault()
                    void send()
                  }
                }}
                maxLength={300}
                placeholder={t('chat.placeholder')}
                aria-label={t('chat.placeholder')}
                className="min-w-0 flex-1 rounded-xl border border-gray-300 bg-gray-50 px-3 py-2 text-sm focus:border-emerald-500 focus:outline-none focus:ring-1 focus:ring-emerald-500"
              />
              <button
                type="button"
                onClick={() => void send()}
                disabled={draft.trim().length === 0}
                aria-label={t('chat.send')}
                className="flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-xl bg-emerald-600 text-white shadow transition-transform enabled:hover:scale-105 enabled:hover:bg-emerald-700 disabled:opacity-40"
              >
                <PaperAirplaneIcon className="h-4 w-4" />
              </button>
            </div>
            <p className="mt-1.5 text-center text-[10px] text-gray-400">{t('chat.rateHint')}</p>
          </div>
        </div>
      )}
    </>
  )
}
