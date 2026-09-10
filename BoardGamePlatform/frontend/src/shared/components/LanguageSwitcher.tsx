import { LANGUAGES, useI18n } from '@/i18n/I18nProvider'

/** Compact EN / فارسی toggle for page headers. */
export function LanguageSwitcher({ dark = false }: { dark?: boolean }) {
  const { lang, setLanguage, t } = useI18n()

  return (
    <div
      className={`inline-flex items-center rounded-full border p-0.5 text-xs font-semibold ${
        dark ? 'border-white/25 bg-white/10 backdrop-blur-sm' : 'border-gray-200 bg-gray-50'
      }`}
      role="group"
      aria-label={t('common.language')}
    >
      {LANGUAGES.map((l) => (
        <button
          key={l.code}
          type="button"
          onClick={() => setLanguage(l.code)}
          className={`rounded-full px-2.5 py-1 transition-colors ${
            lang === l.code
              ? dark
                ? 'bg-white text-gray-900 shadow'
                : 'bg-white text-blue-600 shadow ring-1 ring-gray-200'
              : dark
                ? 'text-white/70 hover:text-white'
                : 'text-gray-500 hover:text-gray-800'
          }`}
        >
          {l.nativeLabel}
        </button>
      ))}
    </div>
  )
}
