import { useQuery } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { CloudAlert, LoaderCircle, ShieldAlert, WifiOff } from 'lucide-react'
import { useEffect, useState } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { DEFAULT_LOCALE } from '@/shared/i18n/locales'
import { Button } from '@/shared/ui'
import { KIOSK_LANGUAGES, kioskFailure } from '../api/kiosk-api'
import { usePhoneViewport } from '../components/kiosk-page'
import type { KioskRules } from '../lib/kiosk-mode'
import { emptyDynamicDraft } from './dynamic-draft'
import { DynamicWizard } from './dynamic-wizard'
import { fetchPreviewSession } from './preview-api'

/** The preview walks like the candidate's own phone: no tablet timers, nothing kept. */
const PREVIEW_RULES: KioskRules = {
  tablet: false,
  draftMaxAgeMs: 0,
  startIdleMs: null,
  wizardIdleMs: null,
  doneReturnSeconds: null,
}

/** Codes that mean «this user may not look» rather than «try again». */
const FORBIDDEN = new Set(['auth.ruxsat_yoq', 'auth.kirilmagan'])

/**
 * `/anketa/korinish` — HRMentor's «Ko'rish» lands here (SPEC-anketa-konstruktori
 * §2.6): the constructor's DRAFT, live from HRMentor, in the candidate's own
 * frame. HR can fill it and download the sample PDF; nothing is stored or sent
 * to the candidate list. «Yangilash» fetches the draft again after an edit in
 * HRMentor — the answers typed so far stay; «Bekor qilish» starts over.
 */
export function KioskPreviewPage() {
  const { locale, setLocale } = useLocale()
  const [round, setRound] = useState(0)

  usePhoneViewport()

  // The form speaks the paper's three languages; HR's own language comes back on leaving.
  const [hrLocale] = useState(locale)
  useEffect(() => {
    if (!(KIOSK_LANGUAGES as readonly string[]).includes(hrLocale)) setLocale(DEFAULT_LOCALE)
    return () => setLocale(hrLocale)
  }, [hrLocale, setLocale])

  const session = useQuery({
    queryKey: ['kiosk-preview'],
    queryFn: ({ signal }) => fetchPreviewSession(signal),
    retry: false,
    staleTime: Infinity,
    gcTime: 0,
    refetchOnWindowFocus: false,
  })

  if (session.data === undefined) {
    return (
      <PreviewProblem
        error={session.isError ? session.error : null}
        busy={session.isFetching}
        onRetry={() => void session.refetch()}
      />
    )
  }

  const definition = session.data.definition
  if (definition === null) {
    // The server checks the draft before sending it; an unreadable one here is a version mismatch.
    return <PreviewProblem error={null} code="hrmentor.definition_invalid" busy={session.isFetching} onRetry={() => void session.refetch()} />
  }

  return (
    <DynamicWizard
      key={round}
      session={session.data}
      definition={definition}
      initialDraft={emptyDynamicDraft()}
      rules={PREVIEW_RULES}
      preview={{ onRefresh: () => void session.refetch(), refreshing: session.isFetching }}
      onDone={() => setRound((value) => value + 1)}
      onCancel={() => {
        setRound((value) => value + 1)
        window.scrollTo({ top: 0 })
      }}
    />
  )
}

/** Loading, no connection, no permission, or HRMentor / the draft could not be read. */
function PreviewProblem({
  error,
  code,
  busy,
  onRetry,
}: {
  error: Error | null
  /** A known failure without an HTTP error (the draft arrived but could not be read). */
  code?: string
  busy: boolean
  onRetry: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()

  if (error === null && code === undefined) {
    return (
      <div className="grid min-h-dvh place-items-center bg-[var(--bg)]">
        <LoaderCircle aria-label={t('common.loading')} className="size-10 animate-spin text-[var(--fg-tertiary)]" />
      </div>
    )
  }

  const offline = error !== null && kioskFailure(error).kind === 'offline'
  const failureCode = code ?? (error === null ? '' : problemCode(error))
  const forbidden = FORBIDDEN.has(failureCode)
  const Icon = offline ? WifiOff : forbidden ? ShieldAlert : CloudAlert

  return (
    <div className="grid min-h-dvh place-items-center bg-[var(--bg)] px-6 text-center">
      <div className="flex max-w-md flex-col items-center">
        <Icon className="size-12 text-[var(--fg-tertiary)]" strokeWidth={1.6} />
        <p className="mt-5 text-title3 font-semibold">{t('kiosk.preview.failedTitle')}</p>
        <p className="mt-2 text-body text-[var(--fg-secondary)]">
          {offline ? t('kiosk.offline.text') : errorMessage(failureCode, t('kiosk.preview.failedText'))}
        </p>
        {!forbidden && (
          <Button type="button" variant="primary" size="lg" pill disabled={busy} onClick={onRetry} className="mt-6 h-14 min-w-48">
            {busy && <LoaderCircle className="animate-spin" />}
            {t('kiosk.offline.retry')}
          </Button>
        )}
        <Button type="button" variant="ghost" size="lg" pill onClick={() => void navigate({ to: '/' })} className="mt-2 h-12 min-w-48">
          {t('kiosk.preview.back')}
        </Button>
      </div>
    </div>
  )
}
