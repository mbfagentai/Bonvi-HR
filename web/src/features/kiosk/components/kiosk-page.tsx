import { useQuery } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { CloudAlert, LoaderCircle, LogOut, ShieldAlert, WifiOff } from 'lucide-react'
import { useCallback, useEffect, useMemo, useReducer, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { DEFAULT_LOCALE } from '@/shared/i18n/locales'
import { Button } from '@/shared/ui'
import {
  KIOSK_FORM_VERSION,
  KIOSK_LANGUAGES,
  dropHrSession,
  fetchKioskSession,
  hasHrSession,
  kioskFailure,
  type KioskSession,
} from '../api/kiosk-api'
import { candidateFields, type KioskDefinition } from '../dynamic/definition'
import { dynamicDraftStorage, emptyDynamicDraft, type DynamicDraft } from '../dynamic/dynamic-draft'
import { DynamicWizard } from '../dynamic/dynamic-wizard'
import { draftStorage, emptyDraft, type KioskDraft } from '../lib/kiosk-draft'
import { resolveKioskRules } from '../lib/kiosk-mode'
import { KioskListsContext, applyKioskTexts, restoreKioskTexts } from '../lib/kiosk-texts'
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
  /** The constructor's form (SPEC-anketa-konstruktori §7), pinned for the whole walk. */
  | { name: 'dynamic'; definition: KioskDefinition; draft: DynamicDraft }
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
  /** The constructor form's saved draft — stored under its version, so read once the version is known. */
  const [dynamicSaved, setDynamicSaved] = useState<{ version: number; draft: DynamicDraft | null } | null>(null)

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

  // A published constructor form replaces the built-in one; the definition is data, so a new
  // version needs no reload — the next «Boshlash» takes it (the walk in progress keeps its own).
  const definition = session.data?.definition ?? null
  if (definition !== null && dynamicSaved?.version !== definition.version) {
    setDynamicSaved({ version: definition.version, draft: dynamicDraftStorage.load(definition.version, rules.draftMaxAgeMs) })
  }
  const savedDynamic = definition !== null && dynamicSaved?.version === definition.version ? dynamicSaved.draft : null

  const dropDrafts = useCallback(() => {
    draftStorage.clear()
    setSavedDraft(null)
    dynamicDraftStorage.clear()
    setDynamicSaved((current) => (current === null ? null : { ...current, draft: null }))
  }, [])

  // Tablet mode: a draft nobody resumes is dropped — the next person must not be able to open it.
  useIdle(
    rules.startIdleMs ?? 0,
    dropDrafts,
    rules.startIdleMs !== null && phase.name === 'start' && (savedDraft !== null || savedDynamic !== null),
  )

  const texts = session.data?.texts ?? null
  const lists = useMemo(() => texts?.lists ?? {}, [texts])

  /*
    HR's texts go over the bundle WHILE rendering (idempotent per revision),
    so the first frame already shows them; the effect covers a remount after
    a cleanup (StrictMode) and puts the bundle back when the form is left.
  */
  applyKioskTexts(texts)
  const [, rerender] = useReducer((count: number) => count + 1, 0)
  useEffect(() => {
    if (applyKioskTexts(texts)) rerender()
  }, [texts])
  useEffect(() => () => void restoreKioskTexts(), [])

  const refetchSession = session.refetch

  const reset = useCallback(() => {
    dropDrafts()
    if (rules.tablet) setLocale(DEFAULT_LOCALE)
    setPhase({ name: 'start' })
    window.scrollTo({ top: 0 })
    // The next candidate starts here — ask whether a newer form went live meanwhile
    void refetchSession()
  }, [dropDrafts, rules.tablet, setLocale, refetchSession])

  // A newer blank went live while this page stayed open (the office tablet is
  // never reloaded by hand): reload on the start screen, where nobody is
  // mid-answer and a saved draft survives the reload (sessionStorage).
  const liveVersion = session.data?.formVersion
  useEffect(() => {
    if (phase.name === 'start' && liveVersion !== undefined && liveVersion !== KIOSK_FORM_VERSION) {
      reloadOnceFor(liveVersion)
    }
  }, [phase.name, liveVersion])

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
      <KioskListsContext.Provider value={lists}>
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
      </KioskListsContext.Provider>
    )
  }

  if (phase.name === 'dynamic') {
    return (
      <DynamicWizard
        session={session.data}
        definition={phase.definition}
        initialDraft={phase.draft}
        rules={rules}
        onDone={(result) => {
          dropDrafts()
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
      hasDraft={definition === null ? savedDraft !== null : savedDynamic !== null}
      noVacancies={session.data.vacancies.length === 0 && !session.data.generalPoolAvailable}
      presetVacancy={preset?.title ?? null}
      onStart={() => {
        dropDrafts()
        if (definition !== null) {
          setPhase({ name: 'dynamic', definition, draft: dynamicStart(definition, preset?.id ?? null) })
        } else {
          setPhase({ name: 'wizard', draft: { ...emptyDraft(), vacancy: preset?.id ?? '' } })
        }
      }}
      onResume={() => {
        // Re-read: the draft may have aged past its limit while the start screen was open.
        if (definition !== null) {
          const fresh = dynamicDraftStorage.load(definition.version, rules.draftMaxAgeMs)
          setDynamicSaved({ version: definition.version, draft: fresh })
          if (fresh !== null) setPhase({ name: 'dynamic', definition, draft: fresh })
          return
        }

        const fresh = draftStorage.load(rules.draftMaxAgeMs)
        setSavedDraft(fresh)
        if (fresh !== null) setPhase({ name: 'wizard', draft: fresh })
      }}
    />
  )
}

/**
 * Reloads the page once per server version. A browser that still gets the old
 * page after reloading (a stale cache) must not reload forever, so the version
 * is remembered for this tab first; without storage nothing is risked.
 */
function reloadOnceFor(version: string): void {
  const key = 'bonvi.kiosk.reloadedFor'

  try {
    if (window.sessionStorage.getItem(key) === version) return
    window.sessionStorage.setItem(key, version)
  } catch {
    return
  }

  window.location.reload()
}

/** A fresh constructor draft; `?vakansiya=` lands in the form's vacancy question. */
function dynamicStart(definition: KioskDefinition, vacancyId: string | null): DynamicDraft {
  const draft = emptyDynamicDraft()
  const vacancy = candidateFields(definition).find((field) => field.kind === 'vacancy')
  return vacancy === undefined || vacancyId === null ? draft : { ...draft, values: { [vacancy.id]: vacancyId } }
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
export function usePhoneViewport(): void {
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
