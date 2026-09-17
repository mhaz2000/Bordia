import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { GlobeAltIcon, LockClosedIcon, SparklesIcon } from '@heroicons/react/24/outline'
import { BrandLogo } from './BrandLogo'
import { AmbientBackdrop } from './AmbientBackdrop'
import { LanguageSwitcher } from './LanguageSwitcher'
import { useI18n } from '@/i18n/I18nProvider'

/**
 * Shared frame for the auth screens: dark felt backdrop, brand column with
 * the platform pitch and game tiles, and a bright card for the form. RTL-safe
 * (logical paddings; the grid mirrors with the document direction).
 */
export function AuthShell({ children }: { children: ReactNode }) {
  const { t } = useI18n()

  return (
    <div className="relative min-h-screen overflow-hidden bg-slate-950">
      <AmbientBackdrop dense />
      <div className="relative z-10 flex min-h-screen flex-col">
        <div className="flex items-center justify-between px-5 pt-5 sm:px-10">
          <Link to="/" aria-label={t('common.appTitle')}>
            <BrandLogo dark />
          </Link>
          <LanguageSwitcher dark />
        </div>

        <div className="mx-auto grid w-full max-w-6xl flex-1 items-center gap-10 px-5 py-10 sm:px-10 lg:grid-cols-[1.1fr_1fr]">
          {/* Brand pitch - desktop only */}
          <div className="hidden flex-col gap-8 lg:flex">
            <div className="flex items-center gap-3">
              {[
                { g: 'from-red-500 via-orange-500 to-amber-400', n: 'UNO' },
                { g: 'from-indigo-500 via-slate-500 to-violet-500', n: 'Silver' },
                { g: 'from-emerald-500 via-teal-500 to-cyan-500', n: 'Splendor' },
                { g: 'from-sky-400 via-blue-600 to-indigo-800', n: 'Azul' },
              ].map((x, i) => (
                <div
                  key={x.n}
                  className={`flex h-20 w-32 items-center justify-center rounded-xl bg-gradient-to-br ${x.g} shadow-xl ring-1 ring-white/25 ${
                    i === 0 ? '-rotate-3' : i === 1 ? '-rotate-1' : i === 2 ? 'rotate-2' : 'rotate-5'
                  }`}
                >
                  <span className="-rotate-3 text-sm font-black tracking-widest text-white drop-shadow-lg">
                    {x.n.toUpperCase()}
                  </span>
                </div>
              ))}
            </div>

            <div>
              <h1 className="text-4xl font-black tracking-tight text-white sm:text-5xl">
                {t('site.heroTitle')}
                <span className="text-emerald-400">.</span>
              </h1>
              <p className="mt-3 max-w-md text-base leading-relaxed text-slate-300/90">{t('site.authTagline')}</p>
            </div>

            <ul className="space-y-3">
              {[
                { icon: SparklesIcon, key: 'site.feature1' },
                { icon: LockClosedIcon, key: 'site.feature2' },
                { icon: GlobeAltIcon, key: 'site.feature3' },
              ].map((f) => (
                <li key={f.key} className="flex items-center gap-3 text-sm text-slate-200">
                  <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-white/10 ring-1 ring-white/15">
                    <f.icon className="h-[18px] w-[18px] text-emerald-300" />
                  </span>
                  {t(f.key)}
                </li>
              ))}
            </ul>
          </div>

          {/* Form column */}
          <div className="mx-auto w-full max-w-md">{children}</div>
        </div>
      </div>
    </div>
  )
}
