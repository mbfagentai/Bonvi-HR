import { useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { CircleCheck, ExternalLink, LoaderCircle, Play, Undo2 } from 'lucide-react'
import { useCallback, useRef, useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import {
  useBlankSubmission,
  useBlankTemplateVersion,
  useCreateBlank,
} from '@/features/blanks/api/blanks-queries'
import { useBlankAttachmentViewer } from '@/features/blanks/components/blank-attachment-viewer'
import { BlankForm } from '@/features/blanks/components/renderer/blank-form'
import { blankErrorCode } from '@/features/blanks/lib/blank-error'
import { searchBlankDocRef, searchBlankRef } from '@/features/blanks/lib/blank-lookups'
import { BLANKS_FILL, BLANKS_VIEW } from '@/features/blanks/lib/blank-permissions'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { todayIso } from '@/shared/lib/today'
import { Alert, Badge, Button, buttonVariants, Card, Dialog, Skeleton, TextArea } from '@/shared/ui'

import type { ProcessCaseDetail, ProcessCaseStep, ProcessStepInputs } from '../api/processes-api'
import {
  processKeys,
  useCompleteProcessStep,
  useSkipProcessStep,
  useUndoProcessStep,
} from '../api/processes-queries'
import { PROCESSES_MANAGE, PROCESSES_WORK } from '../lib/permissions'
import { describeDue, longDueDate } from '../lib/process-due'
import { describeRecordedInputs } from '../lib/process-manual-form'
import {
  blankCreateBody,
  isStepFinished,
  pendingPrereqTitles,
  PROCESS_WRITE_API_READY,
  stepEntry,
  type ProcessStepEntry,
} from '../lib/process-step'
import {
  ProcessBlankChip,
  ProcessHolderLabel,
  ProcessStateBadge,
  ProcessWarningChips,
  useProcessRoleLabel,
} from './process-chips'
import { ProcessManualForm } from './process-manual-form'

const NO_SECTIONS: readonly string[] = []

/** The panel's chips read a step larger than the same chips in a list. */
const PANEL_CHIP_CLASS = 'text-footnote'

/** Blue bold numbers on the left, the way the prototype numbers its guide and its callouts. */
function NumberedLines({ lines, className }: { lines: readonly string[]; className?: string }) {
  const { num } = useLocale()

  return (
    <ol className={cn('space-y-2', className)}>
      {lines.map((line, index) => (
        <li key={index} className="flex gap-1.5">
          <span className="tabular shrink-0 font-semibold text-[var(--accent-text)]">
            {num(index + 1)}.
          </span>
          <span className="min-w-0 break-words">{line}</span>
        </li>
      ))}
    </ol>
  )
}

/** A native disclosure, with the prototype's small filled triangle instead of the browser's own. */
function Disclosure({
  summary,
  summaryClassName,
  className,
  onToggle,
  children,
}: {
  summary: ReactNode
  summaryClassName?: string
  className?: string
  onToggle?: (open: boolean) => void
  children: ReactNode
}) {
  return (
    <details
      className={cn('group', className)}
      onToggle={(event) => onToggle?.(event.currentTarget.open)}
    >
      <summary
        className={cn(
          'flex cursor-pointer list-none items-center gap-2 [&::-webkit-details-marker]:hidden',
          summaryClassName,
        )}
      >
        <Play
          aria-hidden
          className="size-2.5 shrink-0 fill-current transition-transform duration-[120ms] group-open:rotate-90"
        />
        <span className="min-w-0">{summary}</span>
      </summary>
      {children}
    </details>
  )
}

/** Red late, orange today, grey otherwise: «Muddat: 5-noyabr gacha». Nothing on a step without a due date. */
function DeadlineChip({ step }: { step: ProcessCaseStep }) {
  const { t, num, locale } = useLocale()
  const due = describeDue(step.dueDate, step.daysLate, todayIso())

  if (due.kind === 'none') return null
  if (due.kind === 'late') {
    return (
      <Badge tone="danger" className={PANEL_CHIP_CLASS}>
        {t('processes.due.late', { n: num(due.days) })}
      </Badge>
    )
  }

  return (
    <Badge
      tone={due.kind === 'today' ? 'warning' : 'neutral'}
      className={cn(PANEL_CHIP_CLASS, due.kind !== 'today' && 'text-[var(--fg)]')}
    >
      {t('processes.case.deadline', { date: longDueDate(step.dueDate ?? '', locale) })}
    </Badge>
  )
}

/** The blue-edged note that says whose step this is and what stands in its way. */
function StepCallout({ title, lines }: { title: string; lines: readonly string[] }) {
  return (
    <aside className="rounded-md border border-[var(--accent-soft-hover)] border-l-4 border-l-[var(--accent)] bg-[var(--accent-soft)] px-5 py-[18px]">
      <p className="text-callout font-semibold">{title}</p>
      {lines.length > 0 && <NumberedLines lines={lines} className="mt-2.5 text-callout leading-6" />}
    </aside>
  )
}

/** What the step's callout says, or `null` when the viewer simply holds an open step. */
function useStepCallout(
  detail: ProcessCaseDetail,
  step: ProcessCaseStep,
): { title: string; lines: string[] } | null {
  const { t } = useLocale()
  const roleLabel = useProcessRoleLabel()

  if (isStepFinished(step.state) || detail.status !== 'Active') return null

  const waiting = step.state === 'Locked' || step.state === 'Waiting'
  const prereqs = waiting ? pendingPrereqTitles(step, detail.steps) : []
  const prereqLines = prereqs.length > 0 ? [t('processes.case.prereqs', { titles: prereqs.join('; ') })] : []

  if (step.holder.role === null) {
    return {
      title: t('processes.case.systemTitle'),
      lines: [t('processes.case.systemText'), ...prereqLines],
    }
  }

  if (!step.viewerHolds) {
    const role = roleLabel(step.holder.role)
    return {
      title: t('processes.case.notHolder', { role }),
      lines: [t('processes.case.notHolderText', { role }), ...prereqLines],
    }
  }

  return prereqLines.length > 0
    ? { title: t('processes.case.waitingTitle'), lines: prereqLines }
    : null
}

/** A blank's answers, read-only. Mounted when the disclosure opens, so a closed one asks the server for nothing. */
function LinkedBlankPreview({ submissionId }: { submissionId: string }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const detail = useBlankSubmission(submissionId)
  const version = useBlankTemplateVersion(detail.data?.templateVersionId)
  const viewer = useBlankAttachmentViewer(submissionId, detail.data?.attachments ?? [])
  const ignoreChange = useCallback(() => undefined, [])

  const openLink = (
    <Link
      to="/blankalar/$submissionId"
      params={{ submissionId }}
      className={buttonVariants({ variant: 'secondary', size: 'sm' })}
    >
      {t('processes.case.open')}
    </Link>
  )

  if (detail.isError || version.isError) {
    return (
      <div className="flex flex-wrap items-center gap-3 text-subhead text-[var(--fg-secondary)]">
        <span>{errorMessage(problemCode(detail.error ?? version.error))}</span>
        {openLink}
      </div>
    )
  }

  const submission = detail.data
  const definition = version.data?.definition

  if (submission === undefined || definition === undefined) {
    return (
      <div className="space-y-2" aria-busy="true">
        <Skeleton className="h-4 w-2/3 rounded-md" />
        <Skeleton className="h-4 w-1/2 rounded-md" />
      </div>
    )
  }

  return (
    <div className="space-y-3">
      <BlankForm
        definition={definition}
        answers={submission.answers}
        onChange={ignoreChange}
        editableSections={NO_SECTIONS}
        readOnly
        computed={submission.computed}
        attachments={submission.attachments}
        searchRef={searchBlankRef}
        searchDocRef={searchBlankDocRef}
        openFile={viewer.open}
      />
      {openLink}
      {viewer.element}
    </div>
  )
}

/**
 * «… to'ldirilgan ma'lumot (o'qish uchun)»: what a manual step recorded, and the blank behind a blank step. A step with
 * neither has nothing to read, so the disclosure drops out. Only a step that links to a real blank talks about one.
 */
function StepRecordedData({ step }: { step: ProcessCaseStep }) {
  const { t, date } = useLocale()
  const { hasPermission } = useAuth()
  const [opened, setOpened] = useState(false)

  const recorded = describeRecordedInputs(step.manual?.inputs ?? [], step.payload)
  const submissionId = step.linkedBlankSubmissionId
  const blankAhead =
    submissionId === null && step.link.screen === 'blank' && !isStepFinished(step.state)

  if (recorded.length === 0 && submissionId === null && !blankAhead) return null

  return (
    <Disclosure
      className="rounded-md border border-[var(--border)]"
      summaryClassName="px-4 py-3.5 text-subhead font-semibold"
      summary={
        step.blankTitle !== null
          ? t('processes.case.readOnlyData', { title: step.blankTitle })
          : t('processes.case.readOnlyDataPlain')
      }
      onToggle={(open) => {
        if (open) setOpened(true)
      }}
    >
      <div className="space-y-3 border-t border-[var(--border)] px-4 py-3.5">
        {recorded.length > 0 && (
          <dl className="space-y-1.5 text-subhead">
            {recorded.map((row) => (
              <div key={row.id}>
                <dt className="text-footnote text-[var(--fg-secondary)]">{row.label}</dt>
                {row.kind === 'text' && <dd className="whitespace-pre-line">{row.text}</dd>}
                {row.kind === 'date' && <dd className="tabular">{date(row.date)}</dd>}
                {row.kind === 'checklist' && (
                  <dd>
                    <ul className="space-y-0.5">
                      {row.items.map((item) => (
                        <li key={item.value}>
                          {item.label}
                          {' — '}
                          <span className="text-[var(--fg-secondary)]">
                            {t(`processes.case.marks.${item.mark}`)}
                          </span>
                        </li>
                      ))}
                    </ul>
                  </dd>
                )}
              </div>
            ))}
          </dl>
        )}

        {blankAhead && recorded.length === 0 && (
          <p className="text-subhead text-[var(--fg-secondary)]">
            {t('processes.case.blankNotFilled')}
          </p>
        )}

        {submissionId !== null &&
          (hasPermission(BLANKS_VIEW) ? (
            opened && <LinkedBlankPreview submissionId={submissionId} />
          ) : (
            <p className="text-subhead text-[var(--fg-secondary)]">
              {t('processes.case.blankNoAccess')}
            </p>
          ))}
      </div>
    </Disclosure>
  )
}

/**
 * «IMZOLAR». The step's definition names only the kind of signature (D1, D2, D3), not who signed or when, so one row says
 * what is asked and of whom. The signers themselves live on the blank's own page.
 */
function StepSignatures({ step }: { step: ProcessCaseStep }) {
  const { t } = useLocale()
  const roleLabel = useProcessRoleLabel()

  if (step.signature === null) return null

  const signed = step.state === 'Done'
  const standing = signed ? 'done' : step.state === 'Skipped' ? 'skipped' : 'pending'

  return (
    <section className="space-y-2">
      <h3 className="text-footnote font-semibold tracking-wide text-[var(--fg-secondary)] uppercase">
        {t('processes.case.signaturesTitle')}
      </h3>
      <p className="flex items-start gap-2 rounded-md border border-[var(--border)] px-4 py-3 text-subhead">
        {signed ? (
          <CircleCheck aria-hidden className="mt-0.5 size-4 shrink-0 text-ios-green" />
        ) : (
          <span
            aria-hidden
            className="mt-[3px] size-3.5 shrink-0 rounded-full border-[1.5px] border-[var(--fg)]"
          />
        )}
        <span className="min-w-0">
          {step.blankTitle ?? step.title}
          {' — '}
          <span className="font-semibold">{roleLabel(step.holder.role)}</span>
          {' · '}
          {t(`processes.case.signature.${step.signature}`, { defaultValue: step.signature })}
          {' · '}
          {t(`processes.case.signatureState.${standing}`)}
        </span>
      </p>
    </section>
  )
}

/**
 * The centre card of a case (SPEC §11.5): whose step it is, why it stands, what was entered, and the buttons. It takes a
 * loaded case, so any screen holding `useProcessCase(caseId)` can draw it. The `key` starts a fresh form and fresh errors
 * for every step.
 */
export function ProcessStepPanel({
  detail,
  step,
}: {
  detail: ProcessCaseDetail
  step: ProcessCaseStep
}) {
  return <StepPanelBody key={`${detail.id}:${step.stepCode}`} detail={detail} step={step} />
}

function StepPanelBody({ detail, step }: { detail: ProcessCaseDetail; step: ProcessCaseStep }) {
  const { t, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()
  const callout = useStepCallout(detail, step)

  const finished = isStepFinished(step.state)
  // The stepper's dot already says «active» and «not yet»; only what needs reading gets a word here.
  const showState = step.state !== 'Active' && step.state !== 'Locked'

  return (
    <Card>
      <header className="border-b border-[var(--border)] px-5 pt-5 pb-5 sm:px-[22px]">
        <div className="flex flex-wrap items-center gap-1.5">
          {step.holder.role !== null && (
            <Badge
              tone="brand"
              className={cn(
                PANEL_CHIP_CLASS,
                'h-auto min-h-6 max-w-full border border-[var(--accent-soft-hover)] py-0.5 whitespace-normal',
              )}
            >
              <ProcessHolderLabel holder={step.holder} />
            </Badge>
          )}
          <ProcessBlankChip
            code={step.blankCode}
            title={step.blankTitle}
            className={PANEL_CHIP_CLASS}
          />
          {!finished && <DeadlineChip step={step} />}
          {showState && <ProcessStateBadge state={step.state} />}
          {step.optional && <Badge tone="neutral">{t('processes.case.optional')}</Badge>}
          <ProcessWarningChips codes={step.warnings} />
        </div>

        <h2 className="mt-3 text-title2 leading-tight font-semibold break-words">{step.title}</h2>

        {step.doneAt !== null && (
          <p className="tabular mt-2 text-footnote text-[var(--fg-secondary)]">
            {t(`processes.state.${step.state}`)}: {dateTime(step.doneAt)}
            {step.doneByName !== null && ` · ${step.doneByName}`}
          </p>
        )}
        {step.resolution !== null && step.resolution !== 'Manual' && (
          <p className="mt-1 text-footnote text-[var(--fg-secondary)]">
            {t(`processes.case.resolution.${step.resolution}`)}
          </p>
        )}
      </header>

      <div className="space-y-5 px-5 py-5 sm:px-[22px]">
        {step.state === 'Blocked' && (
          <Alert tone="danger" title={errorMessage(step.gateErrorCode, step.gateCode ?? undefined)}>
            {step.resolver !== null && (
              <span>
                {t('processes.case.resolver')}: <ProcessHolderLabel holder={step.resolver} />
              </span>
            )}
          </Alert>
        )}

        {callout !== null && <StepCallout title={callout.title} lines={callout.lines} />}

        <StepRecordedData step={step} />
        <StepSignatures step={step} />

        <StepActions
          detail={detail}
          step={step}
          canWork={hasPermission(PROCESSES_WORK)}
          canManage={hasPermission(PROCESSES_MANAGE)}
          canFillBlanks={hasPermission(BLANKS_FILL)}
        />
      </div>
    </Card>
  )
}

/** The right-hand card: «Nima qilaman?» and where the instruction comes from (SPEC §11.5). */
export function ProcessStepGuide({ step }: { step: ProcessCaseStep }) {
  const { t } = useLocale()

  return (
    <Card key={step.stepCode} className="p-5">
      <h2 className="text-body font-semibold">{t('processes.case.guide')}</h2>

      {step.guide.length > 0 ? (
        <NumberedLines lines={step.guide} className="mt-3 text-callout leading-6" />
      ) : (
        <p className="mt-3 text-subhead text-[var(--fg-secondary)]">
          {t('processes.case.guideEmpty')}
        </p>
      )}

      {step.sourceRef !== null && (
        <Disclosure
          className="mt-4"
          summaryClassName="text-footnote text-[var(--fg-secondary)]"
          summary={t('processes.case.source')}
        >
          <p className="mt-2 text-footnote break-words text-[var(--fg-secondary)]">
            {step.sourceRef}
          </p>
        </Disclosure>
      )}
    </Card>
  )
}

/** Everything the viewer may press. The server's `can*` flags decide; `processes.work` hides them from a read-only user. */
function StepActions({
  detail,
  step,
  canWork,
  canManage,
  canFillBlanks,
}: {
  detail: ProcessCaseDetail
  step: ProcessCaseStep
  canWork: boolean
  canManage: boolean
  canFillBlanks: boolean
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const queryClient = useQueryClient()

  const complete = useCompleteProcessStep()
  const skip = useSkipProcessStep()
  const undo = useUndoProcessStep()
  const [dialog, setDialog] = useState<'skip' | 'undo' | null>(null)
  /**
   * The version this form has sent and not been refused on. `isPending` turns only on the next render, so Enter and a click
   * in one go would both get through without this; and a 200 stays final until the case arrives with a new version.
   */
  const sentVersion = useRef<number | null>(null)

  const target = { caseId: detail.id, stepCode: step.stepCode }
  const { version } = detail
  const locked = !PROCESS_WRITE_API_READY

  const entry = stepEntry(step, detail.subject, canWork && detail.status === 'Active')
  // Someone only reading a linked blank opens it from inside «… to'ldirilgan ma'lumot»; a second button would just repeat it.
  const readsLinkedBlank = entry?.kind === 'open' && step.linkedBlankSubmissionId !== null && !step.viewerHolds
  const showEntry =
    entry !== null && !readsLinkedBlank && (entry.target.kind !== 'createBlank' || canFillBlanks)

  const showComplete = canWork && step.canComplete && step.manual?.allowed === true
  const showSkip = canWork && step.canSkip
  const showUndo = canWork && step.canUndo

  if (!showEntry && !showComplete && !showSkip && !showUndo) return null

  const sent = sentVersion.current === version
  // The 200 came, the reload has finished and the version has not moved: only a failed reload leaves it like that.
  const reloadFailed = sent && complete.isSuccess

  const submit = (inputs: ProcessStepInputs, missingReason?: string) => {
    if (sentVersion.current === version) return
    sentVersion.current = version

    complete.mutate(
      {
        ...target,
        body: { version, inputs, ...(missingReason === undefined ? {} : { missingReason }) },
      },
      {
        onError: (error) => {
          sentVersion.current = null
          // The screen thought the data was there, so it is out of date: show what is really missing.
          if (problemCode(error) === 'jarayon.malumot_yetishmaydi') {
            void queryClient.invalidateQueries({ queryKey: processKeys.case(detail.id) })
          }
        },
      },
    )
  }

  const skipButton = showSkip && (
    <Button type="button" variant="secondary" disabled={locked} onClick={() => setDialog('skip')}>
      {t('processes.case.skip')}
    </Button>
  )

  return (
    <section className="space-y-3 border-[var(--border)] not-first:border-t not-first:pt-5">
      {showEntry && <StepEntryButton entry={entry} detail={detail} />}

      {complete.isError && (
        <Alert tone="danger">{errorMessage(problemCode(complete.error))}</Alert>
      )}

      {reloadFailed && (
        <Alert tone="warning">
          <p>{t('processes.case.reloadFailed')}</p>
          <Button
            type="button"
            size="sm"
            className="mt-2"
            onClick={() =>
              void queryClient.invalidateQueries({ queryKey: processKeys.case(detail.id) })
            }
          >
            {t('common.retry')}
          </Button>
        </Alert>
      )}

      {showComplete ? (
        <ProcessManualForm
          inputs={step.manual?.inputs ?? []}
          locked={locked}
          pending={complete.isPending || sent}
          finished={reloadFailed}
          canCloseWithReason={canManage}
          failure={complete.isError ? errorMessage(problemCode(complete.error)) : null}
          caseId={detail.id}
          onSubmit={submit}
        >
          {skipButton}
        </ProcessManualForm>
      ) : (
        skipButton
      )}

      {showUndo && (
        <Button type="button" variant="ghost" disabled={locked} onClick={() => setDialog('undo')}>
          <Undo2 />
          {t('processes.case.undo')}
        </Button>
      )}

      {locked && (showComplete || showSkip || showUndo) && (
        <p className="text-footnote text-[var(--fg-secondary)]">{t('processes.case.writeSoon')}</p>
      )}

      <SkipDialog
        open={dialog === 'skip'}
        reasonRequired={detail.legacy}
        pending={skip.isPending}
        failure={skip.isError ? errorMessage(problemCode(skip.error)) : null}
        onClose={() => setDialog(null)}
        onConfirm={(reason) =>
          skip.mutate(
            { ...target, body: { version, reason } },
            { onSuccess: () => setDialog(null) },
          )
        }
      />

      <UndoDialog
        open={dialog === 'undo'}
        pending={undo.isPending}
        failure={undo.isError ? errorMessage(problemCode(undo.error)) : null}
        onClose={() => setDialog(null)}
        onConfirm={() =>
          undo.mutate({ ...target, body: { version } }, { onSuccess: () => setDialog(null) })
        }
      />
    </section>
  )
}

/** «Boshlash» or «Ochish». A blank not created yet is created here first, and the draft opens (SPEC §11.5). */
function StepEntryButton({
  entry,
  detail,
}: {
  entry: ProcessStepEntry
  detail: ProcessCaseDetail
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()
  const createBlank = useCreateBlank()
  // One id per panel: a double click or a retry reaches the same draft (`blank-create-dialog.tsx`). Made on the press:
  // `crypto.randomUUID` is missing outside a secure context, and that must not take down a page for merely showing a step.
  const clientRequestId = useRef<string | null>(null)

  const go = () => {
    const { target } = entry

    switch (target.kind) {
      case 'internal':
        void navigate({ href: target.href })
        return
      case 'external':
        window.open(target.url, '_blank', 'noopener,noreferrer')
        return
      case 'createBlank': {
        const body = blankCreateBody(target, detail.subject)
        if (body === null) return
        clientRequestId.current ??= crypto.randomUUID()
        createBlank.mutate(
          { ...body, clientRequestId: clientRequestId.current },
          {
            onSuccess: (created) =>
              void navigate({
                to: '/blankalar/$submissionId',
                params: { submissionId: created.id },
              }),
          },
        )
      }
    }
  }

  return (
    <div className="space-y-2">
      {createBlank.isError && (
        <Alert tone="danger">{errorMessage(blankErrorCode(createBlank.error))}</Alert>
      )}
      <Button
        type="button"
        variant={entry.kind === 'start' ? 'primary' : 'secondary'}
        disabled={createBlank.isPending}
        onClick={go}
      >
        {createBlank.isPending ? (
          <LoaderCircle className="animate-spin" />
        ) : entry.target.kind === 'external' ? (
          <ExternalLink />
        ) : entry.kind === 'start' ? (
          <Play />
        ) : null}
        {entry.kind === 'start' ? t('processes.case.start') : t('processes.case.open')}
      </Button>
    </div>
  )
}

/** «Kerak emas». The reason is mandatory on a legacy case (§10 #11) and helpful everywhere else. */
function SkipDialog({
  open,
  reasonRequired,
  pending,
  failure,
  onClose,
  onConfirm,
}: {
  open: boolean
  reasonRequired: boolean
  pending: boolean
  failure: string | null
  onClose: () => void
  onConfirm: (reason: string | null) => void
}) {
  const { t } = useLocale()
  const [reason, setReason] = useState('')
  const trimmed = reason.trim()

  return (
    <Dialog
      open={open}
      onClose={onClose}
      dismissible={!pending}
      title={t('processes.case.skipTitle')}
      description={t('processes.case.skipText')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            disabled={pending || (reasonRequired && trimmed === '')}
            onClick={() => onConfirm(trimmed === '' ? null : trimmed)}
          >
            {pending && <LoaderCircle className="animate-spin" />}
            {t('processes.case.skip')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {failure !== null && <Alert tone="danger">{failure}</Alert>}
        <TextArea
          label={
            reasonRequired ? `${t('processes.case.skipReason')} *` : t('processes.case.skipReason')
          }
          value={reason}
          onChange={(event) => setReason(event.target.value)}
        />
      </div>
    </Dialog>
  )
}

function UndoDialog({
  open,
  pending,
  failure,
  onClose,
  onConfirm,
}: {
  open: boolean
  pending: boolean
  failure: string | null
  onClose: () => void
  onConfirm: () => void
}) {
  const { t } = useLocale()

  return (
    <Dialog
      open={open}
      onClose={onClose}
      dismissible={!pending}
      title={t('processes.case.undoTitle')}
      description={t('processes.case.undoText')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button type="button" variant="primary" disabled={pending} onClick={onConfirm}>
            {pending && <LoaderCircle className="animate-spin" />}
            {t('processes.case.undo')}
          </Button>
        </>
      }
    >
      {failure !== null && <Alert tone="danger">{failure}</Alert>}
    </Dialog>
  )
}
