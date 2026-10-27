import { BriefcaseBusiness, CircleAlert, ClipboardPen, History, Tablet } from 'lucide-react'

import { useLocale } from '@/shared/i18n/use-locale'
import type { LocaleCode } from '@/shared/i18n/locales'
import { cn } from '@/shared/lib/cn'
import { BonviLogo, Button } from '@/shared/ui'
import { KIOSK_LANGUAGES } from '../api/kiosk-api'

const LANGUAGE_LABELS: Readonly<Record<(typeof KIOSK_LANGUAGES)[number], string>> = {
  'uz-Latn': 'O‘zbekcha',
  'uz-Cyrl': 'Ўзбекча',
  ru: 'Русский',
}

/**
 * First screen: one big button and the language choice. A draft left by a
 * reload can be resumed. With no accepting vacancy and the general pool closed
 * the server would refuse every form, so the start button is disabled and the
 * candidate is sent to HR. In tablet mode a small label at the bottom lets HR
 * see the switch is on.
 */
export function KioskStart({
  tablet,
  hasDraft,
  noVacancies,
  presetVacancy,
  onStart,
  onResume,
}: {
  tablet: boolean
  hasDraft: boolean
  noVacancies: boolean
  /** Title of the vacancy from `?vakansiya=` — shown so the candidate knows what they apply for. */
  presetVacancy: string | null
  onStart: () => void
  onResume: () => void
}) {
  const { t, locale, setLocale } = useLocale()

  return (
    <div className="relative flex min-h-dvh flex-col items-center justify-center overflow-hidden bg-[var(--bg)] px-5 pt-[max(2.5rem,env(safe-area-inset-top))] pb-[max(2.5rem,env(safe-area-inset-bottom))]">
      <div aria-hidden className="pointer-events-none absolute inset-0 overflow-hidden">
        <div className="absolute -top-48 left-1/2 size-[640px] -translate-x-1/2 rounded-full bg-brand-700 opacity-[0.08] blur-[130px]" />
        <div className="absolute -right-40 -bottom-56 size-[520px] rounded-full bg-ios-blue opacity-[0.06] blur-[130px]" />
      </div>

      <main className="relative z-10 flex w-full max-w-md flex-col items-center text-center">
        <BonviLogo height={44} className="text-brand-700 dark:text-brand-300" />

        <h1 className="mt-8 text-largetitle leading-tight font-semibold tracking-tight">{t('kiosk.start.title')}</h1>
        <p className="mt-3 text-body text-[var(--fg-secondary)]">{t('kiosk.start.subtitle')}</p>

        {presetVacancy !== null && (
          <p className="mt-5 inline-flex max-w-full items-center gap-2 rounded-full bg-[var(--bg-elevated)] px-4 py-2 text-callout font-medium shadow-card">
            <BriefcaseBusiness className="size-[18px] shrink-0 text-[var(--accent-text)]" />
            <span className="truncate">{t('kiosk.start.vacancy', { title: presetVacancy })}</span>
          </p>
        )}

        <div role="radiogroup" aria-label={t('common.language')} className="mt-8 grid w-full grid-cols-3 gap-2.5">
          {KIOSK_LANGUAGES.map((code) => {
            const active = code === locale
            return (
              <button
                key={code}
                type="button"
                role="radio"
                aria-checked={active}
                onClick={() => setLocale(code as LocaleCode)}
                className={cn(
                  'h-14 rounded-2xl border text-callout font-semibold transition-colors',
                  active
                    ? 'border-[var(--accent)] bg-[var(--accent-soft)] text-[var(--accent-on-soft)] ring-1 ring-[var(--accent)]'
                    : 'border-[var(--border-strong)] bg-[var(--bg-elevated)] active:bg-[var(--bg-active)]',
                )}
              >
                {LANGUAGE_LABELS[code]}
              </button>
            )
          })}
        </div>

        <Button
          type="button"
          variant="primary"
          size="lg"
          pill
          disabled={noVacancies}
          onClick={onStart}
          className="mt-5 h-16 w-full text-title3 font-semibold [&>svg]:size-6"
        >
          <ClipboardPen />
          {t('kiosk.start.begin')}
        </Button>

        {noVacancies && (
          <p role="status" className="mt-4 flex items-center gap-2 text-left text-callout text-[var(--fg-secondary)]">
            <CircleAlert className="size-5 shrink-0 text-ios-orange" />
            {t('kiosk.start.noVacancies')}
          </p>
        )}

        {hasDraft && !noVacancies && (
          <Button type="button" variant="soft" size="lg" pill onClick={onResume} className="mt-3 h-14 w-full text-callout">
            <History />
            {t('kiosk.start.resume')}
          </Button>
        )}

        <p className="mt-7 text-subhead text-[var(--fg-secondary)]">{t('kiosk.start.duration')}</p>
        <p className="mt-1.5 text-subhead text-[var(--fg-tertiary)]">{t('kiosk.start.requiredNote')}</p>
      </main>

      {tablet && (
        <p className="absolute bottom-[max(1.25rem,env(safe-area-inset-bottom))] z-10 flex items-center gap-1.5 text-caption1 text-[var(--fg-tertiary)]">
          <Tablet className="size-3.5" />
          {t('kiosk.start.tabletMode')}
        </p>
      )}
    </div>
  )
}
