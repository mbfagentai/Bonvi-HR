import { ArrowLeft, ArrowRight, LoaderCircle, Send, X } from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import type { LocaleCode } from '@/shared/i18n/locales'
import { cn } from '@/shared/lib/cn'
import { Alert, Button, Dialog, SegmentedControl } from '@/shared/ui'
import {
  BOT_CHECK_FAILED,
  KIOSK_LANGUAGES,
  kioskFailure,
  submitQuestionnaire,
  type KioskLanguage,
  type KioskSession,
  type KioskSubmitResult,
} from '../api/kiosk-api'
import {
  draftStorage,
  screenErrors,
  screenOfPath,
  toAnswers,
  validateAll,
  type FieldErrors,
  type KioskDraft,
  type ValidationContext,
} from '../lib/kiosk-draft'
import type { KioskRules } from '../lib/kiosk-mode'
import { aboutQuestionsOf, activeScreens, type WizardScreen } from '../lib/kiosk-options'
import { offeredComputerKeys } from '../lib/kiosk-texts'
import { scrollToFirstError } from '../lib/scroll-to-error'
import { useIdle } from '../lib/use-idle'
import { BotCheck, type BotCheckState } from './bot-check'
import { IdleDialog } from './idle-dialog'
import { AboutScreen } from './screens/screen-about'
import {
  BackgroundScreen,
  ConditionsScreen,
  HealthScreen,
  ReferralScreen,
  SalaryScreen,
  TermsScreen,
} from './screens/screen-conditions'
import { ConsentScreen, SignatureScreen } from './screens/screen-consent'
import { ComputerScreen, EducationScreen, LanguagesScreen, SchoolScreen } from './screens/screen-education'
import { FamilyScreen, ReferencesScreen, WorkScreen } from './screens/screen-history'
import {
  AddressScreen,
  ContactsScreen,
  LicenseScreen,
  MaritalScreen,
  PersonalScreen,
  VacancyScreen,
} from './screens/screen-personal'
import type { ScreenProps } from './screens/screen-props'

/** `botCheck` — Turnstile did not pass (here or on the server); shown next to «Yuborish». */
type SubmitFailure = { kind: 'offline' } | { kind: 'rejected'; message: string } | { kind: 'botCheck' }

const LANGUAGE_SHORT: Readonly<Record<KioskLanguage, string>> = {
  'uz-Latn': 'O‘z',
  'uz-Cyrl': 'Ўз',
  ru: 'Ру',
}

/**
 * The form: short one-topic screens (`WIZARD_SCREENS`) with the section name,
 * «4 / 25» and a progress bar on top and «Orqaga / Keyingi» pinned at the
 * bottom. A screen that does not apply to this candidate is left out of the
 * walk and of the count (`activeScreens`: no referees after «Avval biror
 * joyda ishlaganmisiz? — Yo'q»). «Keyingi» validates the screen; after the first failed attempt the
 * screen re-validates as the candidate types, so a fixed field loses its red
 * message at once. Every change is mirrored to session storage (photo and
 * signature stay in memory).
 *
 * With the bot check on (`session.turnstileSiteKey`) the last screen carries
 * the Turnstile widget; «Yuborish» pressed before it has a token waits
 * («Tekshirilmoqda…») and sends as soon as the token arrives.
 */
export function KioskWizard({
  session,
  initialDraft,
  rules,
  onDone,
  onCancel,
}: {
  session: KioskSession
  initialDraft: KioskDraft
  rules: KioskRules
  onDone: (result: KioskSubmitResult) => void
  onCancel: () => void
}) {
  const { t, locale, setLocale } = useLocale()
  const errorMessage = useErrorMessage()

  const [draft, setDraft] = useState(initialDraft)
  const [index, setIndex] = useState(0)
  const [photo, setPhoto] = useState<Blob | null>(null)
  const [signature, setSignature] = useState<Blob | null>(null)
  const [attempted, setAttempted] = useState<ReadonlySet<WizardScreen>>(new Set())
  const [submitting, setSubmitting] = useState(false)
  const [failure, setFailure] = useState<SubmitFailure | null>(null)
  const [confirmCancel, setConfirmCancel] = useState(false)
  const [idle, setIdle] = useState(false)
  /** Honeypot (SPEC §2) — only a bot types here. */
  const [website, setWebsite] = useState('')

  const siteKey = session.turnstileSiteKey
  const [botCheck, setBotCheck] = useState<BotCheckState>({ status: 'checking', interactive: false })
  const [botCheckReset, setBotCheckReset] = useState(0)
  /** «Yuborish» was pressed before the bot check had a token — send as soon as it has one. */
  const [awaitingCheck, setAwaitingCheck] = useState(false)
  const botCheckRef = useRef<HTMLDivElement>(null)

  // `index` points into this candidate's screens. The list changes only on the work screen, which
  // comes before the one it removes, so the current position stays put when it does.
  const screens = useMemo(() => activeScreens(draft.hasWorkExperience), [draft.hasWorkExperience])
  const screen: WizardScreen = screens[index] ?? 'vacancy'
  const total = screens.length
  const last = index === total - 1
  const language: KioskLanguage = (KIOSK_LANGUAGES as readonly string[]).includes(locale)
    ? (locale as KioskLanguage)
    : 'uz-Latn'

  const today = session.today !== '' ? session.today : new Date().toISOString().slice(0, 10)

  const context: ValidationContext = useMemo(
    () => ({
      today,
      vacancyIds: new Set(session.vacancies.map((vacancy) => vacancy.id)),
      generalPoolAvailable: session.generalPoolAvailable,
      hasPhoto: photo !== null,
      hasSignature: signature !== null,
      referralChannels: new Map(session.referralChannels.map((channel) => [channel.id, channel.asksDetail])),
      computerKeys: offeredComputerKeys(session.texts?.lists ?? {}),
    }),
    [today, session, photo, signature],
  )

  const allErrors = useMemo(() => validateAll(draft, context), [draft, context])

  // Errors are visible only after «Keyingi» was pressed on this screen once.
  const errors: FieldErrors = useMemo(
    () => (attempted.has(screen) ? screenErrors(screen, allErrors) : {}),
    [attempted, screen, allErrors],
  )

  useEffect(() => draftStorage.save(draft), [draft])

  const patch = useCallback((changes: Partial<KioskDraft>) => setDraft((current) => ({ ...current, ...changes })), [])

  // Tablet mode only: a candidate who walked away must not leave their answers to the next one.
  useIdle(rules.wizardIdleMs ?? 0, () => setIdle(true), rules.wizardIdleMs !== null && !idle && !submitting)

  const markAttempted = (owner: WizardScreen) => setAttempted((current) => new Set(current).add(owner))

  const goTo = (next: number) => {
    setIndex(next)
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
    const found = screenErrors(screen, validateAll(draft, context))
    if (Object.keys(found).length > 0) {
      markAttempted(screen)
      scrollToFirstError(found)
      return
    }

    if (last) void submit()
    else goTo(index + 1)
  }

  const submit = async (freshToken?: string) => {
    // Every screen once more — a draft restored after a reload may have skipped one.
    const everything = validateAll(draft, context)
    for (const [position, owner] of screens.entries()) {
      const found = screenErrors(owner, everything)
      if (Object.keys(found).length > 0) {
        markAttempted(owner)
        goTo(position)
        scrollToFirstError(found)
        return
      }
    }

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
      const result = await submitQuestionnaire({
        answers: toAnswers(draft, context),
        clientSubmissionId: draft.clientSubmissionId,
        language,
        photo,
        signature,
        website,
        turnstileToken,
        // The wording the candidate saw — the PDF keeps it (SPEC-blankalar-hrmentor §5.3)
        textsRevision: session.texts?.revision,
      })
      onDone(result)
    } catch (error) {
      // A token survives one server check only, whatever its outcome — the next try needs a new one.
      if (siteKey !== null) restartBotCheck()

      const failed = kioskFailure(error)

      if (failed.kind === 'rejected' && failed.code === BOT_CHECK_FAILED) {
        // Shown by the button; the draft, photo and signature all stay.
        setFailure({ kind: 'botCheck' })
      } else if (failed.kind === 'rejected') {
        // The server names the field: open its screen so the candidate sees where to fix.
        const owner = failed.detail === null ? null : screenOfPath(failed.detail)
        const position = owner === null ? -1 : screens.indexOf(owner)
        if (position >= 0 && owner !== screen) setIndex(position)

        setFailure({ kind: 'rejected', message: errorMessage(failed.code, t('kiosk.submit.rejected')) })
        window.scrollTo({ top: 0, behavior: 'smooth' })
      } else {
        setFailure(failed)
        window.scrollTo({ top: 0, behavior: 'smooth' })
      }
    } finally {
      setSubmitting(false)
    }
  }

  const screenProps: ScreenProps = { draft, patch, errors, session, today, context, tablet: rules.tablet }

  return (
    <div className="flex min-h-dvh flex-col bg-[var(--bg)]">
      <header className="blur-bar sticky top-0 z-20 border-b border-[var(--border)]">
        <div className="mx-auto max-w-2xl px-5 pt-[max(0.75rem,env(safe-area-inset-top))] pb-3">
          <div className="flex h-8 items-center gap-3">
            <p className="flex-1 text-footnote font-medium text-[var(--fg-secondary)] tabular">
              {t('kiosk.wizard.progress', { n: index + 1, total })}
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

          <h1 className="mt-1.5 text-title2 leading-tight font-semibold tracking-tight">
            {t(`kiosk.screen.${screen}`)}
          </h1>
        </div>

        <progress
          value={index + 1}
          max={total}
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
        {failure !== null && failure.kind !== 'botCheck' && (
          <Alert tone="danger" className="text-subhead">
            {failure.kind === 'offline' ? t('kiosk.submit.offline') : failure.message}
          </Alert>
        )}

        <div key={screen} className="space-y-6">
          <ScreenBody
            screen={screen}
            props={screenProps}
            photo={photo}
            onPhoto={setPhoto}
            hasSignature={signature !== null}
            onSignature={setSignature}
          />
        </div>

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

        {Object.keys(errors).length > 0 && (
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
            disabled={submitting || awaitingCheck}
            onClick={next}
            className="h-[52px] flex-[2] text-callout sm:ml-auto sm:min-w-44 sm:flex-none"
          >
            {submitting ? (
              <>
                <LoaderCircle className="animate-spin" />
                {t('kiosk.wizard.sending')}
              </>
            ) : awaitingCheck ? (
              <>
                <LoaderCircle className="animate-spin" />
                {t('kiosk.wizard.checking')}
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

function ScreenBody({
  screen,
  props,
  photo,
  onPhoto,
  hasSignature,
  onSignature,
}: {
  screen: WizardScreen
  props: ScreenProps
  photo: Blob | null
  onPhoto: (photo: Blob | null) => void
  hasSignature: boolean
  onSignature: (signature: Blob | null) => void
}): ReactNode {
  switch (screen) {
    case 'vacancy':
      return <VacancyScreen {...props} photo={photo} onPhoto={onPhoto} />
    case 'personal':
      return <PersonalScreen {...props} />
    case 'address':
      return <AddressScreen {...props} />
    case 'contacts':
      return <ContactsScreen {...props} />
    case 'marital':
      return <MaritalScreen {...props} />
    case 'license':
      return <LicenseScreen {...props} />
    case 'education':
      return <EducationScreen {...props} />
    case 'school':
      return <SchoolScreen {...props} />
    case 'languages':
      return <LanguagesScreen {...props} />
    case 'computer':
      return <ComputerScreen {...props} />
    case 'work':
      return <WorkScreen {...props} />
    case 'references':
      return <ReferencesScreen {...props} />
    case 'family':
      return <FamilyScreen {...props} />
    case 'referral':
      return <ReferralScreen {...props} />
    case 'salary':
      return <SalaryScreen {...props} />
    case 'conditions':
      return <ConditionsScreen {...props} />
    case 'terms':
      return <TermsScreen {...props} />
    case 'health':
      return <HealthScreen {...props} />
    case 'background':
      return <BackgroundScreen {...props} />
    case 'about':
    case 'aboutSuccess':
    case 'aboutHard':
    case 'aboutTeam':
      return (
        <AboutScreen
          {...props}
          questions={aboutQuestionsOf(screen, props.draft.hasWorkExperience)}
          showHint={screen === 'about'}
        />
      )
    case 'consent':
      return <ConsentScreen {...props} />
    case 'signature':
      return <SignatureScreen {...props} hasSignature={hasSignature} onSignature={onSignature} />
  }
}
