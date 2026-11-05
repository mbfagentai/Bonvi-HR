import { useNavigate } from '@tanstack/react-router'
import { ChevronRight, ExternalLink, LoaderCircle, Play, Undo2 } from 'lucide-react'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { useCreateBlank } from '@/features/blanks/api/blanks-queries'
import { blankErrorCode } from '@/features/blanks/lib/blank-error'
import { BLANKS_FILL } from '@/features/blanks/lib/blank-permissions'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { todayIso } from '@/shared/lib/today'
import { Alert, Badge, Button, Dialog, TextArea } from '@/shared/ui'

import type { ProcessCaseDetail, ProcessCaseStep } from '../api/processes-api'
import {
  useCompleteProcessStep,
  useSkipProcessStep,
  useUndoProcessStep,
} from '../api/processes-queries'
import { PROCESSES_WORK } from '../lib/permissions'
import { describeRecordedInputs } from '../lib/process-manual-form'
import {
  blankCreateBody,
  pendingPrereqTitles,
  PROCESS_WRITE_API_READY,
  stepEntry,
  type ProcessStepEntry,
} from '../lib/process-step'
import {
  ProcessBlankChip,
  ProcessDueChip,
  ProcessHolderLabel,
  ProcessStateBadge,
  ProcessWarningChips,
  useProcessRoleLabel,
} from './process-chips'
import { ProcessManualForm } from './process-manual-form'

/**
 * One step of a case (SPEC §11.5): what it is, why it stands, what to do, and the buttons to do it. It takes a loaded
 * case, so any screen holding `useProcessCase(caseId)` can draw it. The `key` starts a fresh form and fresh errors for
 * every step.
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
  const { t, date, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const roleLabel = useProcessRoleLabel()
  const { hasPermission } = useAuth()

  const finished = step.state === 'Done' || step.state === 'Skipped'
  const waitingForOthers = step.state === 'Locked' || step.state === 'Waiting'
  const prereqs = waitingForOthers ? pendingPrereqTitles(step, detail.steps) : []
  const recorded = describeRecordedInputs(step.manual?.inputs ?? [], step.payload)

  return (
    <div className="space-y-4">
      <header className="space-y-2">
        <h2 className="text-headline font-semibold">{step.title}</h2>
        <div className="flex flex-wrap items-center gap-1.5">
          <ProcessStateBadge state={step.state} />
          {!finished && (
            <ProcessDueChip dueDate={step.dueDate} daysLate={step.daysLate} today={todayIso()} />
          )}
          <ProcessBlankChip code={step.blankCode} title={step.blankTitle} />
          {step.optional && <Badge tone="neutral">{t('processes.case.optional')}</Badge>}
          <ProcessWarningChips codes={step.warnings} />
        </div>
      </header>

      <dl className="space-y-1 text-subhead">
        <div className="flex flex-wrap gap-x-2">
          <dt className="text-[var(--fg-secondary)]">{t('processes.case.holder')}:</dt>
          <dd>
            <ProcessHolderLabel holder={step.holder} />
          </dd>
        </div>
        {step.doneAt !== null && (
          <div className="flex flex-wrap gap-x-2">
            <dt className="text-[var(--fg-secondary)]">{t(`processes.state.${step.state}`)}:</dt>
            <dd className="tabular">
              {dateTime(step.doneAt)}
              {step.doneByName !== null && ` · ${step.doneByName}`}
            </dd>
          </div>
        )}
        {step.resolution !== null && step.resolution !== 'Manual' && (
          <div className="flex flex-wrap gap-x-2">
            <dd className="text-[var(--fg-secondary)]">
              {t(`processes.case.resolution.${step.resolution}`)}
            </dd>
          </div>
        )}
      </dl>

      {step.state === 'Blocked' && (
        <Alert tone="danger" title={errorMessage(step.gateErrorCode, step.gateCode ?? undefined)}>
          {step.resolver !== null && (
            <span>
              {t('processes.case.resolver')}: <ProcessHolderLabel holder={step.resolver} />
            </span>
          )}
        </Alert>
      )}

      {!step.viewerHolds && !finished && (
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('processes.case.notHolder', { role: roleLabel(step.holder.role) })}
        </p>
      )}

      {prereqs.length > 0 && (
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('processes.case.prereqs', { titles: prereqs.join('; ') })}
        </p>
      )}

      {recorded.length > 0 && (
        <section className="space-y-1.5">
          <h3 className="text-footnote font-semibold text-[var(--fg-secondary)]">
            {t('processes.case.recorded')}
          </h3>
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
        </section>
      )}

      {step.guide.length > 0 && (
        <section className="space-y-1.5">
          <h3 className="text-footnote font-semibold text-[var(--fg-secondary)]">
            {t('processes.case.guide')}
          </h3>
          <ol className="list-decimal space-y-1 pl-5 text-subhead">
            {step.guide.map((line, index) => (
              <li key={index}>{line}</li>
            ))}
          </ol>
        </section>
      )}

      {step.sourceRef !== null && (
        <details className="group text-subhead">
          <summary className="flex cursor-pointer list-none items-center gap-1 text-footnote font-semibold text-[var(--fg-secondary)]">
            <ChevronRight
              aria-hidden
              className="size-4 transition-transform group-open:rotate-90"
            />
            {t('processes.case.source')}
          </summary>
          <p className="mt-1.5 pl-5 text-[var(--fg-secondary)]">{step.sourceRef}</p>
        </details>
      )}

      <StepActions
        detail={detail}
        step={step}
        canWork={hasPermission(PROCESSES_WORK)}
        canFillBlanks={hasPermission(BLANKS_FILL)}
      />
    </div>
  )
}

/** Everything the viewer may press. The server's `can*` flags decide; `processes.work` hides them from a read-only user. */
function StepActions({
  detail,
  step,
  canWork,
  canFillBlanks,
}: {
  detail: ProcessCaseDetail
  step: ProcessCaseStep
  canWork: boolean
  canFillBlanks: boolean
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  const complete = useCompleteProcessStep()
  const skip = useSkipProcessStep()
  const undo = useUndoProcessStep()
  const [dialog, setDialog] = useState<'skip' | 'undo' | null>(null)

  const target = { caseId: detail.id, stepCode: step.stepCode }
  const expectedVersion = detail.version
  const locked = !PROCESS_WRITE_API_READY

  const entry = stepEntry(step, detail.subject, canWork && detail.status === 'Active')
  const showEntry = entry !== null && (entry.target.kind !== 'createBlank' || canFillBlanks)

  const showComplete = canWork && step.canComplete && step.manual?.allowed === true
  const showSkip = canWork && step.canSkip
  const showUndo = canWork && step.canUndo

  if (!showEntry && !showComplete && !showSkip && !showUndo) return null

  const skipButton = showSkip && (
    <Button type="button" variant="secondary" disabled={locked} onClick={() => setDialog('skip')}>
      {t('processes.case.skip')}
    </Button>
  )

  return (
    <section className="space-y-3 border-t border-[var(--border)] pt-4">
      {showEntry && <StepEntryButton entry={entry} detail={detail} />}

      {complete.isError && (
        <Alert tone="danger">{errorMessage(problemCode(complete.error))}</Alert>
      )}

      {showComplete ? (
        <ProcessManualForm
          inputs={step.manual?.inputs ?? []}
          locked={locked}
          pending={complete.isPending}
          onSubmit={(inputs) =>
            complete.mutate({ ...target, body: { inputs, expectedVersion } })
          }
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
            { ...target, body: { reason, expectedVersion } },
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
          undo.mutate({ ...target, body: { expectedVersion } }, { onSuccess: () => setDialog(null) })
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
  // One id per panel: a double click or a retry reaches the same draft (`blank-create-dialog.tsx`).
  const [clientRequestId] = useState(() => crypto.randomUUID())

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
        createBlank.mutate(
          { ...body, clientRequestId },
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
