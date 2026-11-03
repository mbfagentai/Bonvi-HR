import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { ArrowLeft, Ban, CircleCheck, FileText, LoaderCircle, RefreshCw, Send } from 'lucide-react'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { useReturnTo } from '@/shared/lib/return-to'
import { Alert, Badge, Button, Card, Dialog, EmptyState, IconTile, PageHeader, Skeleton, TextArea } from '@/shared/ui'

import {
  deleteBlankAttachment,
  fetchBlankSubmission,
  putBlankAnswers,
  uploadBlankAttachment,
  type BlankSubmission,
} from '../api/blanks-api'
import {
  blankKeys,
  useBlankSubmission,
  useBlankTemplateVersion,
  useCancelBlank,
  useSaveBlankAnswers,
  useSubmitBlank,
} from '../api/blanks-queries'
import { validateAnswers, type FieldErrors } from '../lib/answers-zod'
import { blankErrorCode, blankServerError, isBlankConflict } from '../lib/blank-error'
import { searchBlankDocRef, searchBlankRef } from '../lib/blank-lookups'
import { blankNumberLabel } from '../lib/blank-permissions'
import { STATUS_KEY, STATUS_TONE } from '../lib/blank-presentation'
import type {
  Answers,
  BlankAttachmentDto,
  BlankDefinition,
  BlankStepState,
  ComputedValues,
} from '../lib/definition-types'
import { useBlankAttachmentViewer } from './blank-attachment-viewer'
import { BlankForm, type BlankServerError } from './renderer/blank-form'
import type { RemoveFile, UploadFile } from './renderer/renderer-context'

/** SPEC §14 S11: autosave 2 s after the last keystroke. */
const AUTOSAVE_MS = 2000

const EMPTY_ERRORS: FieldErrors = {}

type SaveState = 'idle' | 'pending' | 'saving' | 'saved' | 'failed'

const STEP_TONE: Record<BlankStepState, 'neutral' | 'warning' | 'success' | 'danger'> = {
  Waiting: 'neutral',
  Open: 'warning',
  Signed: 'success',
  Skipped: 'neutral',
  Rejected: 'danger',
}

/**
 * One blank (route `/blankalar/$submissionId`).
 *
 * Draft with `can.edit` → the fill form: autosave PUT #9 two seconds after the last change
 * with `expectedVersion`; 409 `blanka.ozgargan` freezes editing behind a «reload» banner (the
 * other save is never overwritten); submit validates the editable sections with
 * `requireFilled` first, flushes the pending save, then POST #10.
 * The route opens `<BlankViewPage>` (steps, signing, PDF) for every other status; this page
 * still renders read-only if it is ever given one (e.g. the draft was just cancelled).
 */
export function BlankFillPage({ submissionId }: { submissionId: string }) {
  const { t, num, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const back = useReturnTo(() => void navigate({ to: '/blankalar' }))

  const detail = useBlankSubmission(submissionId)
  const version = useBlankTemplateVersion(detail.data?.templateVersionId)
  const save = useSaveBlankAnswers()
  const submitBlank = useSubmitBlank()

  /* ── local working copy ─────────────────────────────────────────────── */
  const [answers, setAnswers] = useState<Answers>({})
  const [computed, setComputed] = useState<ComputedValues>({})
  const [attachments, setAttachments] = useState<BlankAttachmentDto[]>([])
  const [saveState, setSaveState] = useState<SaveState>('idle')
  const [conflict, setConflict] = useState(false)
  const [serverError, setServerError] = useState<BlankServerError | null>(null)
  const [submitErrors, setSubmitErrors] = useState<FieldErrors | null>(null)
  const [cancelling, setCancelling] = useState(false)

  const answersRef = useRef<Answers>({})
  const versionRef = useRef(-1)
  const editSeq = useRef(0)
  const savedSeq = useRef(0)
  const inflight = useRef<Promise<boolean> | null>(null)
  const timer = useRef<number | undefined>(undefined)

  const adopt = useCallback((data: BlankSubmission) => {
    answersRef.current = data.answers
    versionRef.current = data.version
    savedSeq.current = editSeq.current
    setAnswers(data.answers)
    setComputed(data.computed)
    setAttachments(data.attachments)
  }, [])

  // First load, and every newer server version while nothing local is unsaved.
  // A refetch with the SAME version (global invalidation after our own save) is ignored,
  // so it never moves the cursor under the user's hands.
  useEffect(() => {
    const data = detail.data
    if (!data) return
    const unsaved = editSeq.current !== savedSeq.current
    if (versionRef.current < 0 || (!unsaved && data.version > versionRef.current)) adopt(data)
  }, [adopt, detail.data])

  const submission = detail.data
  const definition: BlankDefinition | undefined = version.data?.definition
  const editing = submission !== undefined && submission.status === 'Draft' && submission.can.edit && !conflict
  const editableSections = useMemo(() => (editing ? (submission?.can.editableSections ?? []) : []), [editing, submission])

  /* ── autosave ───────────────────────────────────────────────────────── */
  const flush = useCallback(async (): Promise<boolean> => {
    window.clearTimeout(timer.current)
    timer.current = undefined
    // One request at a time: wait for the running save, then send what is still unsaved
    while (inflight.current) await inflight.current
    if (editSeq.current === savedSeq.current) return true
    const seq = editSeq.current
    const run = (async (): Promise<boolean> => {
      setSaveState('saving')
      try {
        const saved = await save.mutateAsync({
          id: submissionId,
          expectedVersion: versionRef.current,
          answers: answersRef.current,
        })
        versionRef.current = saved.version
        savedSeq.current = seq
        setComputed(saved.computed)
        setServerError(null)
        queryClient.setQueryData(blankKeys.detail(submissionId), saved)
        setSaveState(editSeq.current === seq ? 'saved' : 'pending')
        return true
      } catch (error) {
        if (isBlankConflict(error)) setConflict(true)
        else setServerError(blankServerError(error))
        setSaveState('failed')
        return false
      }
    })()
    inflight.current = run
    try {
      return await run
    } finally {
      inflight.current = null
    }
  }, [queryClient, save, submissionId])

  const change = useCallback(
    (next: Answers) => {
      answersRef.current = next
      editSeq.current += 1
      setAnswers(next)
      setSaveState('pending')
      window.clearTimeout(timer.current)
      timer.current = window.setTimeout(() => {
        timer.current = undefined
        void flush()
      }, AUTOSAVE_MS)
    },
    [flush],
  )

  // Leaving with unsaved edits: the browser asks; an in-app navigation saves best-effort.
  useEffect(() => {
    const warn = (event: BeforeUnloadEvent) => {
      if (editSeq.current !== savedSeq.current) event.preventDefault()
    }
    window.addEventListener('beforeunload', warn)
    return () => {
      window.removeEventListener('beforeunload', warn)
      window.clearTimeout(timer.current)
      if (editSeq.current !== savedSeq.current && versionRef.current >= 0 && inflight.current === null)
        void putBlankAnswers(submissionId, versionRef.current, answersRef.current).catch(() => undefined)
    }
  }, [submissionId])

  const reload = useCallback(async () => {
    window.clearTimeout(timer.current)
    const result = await detail.refetch()
    if (result.data) {
      editSeq.current = savedSeq.current
      adopt(result.data)
      setConflict(false)
      setServerError(null)
      setSaveState('idle')
    }
  }, [adopt, detail])

  /* ── submit ─────────────────────────────────────────────────────────── */
  const liveSubmitErrors = useMemo(
    () =>
      submitErrors === null || !definition
        ? EMPTY_ERRORS
        : validateAnswers(definition, answers, { requireFilled: true, sections: editableSections }),
    [answers, definition, editableSections, submitErrors],
  )

  const submit = useCallback(async () => {
    if (!definition || !submission) return
    const errors = validateAnswers(definition, answersRef.current, { requireFilled: true, sections: editableSections })
    setSubmitErrors(errors)
    if (Object.keys(errors).length > 0) return
    if (!(await flush())) return
    setServerError(null)
    submitBlank.mutate(
      { id: submissionId, expectedVersion: versionRef.current },
      {
        onSuccess: (result) => {
          queryClient.setQueryData(blankKeys.detail(submissionId), result)
          adopt(result)
          setSubmitErrors(null)
          setSaveState('idle')
        },
        onError: (error) => {
          if (isBlankConflict(error)) setConflict(true)
          else setServerError(blankServerError(error))
        },
      },
    )
  }, [adopt, definition, editableSections, flush, queryClient, submission, submissionId, submitBlank])

  /* ── files (#17–#19) ─────────────────────────────────────────────────── */
  const refreshAfterFileChange = useCallback(async () => {
    const fresh = await fetchBlankSubmission(submissionId)
    // The bump is our own upload/removal — keep local answers, take the new version
    versionRef.current = fresh.version
    setAttachments(fresh.attachments)
    queryClient.setQueryData(blankKeys.detail(submissionId), fresh)
  }, [queryClient, submissionId])

  const uploadFile = useCallback<UploadFile>(
    async (fieldPath, file) => {
      if (!(await flush())) throw new Error('save-pending')
      const stored = await uploadBlankAttachment(submissionId, fieldPath, file)
      await refreshAfterFileChange()
      return stored
    },
    [flush, refreshAfterFileChange, submissionId],
  )

  const removeFile = useCallback<RemoveFile>(
    async (attachment) => {
      if (!(await flush())) throw new Error('save-pending')
      await deleteBlankAttachment(submissionId, attachment.id, versionRef.current)
      await refreshAfterFileChange()
    },
    [flush, refreshAfterFileChange, submissionId],
  )

  const viewer = useBlankAttachmentViewer(submissionId, attachments)
  const openFile = viewer.open

  /* ── states ─────────────────────────────────────────────────────────── */
  if (detail.isError || version.isError) {
    const code = blankErrorCode(detail.error ?? version.error)
    return (
      <div className="space-y-6">
        <Button variant="ghost" size="sm" onClick={back.go} className="-ml-2">
          <ArrowLeft />
          {back.label}
        </Button>
        <EmptyState icon={FileText} title={t('blanks.fill.loadFailed')} description={errorMessage(code)} />
      </div>
    )
  }

  if (!submission || !definition) {
    return (
      <div className="space-y-6" aria-busy="true">
        <Skeleton className="h-8 w-40" />
        <Skeleton className="h-10 w-2/3" />
        <Card className="space-y-4 p-5">
          <Skeleton className="h-6 w-1/3" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-24 w-full" />
        </Card>
      </div>
    )
  }

  const isDraft = submission.status === 'Draft'
  const busy = submitBlank.isPending

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" onClick={back.go} className="-ml-2">
        <ArrowLeft />
        {back.label}
      </Button>

      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={FileText} tone="brand" size="lg" />
            <span className="min-w-0 truncate">
              {blankNumberLabel(submission.templateCode, submission.number)} · {submission.templateTitle}
            </span>
          </span>
        }
        subtitle={
          <span className="flex flex-wrap items-center gap-2">
            <Badge tone={STATUS_TONE[submission.status]}>{t(STATUS_KEY[submission.status])}</Badge>
            {submission.subject.fullName !== null && <span>{submission.subject.fullName}</span>}
            {submission.departmentName !== null && (
              <span className="text-[var(--fg-secondary)]">· {submission.departmentName}</span>
            )}
            {submission.missingCount > 0 && (
              <Badge tone="warning">{t('blanks.list.missingN', { n: num(submission.missingCount) })}</Badge>
            )}
          </span>
        }
        actions={
          editing ? (
            <>
              <SaveIndicator state={saveState} />
              {submission.can.cancel && (
                <Button onClick={() => setCancelling(true)} disabled={busy}>
                  <Ban />
                  {t('blanks.fill.cancel')}
                </Button>
              )}
              {submission.can.submit && (
                <Button variant="primary" onClick={() => void submit()} disabled={busy || saveState === 'saving'}>
                  {busy ? <LoaderCircle className="animate-spin" /> : <Send />}
                  {t('blanks.fill.submit')}
                </Button>
              )}
            </>
          ) : submission.can.cancel ? (
            <Button onClick={() => setCancelling(true)}>
              <Ban />
              {t('blanks.fill.cancel')}
            </Button>
          ) : null
        }
      />

      {conflict && (
        <Alert tone="warning">
          <span className="flex flex-wrap items-center justify-between gap-3">
            <span>{t('blanks.fill.conflict')}</span>
            <Button size="sm" onClick={() => void reload()} disabled={detail.isFetching}>
              <RefreshCw className={detail.isFetching ? 'animate-spin' : undefined} />
              {t('blanks.fill.reload')}
            </Button>
          </span>
        </Alert>
      )}
      {isDraft && submission.returnComment !== null && (
        <Alert tone="warning">{t('blanks.fill.returned', { comment: submission.returnComment })}</Alert>
      )}
      {submission.status === 'Cancelled' && submission.cancelReason !== null && (
        <Alert tone="info">{t('blanks.fill.cancelledReason', { reason: submission.cancelReason })}</Alert>
      )}
      {submitErrors !== null && Object.keys(liveSubmitErrors).length > 0 && (
        <Alert tone="danger">{t('blanks.fill.fixErrors', { n: num(Object.keys(liveSubmitErrors).length) })}</Alert>
      )}

      <BlankForm
        definition={definition}
        answers={answers}
        onChange={change}
        editableSections={editableSections}
        readOnly={!editing}
        computed={computed}
        errors={liveSubmitErrors}
        serverError={serverError}
        attachments={attachments}
        searchRef={searchBlankRef}
        searchDocRef={searchBlankDocRef}
        uploadFile={editing ? uploadFile : undefined}
        removeFile={editing ? removeFile : undefined}
        openFile={openFile}
      />

      {/* Read-only fallback — a Draft has no steps; the route shows <BlankViewPage> otherwise. */}
      {submission.steps.length > 0 && (
        <Card className="p-5">
          <h2 className="mb-3 text-headline font-semibold">{t('blanks.fill.steps')}</h2>
          <ul className="divide-y divide-[var(--border)]">
            {submission.steps.map((step) => (
              <li key={step.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2.5">
                <span className="min-w-0 flex-1 truncate font-medium" title={step.title}>
                  {step.title}
                </span>
                {step.actedByName !== null && (
                  <span className="text-footnote text-[var(--fg-secondary)]">{step.actedByName}</span>
                )}
                {step.actedAt !== null && (
                  <span className="tabular text-footnote text-[var(--fg-secondary)]">{dateTime(step.actedAt)}</span>
                )}
                <Badge tone={STEP_TONE[step.state]}>{t(`blanks.fill.stepState.${step.state}`)}</Badge>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {cancelling && (
        <CancelDialog
          submission={submission}
          onClose={() => setCancelling(false)}
          onDone={(result) => {
            window.clearTimeout(timer.current)
            editSeq.current = savedSeq.current
            queryClient.setQueryData(blankKeys.detail(submissionId), result)
            adopt(result)
            setCancelling(false)
          }}
          expectedVersion={() => versionRef.current}
        />
      )}
      {viewer.element}
    </div>
  )
}

function SaveIndicator({ state }: { state: SaveState }) {
  const { t } = useLocale()
  if (state === 'idle') return null
  const text =
    state === 'saving' ? t('blanks.fill.saving') : state === 'saved' ? t('blanks.fill.saved') : state === 'failed' ? t('blanks.fill.saveFailed') : t('blanks.fill.unsaved')
  return (
    <span
      className={
        state === 'failed'
          ? 'flex items-center gap-1.5 self-center text-footnote text-ios-red'
          : 'flex items-center gap-1.5 self-center text-footnote text-[var(--fg-secondary)]'
      }
      aria-live="polite"
    >
      {state === 'saving' && <LoaderCircle className="size-4 animate-spin" />}
      {state === 'saved' && <CircleCheck className="size-4" />}
      {text}
    </span>
  )
}

/** Cancel with a required reason — POST #11 (shared with the view page). */
export function CancelDialog({
  submission,
  expectedVersion,
  onClose,
  onDone,
}: {
  submission: BlankSubmission
  expectedVersion: () => number
  onClose: () => void
  onDone: (result: BlankSubmission) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const cancel = useCancelBlank()
  const [reason, setReason] = useState('')
  const [failure, setFailure] = useState<string | null>(null)

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!cancel.isPending}
      title={t('blanks.fill.cancelTitle')}
      description={t('blanks.fill.cancelText')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={cancel.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            variant="danger"
            disabled={cancel.isPending || reason.trim() === ''}
            onClick={() =>
              cancel.mutate(
                { id: submission.id, expectedVersion: expectedVersion(), reason: reason.trim() },
                { onSuccess: onDone, onError: (error) => setFailure(blankErrorCode(error)) },
              )
            }
          >
            {cancel.isPending && <LoaderCircle className="animate-spin" />}
            {t('blanks.fill.cancel')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}
        <TextArea
          label={t('blanks.fill.cancelReason')}
          value={reason}
          maxLength={2000}
          rows={3}
          disabled={cancel.isPending}
          onChange={(event) => setReason(event.target.value)}
        />
      </div>
    </Dialog>
  )
}
