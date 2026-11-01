import { ArrowLeft, ArrowRight, Eye, FileDown, LoaderCircle, RefreshCw, RotateCw, Send, X } from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import type { LocaleCode } from '@/shared/i18n/locales'
import { cn } from '@/shared/lib/cn'
import { saveBlobAs } from '@/shared/lib/download'
import { Alert, Button, Dialog, SegmentedControl, Select } from '@/shared/ui'
import {
  BOT_CHECK_FAILED,
  DEFINITION_VERSION_UNKNOWN,
  KIOSK_LANGUAGES,
  kioskFailure,
  submitDynamicQuestionnaire,
  type KioskLanguage,
  type KioskSession,
  type KioskSubmitResult,
} from '../api/kiosk-api'
import { BotCheck, type BotCheckState } from '../components/bot-check'
import { IdleDialog } from '../components/idle-dialog'
import { ScreenHint } from '../components/kiosk-fields'
import type { FieldErrors } from '../lib/kiosk-draft'
import type { KioskRules } from '../lib/kiosk-mode'
import { scrollToFirstError } from '../lib/scroll-to-error'
import { useIdle } from '../lib/use-idle'
import { BIND, candidateFields, textIn, type KioskDefinition } from './definition'
import {
  activeSteps,
  allSteps,
  dynamicDraftStorage,
  evaluate,
  fieldOfPath,
  stepErrors,
  textOf,
  type DynamicDraft,
  type EvalContext,
  type WizardStep,
} from './dynamic-draft'
import { FieldView, type ErrorOf, type FieldEnv } from './dynamic-fields'
import { fetchPreviewPdf } from './preview-api'

/** `version` — the server no longer has the version this page shows; only a reload helps. */
type SubmitFailure =
  | { kind: 'offline' }
  | { kind: 'rejected'; message: string }
  | { kind: 'botCheck' }
  | { kind: 'version' }

const LANGUAGE_SHORT: Readonly<Record<KioskLanguage, string>> = {
  'uz-Latn': 'O‘z',
  'uz-Cyrl': 'Ўз',
  ru: 'Ру',
}

/** HR's preview (`/anketa/korinish`): what differs from the candidate's walk. */
export interface PreviewControls {
  /** Fetch HRMentor's draft again (the answers typed so far stay). */
  onRefresh: () => void
  refreshing: boolean
}

const stepKey = (step: WizardStep) => `${step.section.id}/${step.screen.id}`

/**
 * The constructor's form (SPEC-anketa-konstruktori §7): one definition screen
 * per step, the section title and «4 / 16» on top, «Orqaga / Keyingi» pinned
 * at the bottom — the built-in `KioskWizard`'s frame, so a candidate cannot
 * tell the two apart. A screen whose fields are all hidden by their conditions
 * is left out of the walk and of the count.
 *
 * «Keyingi» checks the screen with the client mirror of the server's rules;
 * after the first failed attempt the screen re-checks as the candidate types.
 * The definition is pinned for the whole walk (`definition` never changes
 * under the candidate) and its version goes back with the answers, so the
 * server validates exactly the form that was shown.
 *
 * In the preview nothing is stored or sent: no draft in storage, no bot
 * check, photo and signature are optional, and the last step offers the
 * sample PDF instead of «Yuborish».
 */
export function DynamicWizard({
  session,
  definition,
  initialDraft,
  rules,
  preview,
  onDone,
  onCancel,
}: {
  session: KioskSession
  definition: KioskDefinition
  initialDraft: DynamicDraft
  rules: KioskRules
  preview?: PreviewControls | undefined
  onDone: (result: KioskSubmitResult) => void
  onCancel: () => void
}) {
  const { t, locale, setLocale } = useLocale()
  const errorMessage = useErrorMessage()

  const [draft, setDraft] = useState(initialDraft)
  const [currentKey, setCurrentKey] = useState<string | null>(null)
  const [photo, setPhoto] = useState<Blob | null>(null)
  const [signature, setSignature] = useState<Blob | null>(null)
  const [attempted, setAttempted] = useState<ReadonlySet<string>>(new Set())
  const [submitting, setSubmitting] = useState(false)
  const [failure, setFailure] = useState<SubmitFailure | null>(null)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [idle, setIdle] = useState(false)
  /** Honeypot (SPEC-planshet-anketa §2) — only a bot types here. */
  const [website, setWebsite] = useState('')

  const siteKey = preview === undefined ? session.turnstileSiteKey : null
  const [botCheck, setBotCheck] = useState<BotCheckState>({ status: 'checking', interactive: false })
  const [botCheckReset, setBotCheckReset] = useState(0)
  /** «Yuborish» was pressed before the bot check had a token — send as soon as it has one. */
  const [awaitingCheck, setAwaitingCheck] = useState(false)
  const botCheckRef = useRef<HTMLDivElement>(null)

  const language: KioskLanguage = (KIOSK_LANGUAGES as readonly string[]).includes(locale)
    ? (locale as KioskLanguage)
    : 'uz-Latn'
  const today = session.today !== '' ? session.today : new Date().toISOString().slice(0, 10)

  const context: EvalContext = useMemo(
    () => ({
      today,
      vacancyIds: new Set(session.vacancies.map((vacancy) => vacancy.id)),
      generalPoolAvailable: session.generalPoolAvailable,
      referralChannels: new Map(session.referralChannels.map((channel) => [channel.id, channel.asksDetail])),
      hasPhoto: photo !== null,
      hasSignature: signature !== null,
      requireFiles: preview === undefined,
    }),
    [today, session, photo, signature, preview],
  )

  const evaluation = useMemo(() => evaluate(definition, draft, context), [definition, draft, context])

  // Conditions only look back (the server's order), so a choice never hides a
  // screen before the current one: the position of the current step is stable.
  const steps = useMemo(() => allSteps(definition), [definition])
  const active = useMemo(() => activeSteps(steps, evaluation.visible), [steps, evaluation.visible])
  const found = currentKey === null ? -1 : active.findIndex((step) => stepKey(step) === currentKey)
  const index = Math.max(0, found)
  const step = active[index]
  const key = step === undefined ? '' : stepKey(step)
  const total = active.length
  const last = index === total - 1

  const shown: FieldErrors = useMemo(
    () => (step !== undefined && attempted.has(key) ? stepErrors(step, evaluation.errors) : {}),
    [attempted, key, step, evaluation.errors],
  )

  const errorOf: ErrorOf = useCallback(
    (path: string) => {
      const error = shown[path]
      return error === undefined ? undefined : t(`kiosk.error.${error.code}`, error.values)
    },
    [shown, t],
  )

  // The preview stores nothing (SPEC §2.6).
  useEffect(() => {
    if (preview === undefined) dynamicDraftStorage.save(definition.version, draft)
  }, [draft, definition.version, preview])

  // Tablet mode only: a candidate who walked away must not leave their answers to the next one.
  useIdle(rules.wizardIdleMs ?? 0, () => setIdle(true), rules.wizardIdleMs !== null && !idle && !submitting)

  const setValue = useCallback(
    (id: string, value: unknown) => setDraft((current) => ({ ...current, values: { ...current.values, [id]: value } })),
    [],
  )
  const setSame = useCallback(
    (id: string, same: boolean) => setDraft((current) => ({ ...current, same: { ...current.same, [id]: same } })),
    [],
  )

  const fullName = useMemo(() => {
    const bound = candidateFields(definition)
    const part = (bind: string) => {
      const field = bound.find((item) => item.bind === bind)
      return field === undefined ? '' : textOf(draft.values[field.id]).trim()
    }
    return [part(BIND.lastName), part(BIND.firstName), part(BIND.middleName)].filter(Boolean).join(' ')
  }, [definition, draft.values])

  const markAttempted = (owner: string) => setAttempted((current) => new Set(current).add(owner))

  const goTo = (position: number) => {
    const target = active[position]
    if (target === undefined) return
    setCurrentKey(stepKey(target))
    setFailure(null)
    setAwaitingCheck(false)
    window.scrollTo({ top: 0 })
  }

  /** Drops the current token (spent or stale) and runs the check again. */
  const restartBotCheck = () => {
    setBotCheck({ status: 'checking', interactive: false })
    setBotCheckReset((value) => value + 1)
  }

  const onBotCheck = (state: BotCheckState) => {
    setBotCheck(state)
    if (!awaitingCheck) return

    if (state.status === 'passed') {
      void submit(state.token)
    } else if (state.status === 'failed') {
      setAwaitingCheck(false)
      setFailure({ kind: 'botCheck' })
    }
  }

  // Waiting for a checkbox the candidate may not see: bring it into view.
  const needsTap = awaitingCheck && botCheck.status === 'checking' && botCheck.interactive
  useEffect(() => {
    if (needsTap) botCheckRef.current?.scrollIntoView({ block: 'center', behavior: 'smooth' })
  }, [needsTap])

  const next = () => {
    if (step === undefined) return

    const errors = stepErrors(step, evaluation.errors)
    if (Object.keys(errors).length > 0) {
      markAttempted(key)
      scrollToFirstError(errors)
      return
    }

    if (last) void submit()
    else goTo(index + 1)
  }

  /** Every step once more — a draft restored after a reload may have skipped one. */
  const firstInvalidStep = (): boolean => {
    for (const [position, owner] of active.entries()) {
      const errors = stepErrors(owner, evaluation.errors)
      if (Object.keys(errors).length > 0) {
        markAttempted(stepKey(owner))
        goTo(position)
        scrollToFirstError(errors)
        return true
      }
    }
    return false
  }

  /** The server named a field: open its step so the candidate sees where to fix. */
  const showServerFailure = (code: string, detail: string | null, fallback: string) => {
    const owner = detail === null ? -1 : active.findIndex((item) => item.screen.fields.some((field) => field.id === fieldOfPath(detail)))
    if (owner >= 0) {
      const target = active[owner]
      if (target !== undefined) {
        markAttempted(stepKey(target))
        setCurrentKey(stepKey(target))
      }
    }
    setFailure({ kind: 'rejected', message: errorMessage(code, fallback) })
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const submit = async (freshToken?: string) => {
    if (firstInvalidStep()) return
    if (preview !== undefined) return void samplePdf()
    if (photo === null || signature === null) return

    let turnstileToken: string | undefined
    if (siteKey !== null) {
      turnstileToken = freshToken ?? (botCheck.status === 'passed' ? botCheck.token : undefined)
      if (turnstileToken === undefined) {
        setFailure(null)
        if (botCheck.status === 'failed') restartBotCheck()
        setAwaitingCheck(true)
        return
      }
    }

    setAwaitingCheck(false)
    setSubmitting(true)
    setFailure(null)

    try {
      const result = await submitDynamicQuestionnaire({
        answers: evaluation.answers,
        definitionVersion: definition.version,
        clientSubmissionId: draft.clientSubmissionId,
        language,
        photo,
        signature,
        website,
        turnstileToken,
      })
      onDone(result)
    } catch (error) {
      // A token survives one server check only, whatever its outcome — the next try needs a new one.
      if (siteKey !== null) restartBotCheck()

      const failed = kioskFailure(error)

      if (failed.kind === 'rejected' && failed.code === BOT_CHECK_FAILED) {
        setFailure({ kind: 'botCheck' })
      } else if (failed.kind === 'rejected' && failed.code === DEFINITION_VERSION_UNKNOWN) {
        // HR published a new version and the old one is gone: this form can no longer be sent.
        setFailure({ kind: 'version' })
        window.scrollTo({ top: 0, behavior: 'smooth' })
      } else if (failed.kind === 'rejected') {
        showServerFailure(failed.code, failed.detail, t('kiosk.submit.rejected'))
      } else {
        setFailure(failed)
        window.scrollTo({ top: 0, behavior: 'smooth' })
      }
    } finally {
      setSubmitting(false)
    }
  }

  /** Preview only: the sample PDF drawn by the server from these answers. */
  const samplePdf = async () => {
    setSubmitting(true)
    setFailure(null)

    try {
      const { blob, fileName } = await fetchPreviewPdf({ answers: evaluation.answers, language, photo, signature })
      saveBlobAs(blob, fileName)
    } catch (error) {
      const failed = kioskFailure(error)
      if (failed.kind === 'rejected') showServerFailure(failed.code, failed.detail, t('kiosk.preview.pdfFailed'))
      else {
        setFailure(failed)
        window.scrollTo({ top: 0, behavior: 'smooth' })
      }
    } finally {
      setSubmitting(false)
    }
  }

  const env: FieldEnv = {
    definition,
    draft,
    evalContext: context,
    session,
    language,
    today,
    tablet: rules.tablet,
    screenTitle: step === undefined ? '' : textIn(step.screen.title ?? step.section.title, language),
    fullName,
    photo,
    onPhoto: setPhoto,
    hasSignature: signature !== null,
    onSignature: setSignature,
    shown,
  }

  const sectionTitle = step === undefined ? '' : textIn(step.section.title, language)
  const screenHint = step?.screen.hint ?? null

  return (
    <div className="flex min-h-dvh flex-col bg-[var(--bg)]">
      <header className="blur-bar sticky top-0 z-20 border-b border-[var(--border)]">
        {preview !== undefined && (
          <PreviewRibbon
            preview={preview}
            steps={active.map((item) => ({ value: stepKey(item), label: textIn(item.screen.title ?? item.section.title, language) }))}
            current={key}
            onJump={(target) => goTo(active.findIndex((item) => stepKey(item) === target))}
          />
        )}

        <div className="mx-auto max-w-2xl px-5 pt-[max(0.75rem,env(safe-area-inset-top))] pb-3">
          <div className="flex h-8 items-center gap-3">
            <p className="min-w-0 flex-1 truncate text-footnote font-medium text-[var(--fg-secondary)]">
              <span className="tabular">{t('kiosk.wizard.progress', { n: index + 1, total })}</span>
              {sectionTitle !== '' && <span>{` · ${sectionTitle}`}</span>}
            </p>

            <SegmentedControl
              size="sm"
              aria-label={t('common.language')}
              value={language}
              onChange={(code) => setLocale(code as LocaleCode)}
              options={KIOSK_LANGUAGES.map((code) => ({ value: code, label: LANGUAGE_SHORT[code] }))}
              className="w-[132px]"
            />

            <button
              type="button"
              onClick={() => setConfirmCancel(true)}
              aria-label={t('kiosk.wizard.cancel')}
              title={t('kiosk.wizard.cancel')}
              className={cn(
                'grid size-8 shrink-0 place-items-center rounded-full',
                'bg-[var(--bg-sunken)] text-[var(--fg-secondary)]',
                'transition-colors hover:bg-[var(--bg-hover)] hover:text-[var(--fg)]',
              )}
            >
              <X className="size-[18px]" strokeWidth={2.5} />
            </button>
          </div>

          <h1 className="mt-1.5 text-title2 leading-tight font-semibold tracking-tight">{env.screenTitle}</h1>
        </div>

        <progress
          value={index + 1}
          max={Math.max(total, 1)}
          aria-label={t('kiosk.wizard.progress', { n: index + 1, total })}
          className={cn(
            'block h-1 w-full appearance-none border-0 bg-[var(--bg-sunken)]',
            '[&::-webkit-progress-bar]:bg-[var(--bg-sunken)]',
            '[&::-webkit-progress-value]:bg-[var(--accent)] [&::-webkit-progress-value]:transition-[width] [&::-webkit-progress-value]:duration-300',
            '[&::-moz-progress-bar]:bg-[var(--accent)]',
          )}
        />
      </header>

      <main className="mx-auto w-full max-w-2xl flex-1 space-y-6 px-5 pt-6 pb-10">
        {failure?.kind === 'version' && (
          <Alert tone="danger" className="text-subhead">
            <p>{errorMessage(DEFINITION_VERSION_UNKNOWN)}</p>
            <Button
              type="button"
              variant="secondary"
              size="lg"
              pill
              onClick={() => window.location.reload()}
              className="mt-3 h-11"
            >
              <RotateCw />
              {t('kiosk.dyn.reload')}
            </Button>
          </Alert>
        )}

        {(failure?.kind === 'offline' || failure?.kind === 'rejected') && (
          <Alert tone="danger" className="text-subhead">
            {failure.kind === 'offline' ? t('kiosk.submit.offline') : failure.message}
          </Alert>
        )}

        {step !== undefined && (
          <div key={key} className="space-y-6">
            {screenHint !== null && <ScreenHint>{textIn(screenHint, language)}</ScreenHint>}
            {step.screen.fields
              .filter((field) => evaluation.visible.has(field.id) && field.kind !== 'checklist')
              .map((field) => (
                <FieldView key={field.id} field={field} env={env} error={errorOf} setValue={setValue} setSame={setSame} />
              ))}
          </div>
        )}

        {last && siteKey !== null && (
          // Keyed by the submission: every new candidate gets a new widget.
          <div ref={botCheckRef}>
            <BotCheck
              key={draft.clientSubmissionId}
              siteKey={siteKey}
              language={language}
              resetSignal={botCheckReset}
              onChange={onBotCheck}
            />
          </div>
        )}

        {Object.keys(shown).length > 0 && (
          <Alert tone="warning" className="text-subhead">
            {t('kiosk.wizard.fixErrors')}
          </Alert>
        )}

        {/* Honeypot: off-screen and out of the tab order; people never reach it. */}
        <div aria-hidden className="pointer-events-none absolute -left-[10000px] size-px overflow-hidden opacity-0">
          <label>
            Website
            <input
              type="text"
              name="website"
              tabIndex={-1}
              autoComplete="off"
              value={website}
              onChange={(event) => setWebsite(event.target.value)}
            />
          </label>
        </div>
      </main>

      <footer className="blur-bar sticky bottom-0 z-20 border-t border-[var(--border)]">
        {failure?.kind === 'botCheck' && (
          <div className="mx-auto max-w-2xl px-5 pt-3">
            <Alert tone="danger" className="text-subhead">
              {errorMessage(BOT_CHECK_FAILED, t('kiosk.submit.rejected'))}
            </Alert>
          </div>
        )}

        <div className="mx-auto flex max-w-2xl items-center gap-3 px-5 pt-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]">
          {index > 0 && (
            <Button
              type="button"
              variant="secondary"
              size="lg"
              pill
              disabled={submitting}
              onClick={() => goTo(index - 1)}
              className="h-[52px] flex-1 text-callout sm:min-w-36 sm:flex-none"
            >
              <ArrowLeft />
              {t('kiosk.wizard.back')}
            </Button>
          )}

          <Button
            type="button"
            variant="primary"
            size="lg"
            pill
            disabled={submitting || awaitingCheck || failure?.kind === 'version'}
            onClick={next}
            className="h-[52px] flex-[2] text-callout sm:ml-auto sm:min-w-44 sm:flex-none"
          >
            {submitting ? (
              <>
                <LoaderCircle className="animate-spin" />
                {preview !== undefined ? t('kiosk.preview.making') : t('kiosk.wizard.sending')}
              </>
            ) : awaitingCheck ? (
              <>
                <LoaderCircle className="animate-spin" />
                {t('kiosk.wizard.checking')}
              </>
            ) : last && preview !== undefined ? (
              <>
                <FileDown />
                {t('kiosk.preview.pdf')}
              </>
            ) : last ? (
              <>
                <Send />
                {t('kiosk.wizard.submit')}
              </>
            ) : (
              <>
                {t('kiosk.wizard.next')}
                <ArrowRight />
              </>
            )}
          </Button>
        </div>
      </footer>

      <Dialog
        open={confirmCancel}
        onClose={() => setConfirmCancel(false)}
        size="sm"
        title={t('kiosk.wizard.cancelTitle')}
        footer={
          <>
            <Button type="button" variant="secondary" size="lg" onClick={() => setConfirmCancel(false)} className="flex-1 sm:flex-none">
              {t('kiosk.wizard.cancelNo')}
            </Button>
            <Button type="button" variant="danger" size="lg" onClick={onCancel} className="flex-1 sm:flex-none">
              {t('kiosk.wizard.cancelYes')}
            </Button>
          </>
        }
      >
        <p className="text-body text-[var(--fg-secondary)]">{t('kiosk.wizard.cancelText')}</p>
      </Dialog>

      {rules.wizardIdleMs !== null && <IdleDialog open={idle} onContinue={() => setIdle(false)} onTimeout={onCancel} />}
    </div>
  )
}

/**
 * The preview's strip above the frame: it is not the live form, the draft can
 * be fetched again after an edit in HRMentor, and any screen can be opened
 * without filling the ones before it (HR checks the look, not the answers).
 */
function PreviewRibbon({
  preview,
  steps,
  current,
  onJump,
}: {
  preview: PreviewControls
  steps: { value: string; label: string }[]
  current: string
  onJump: (key: string) => void
}) {
  const { t } = useLocale()

  return (
    <div className="border-b border-ios-orange/30 bg-ios-orange/10">
      <div className="mx-auto flex max-w-2xl flex-wrap items-center gap-x-3 gap-y-2 px-5 py-2">
        <p className="flex min-w-0 flex-1 items-center gap-2 text-footnote font-semibold text-ios-orange">
          <Eye className="size-4 shrink-0" />
          <span className="truncate">{t('kiosk.preview.ribbon')}</span>
        </p>

        <Select
          aria-label={t('kiosk.preview.jump')}
          size="sm"
          options={steps.map((item, at) => ({ value: item.value, label: `${at + 1}. ${item.label}` }))}
          value={current}
          onChange={(event) => onJump(event.target.value)}
          className="w-44 sm:w-56"
        />

        <Button
          type="button"
          variant="secondary"
          size="sm"
          pill
          disabled={preview.refreshing}
          onClick={preview.onRefresh}
          title={t('kiosk.preview.refreshHint')}
        >
          <RefreshCw className={cn(preview.refreshing && 'animate-spin')} />
          {t('kiosk.preview.refresh')}
        </Button>
      </div>
    </div>
  )
}
