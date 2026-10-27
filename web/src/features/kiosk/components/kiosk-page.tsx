import { useQuery } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { CloudAlert, LoaderCircle, LogOut, ShieldAlert, WifiOff } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { DEFAULT_LOCALE } from '@/shared/i18n/locales'
import { Button } from '@/shared/ui'
import {
  KIOSK_LANGUAGES,
  dropHrSession,
  fetchKioskSession,
  hasHrSession,
  kioskFailure,
  type KioskSession,
} from '../api/kiosk-api'
import { draftStorage, emptyDraft, type KioskDraft } from '../lib/kiosk-draft'
import { resolveKioskRules } from '../lib/kiosk-mode'
import { isFormOnlySite } from '../lib/kiosk-site'
import { useIdle } from '../lib/use-idle'
import { KioskDone } from './kiosk-done'
import { KioskStart } from './kiosk-start'
import { KioskWizard } from './kiosk-wizard'

/** Vacancies are refreshed this often while the start screen is open. */
const SESSION_REFRESH_MS = 10 * 60 * 1000

type Phase =
  | { name: 'start' }
  | { name: 'wizard'; draft: KioskDraft }
  | { name: 'done'; formNumber: number }

/**
 * `/anketa` — the public HR-F02 form (SPEC §7): start screen, wizard,
 * «Rahmat!». No login and no key; the device decides between the personal and
 * the tablet rules (`resolveKioskRules`).
 */
export function KioskPage({
  tabletSwitch,
  vacancyId,
}: {
  /** `?planshet=1` / `?planshet=0` — turns tablet mode on or off on this device. */
  tabletSwitch?: boolean | undefined
  /** `?vakansiya=<id>` — preselected when it is in the session's list. */
  vacancyId?: string | undefined
}) {
  const { locale, setLocale } = useLocale()

  const [rules] = useState(() => resolveKioskRules(tabletSwitch))
  const [phase, setPhase] = useState<Phase>({ name: 'start' })
  const [savedDraft, setSavedDraft] = useState(() => draftStorage.load(rules.draftMaxAgeMs))
  const [hrSession, setHrSession] = useState(() => rules.tablet && hasHrSession())

  usePhoneViewport()

  // The form speaks the three languages of the paper blank; English is HR-only.
  useEffect(() => {
    if (!(KIOSK_LANGUAGES as readonly string[]).includes(locale)) setLocale(DEFAULT_LOCALE)
  }, [locale, setLocale])

  const session = useQuery({
    queryKey: ['kiosk-session'],
    queryFn: ({ signal }) => fetchKioskSession(signal),
    retry: false,
    staleTime: SESSION_REFRESH_MS,
    refetchInterval: phase.name === 'start' ? SESSION_REFRESH_MS : false,
  })

  // Tablet mode: a draft nobody resumes is dropped — the next person must not be able to open it.
  useIdle(
    rules.startIdleMs ?? 0,
    () => {
      draftStorage.clear()
      setSavedDraft(null)
    },
    rules.startIdleMs !== null && phase.name === 'start' && savedDraft !== null,
  )

  const reset = useCallback(() => {
    draftStorage.clear()
    setSavedDraft(null)
    if (rules.tablet) setLocale(DEFAULT_LOCALE)
    setPhase({ name: 'start' })
    window.scrollTo({ top: 0 })
  }, [rules.tablet, setLocale])

  // No HR session may live in the office tablet's browser: the form does not
  // start until it is ended (security review M5). Asked, not done silently.
  if (hrSession) {
    return (
      <HrSessionNotice
        onContinue={() => {
          dropHrSession()
          setHrSession(false)
        }}
      />
    )
  }

  if (session.data === undefined) {
    return <Waiting error={session.isError ? session.error : null} onRetry={() => void session.refetch()} />
  }

  if (phase.name === 'wizard') {
    return (
      <KioskWizard
        session={session.data}
        initialDraft={phase.draft}
        rules={rules}
        onDone={(result) => {
          draftStorage.clear()
          setSavedDraft(null)
          setPhase({ name: 'done', formNumber: result.formNumber })
          window.scrollTo({ top: 0 })
        }}
        onCancel={reset}
      />
    )
  }

  if (phase.name === 'done') {
    return <KioskDone formNumber={phase.formNumber} returnAfter={rules.doneReturnSeconds} onFinish={reset} />
  }

  const preset = presetVacancy(session.data, vacancyId)

  return (
    <KioskStart
      tablet={rules.tablet}
      hasDraft={savedDraft !== null}
      noVacancies={session.data.vacancies.length === 0 && !session.data.generalPoolAvailable}
      presetVacancy={preset?.title ?? null}
      onStart={() => {
        draftStorage.clear()
        setSavedDraft(null)
        setPhase({ name: 'wizard', draft: { ...emptyDraft(), vacancy: preset?.id ?? '' } })
      }}
      onResume={() => {
        // Re-read: the draft may have aged past its limit while the start screen was open.
        const fresh = draftStorage.load(rules.draftMaxAgeMs)
        setSavedDraft(fresh)
        if (fresh !== null) setPhase({ name: 'wizard', draft: fresh })
      }}
    />
  )
}

function presetVacancy(session: KioskSession, id: string | undefined): { id: string; title: string } | null {
  if (id === undefined) return null
  return session.vacancies.find((vacancy) => vacancy.id === id) ?? null
}

/**
 * The form is used on phones: let the page reach under the notch and the home
 * indicator (`viewport-fit=cover`) — the pinned header and footer pad
 * themselves with `env(safe-area-inset-*)` — and keep fields scrolled into
 * view clear of them (`scroll-padding`). Only while `/anketa` is open; the HR
 * app keeps its own viewport.
 */
function usePhoneViewport(): void {
  useEffect(() => {
    const meta = document.querySelector<HTMLMetaElement>('meta[name="viewport"]')
    const previous = meta?.getAttribute('content') ?? null
    if (meta && previous !== null && !previous.includes('viewport-fit')) {
      meta.setAttribute('content', `${previous}, viewport-fit=cover`)
    }

    const root = document.documentElement
    root.classList.add('scroll-pt-32', 'scroll-pb-28')

    return () => {
      if (meta && previous !== null) meta.setAttribute('content', previous)
      root.classList.remove('scroll-pt-32', 'scroll-pb-28')
    }
  }, [])
}

/**
 * Shown only while an HR session lives in this browser (tablet mode). The way
 * back to the HR app is offered on the HR site only: on the form-only site
 * (SPEC §2.3 item 1) there is no HR route to go to — and no HR session can
 * exist there in the first place (different origin).
 */
function HrSessionNotice({ onContinue }: { onContinue: () => void }) {
  const { t } = useLocale()
  const navigate = useNavigate()
  const [formOnly] = useState(isFormOnlySite)

  return (
    <div className="grid min-h-dvh place-items-center bg-[var(--bg)] px-6 text-center">
      <div className="flex max-w-md flex-col items-center">
        <ShieldAlert className="size-12 text-ios-orange" strokeWidth={1.6} />
        <p className="mt-5 text-title3 font-semibold">{t('kiosk.hrSession.title')}</p>
        <p className="mt-2 text-body text-[var(--fg-secondary)]">{t('kiosk.hrSession.text')}</p>
        <Button type="button" variant="primary" size="lg" pill onClick={onContinue} className="mt-6 h-14 w-full">
          <LogOut />
          {t('kiosk.hrSession.continue')}
        </Button>
        {!formOnly && (
          <Button type="button" variant="ghost" size="lg" pill onClick={() => void navigate({ to: '/' })} className="mt-2 h-12 w-full">
            {t('kiosk.hrSession.back')}
          </Button>
        )}
      </div>
    </div>
  )
}

/** Loading, and the two ways the session can fail: no connection, or the server said no. */
function Waiting({ error, onRetry }: { error: Error | null; onRetry: () => void }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  if (error === null) {
    return (
      <div className="grid min-h-dvh place-items-center bg-[var(--bg)]">
        <LoaderCircle aria-label={t('common.loading')} className="size-10 animate-spin text-[var(--fg-tertiary)]" />
      </div>
    )
  }

  const failure = kioskFailure(error)
  const offline = failure.kind === 'offline'
  const Icon = offline ? WifiOff : CloudAlert

  return (
    <div className="grid min-h-dvh place-items-center bg-[var(--bg)] px-6 text-center">
      <div className="flex max-w-md flex-col items-center">
        <Icon className="size-12 text-[var(--fg-tertiary)]" strokeWidth={1.6} />
        <p className="mt-5 text-title3 font-semibold">
          {offline ? t('kiosk.offline.title') : t('kiosk.unavailable.title')}
        </p>
        <p className="mt-2 text-body text-[var(--fg-secondary)]">
          {failure.kind === 'offline'
            ? t('kiosk.offline.text')
            : errorMessage(failure.code, t('kiosk.unavailable.text'))}
        </p>
        <Button type="button" variant="primary" size="lg" pill onClick={onRetry} className="mt-6 h-14 min-w-48">
          {t('kiosk.offline.retry')}
        </Button>
      </div>
    </div>
  )
}
