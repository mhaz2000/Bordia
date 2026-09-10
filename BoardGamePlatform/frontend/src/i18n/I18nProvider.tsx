import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { en, type Dict } from './locales/en'
import { fa } from './locales/fa'

export type Language = 'en' | 'fa'

const STORAGE_KEY = 'bgp.lang'

export const LANGUAGES: { code: Language; label: string; nativeLabel: string }[] = [
  { code: 'en', label: 'English', nativeLabel: 'English' },
  { code: 'fa', label: 'Farsi', nativeLabel: 'فارسی' },
]

const DICTS: Record<Language, Dict> = { en, fa }

/** Right-to-left locales get dir="rtl" on <html> plus the Farsi webfont. */
export function isRtl(lang: Language): boolean {
  return lang === 'fa'
}

type TFunc = (key: string, params?: Record<string, string | number>) => string

interface I18nContextValue {
  lang: Language
  dir: 'ltr' | 'rtl'
  setLanguage: (lang: Language) => void
  t: TFunc
  /** The active dictionary as a typed object, for array/structured content. */
  d: Dict
}

const I18nContext = createContext<I18nContextValue | null>(null)

/** Module-level accessor for code outside the React tree (API client). */
function activeTImpl(key: string, params?: Record<string, string | number>): string {
  let text = resolve(en, key) ?? key
  if (params) {
    for (const [name, value] of Object.entries(params)) {
      text = text.split(`{${name}}`).join(String(value))
    }
  }
  return text
}
let activeT: TFunc = activeTImpl
let activeLang: Language =
  localStorage.getItem(STORAGE_KEY) === 'fa' ? 'fa' : 'en'

export function getActiveT(): TFunc {
  return activeT
}

/** The language the API client sends as X-Language so the backend localizes
 *  error responses with the app's selection, not the browser's header. */
export function getActiveLang(): Language {
  return activeLang
}

function resolve(dict: Dict, key: string): string | undefined {
  const parts = key.split('.')
  let cur: unknown = dict
  for (const part of parts) {
    if (cur === null || typeof cur !== 'object') return undefined
    cur = (cur as Record<string, unknown>)[part]
  }
  return typeof cur === 'string' ? cur : undefined
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Language>(() => {
    const saved = localStorage.getItem(STORAGE_KEY)
    return saved === 'fa' || saved === 'en' ? saved : 'en'
  })

  const dir: 'ltr' | 'rtl' = isRtl(lang) ? 'rtl' : 'ltr'

  // Reflect language onto <html> so the whole document flips direction,
  // toggle the Farsi webfont class, and localize the browser tab title.
  useEffect(() => {
    document.documentElement.lang = lang
    document.documentElement.dir = dir
    document.documentElement.classList.toggle('lang-fa', lang === 'fa')
    document.title = resolve(DICTS[lang], 'common.appTitle') ?? 'Board Game Platform'
    localStorage.setItem(STORAGE_KEY, lang)
  }, [lang, dir])

  const t = useMemo<TFunc>(() => {
    return (key, params) => {
      let text = resolve(DICTS[lang], key) ?? resolve(en, key) ?? key
      if (params) {
        for (const [name, value] of Object.entries(params)) {
          text = text.split(`{${name}}`).join(String(value))
        }
      }
      return text
    }
  }, [lang])

  // Keep the module-level accessors in sync so non-React callers (the API client)
  // translate and localize requests with the active language too.
  activeT = t
  activeLang = lang

  const value = useMemo<I18nContextValue>(() => ({ lang, dir, setLanguage: setLangState, t, d: DICTS[lang] }), [lang, dir, t])

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}

export function useI18n(): I18nContextValue {
  const ctx = useContext(I18nContext)
  if (!ctx) throw new Error('useI18n must be used inside I18nProvider')
  return ctx
}
