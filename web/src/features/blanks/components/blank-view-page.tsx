import { useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { ArrowLeft, Ban, FileDown, FileText, LoaderCircle, RefreshCw, Save } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { saveBlobAs } from '@/shared/lib/download'
import { useReturnTo } from '@/shared/lib/return-to'
import { Alert, Badge, Button, Card, EmptyState, FileViewer, IconTile, PageHeader, Skeleton } from '@/shared/ui'

import {
  deleteBlankAttachment,
  fetchBlankSubmission,
  uploadBlankAttachment,
  type BlankStep,
  type BlankSubmission,
} from '../api/blanks-api'
import { blankKeys, useBlankPdf, useBlankSubmission, useBlankTemplateVersion, useSaveBlankAnswers } from '../api/blanks-queries'
import { blankErrorCode, blankServerError, isBlankConflict } from '../lib/blank-error'
import { searchBlankDocRef, searchBlankRef } from '../lib/blank-lookups'
import { blankNumberLabel } from '../lib/blank-permissions'
import { STATUS_KEY, STATUS_TONE, sectionFill } from '../lib/blank-presentation'
import type { Answers, BlankDefinition } from '../lib/definition-types'
import { useBlankAttachmentViewer } from './blank-attachment-viewer'
import { CancelDialog } from './blank-fill-page'
import { BlankStepsPanel, type StepAction } from './blank-steps-panel'
import { BlankForm, type BlankServerError } from './renderer/blank-form'
import type { RemoveFile, UploadFile } from './renderer/renderer-context'
import { StepApproveDialog } from './step-approve-dialog'
import { StepCommentDialog } from './step-comment-dialog'
import { StepSignDialog } from './step-sign-dialog'

/**
 * A submitted / ended blank (route `/blankalar/$submissionId` when it is not an editable draft).
 *
 * The document prints read-only; `open` sections (`fill.at = open`) the caller may still edit
 * (`can.editableSections`) are editable inline with an explicit «Save» (PUT #9). A step's own
 * sections are filled inside its approve / sign dialog and travel with the act (#12 / #13).
 * Every action button comes from `can` — permissions are never re-derived here.
 */
export function BlankViewPage({ submissionId }: { submissionId: string }) {
  const { t, num, date } = useLocale()
  const errorMessage = useErrorMessage()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const back = useReturnTo(() => void navigate({ to: '/blankalar' }))

  const detail = useBlankSubmission(submissionId)
  const version = useBlankTemplateVersion(detail.data?.templateVersionId)
  const save = useSaveBlankAnswers()

  const submission = detail.data
  const definition: BlankDefinition | undefined = version.data?.definition

  /* ── open sections: local working copy, explicit save ────────────────── */
  const [answers, setAnswers] = useState<Answers>({})
  const [dirty, setDirty] = useState(false)
  const [conflict, setConflict] = useState(false)
  const [serverError, setServerError] = useState<BlankServerError | null>(null)

  useEffect(() => {
    if (submission && !dirty) setAnswers(submission.answers)
  }, [dirty, submission])

  const openSections = useMemo(() => {
    if (!submission || !definition || conflict) return []
    const editable = new Set(submission.can.editableSections)
    return definition.sections.filter((s) => sectionFill(s).at === 'open' && editable.has(s.id)).map((s) => s.id)
  }, [conflict, definition, submission])

  const adopt = useCallback(
    (result: BlankSubmission) => {
      queryClient.setQueryData(blankKeys.detail(submissionId), result)
    },
    [queryClient, submissionId],
  )

  const saveOpen = useCallback(async (): Promise<boolean> => {
    if (!dirty || !submission) return true
    try {
      const saved = await save.mutateAsync({ id: submissionId, expectedVersion: submission.version, answers })
      adopt(saved)
      setDirty(false)
      setServerError(null)
      return true
    } catch (error) {
      if (isBlankConflict(error)) setConflict(true)
      else setServerError(blankServerError(error))
      return false
    }
  }, [adopt, answers, dirty, save, submission, submissionId])

  const reload = useCallback(async () => {
    const result = await detail.refetch()
    if (result.data) {
      setDirty(false)
      setAnswers(result.data.answers)
      setConflict(false)
      setServerError(null)
    }
  }, [detail])

  /* ── files (TODO(S7): #17–#19 land in parallel) ─────────────────────── */
  const refreshAfterFileChange = useCallback(async () => {
    adopt(await fetchBlankSubmission(submissionId))
  }, [adopt, submissionId])

  const uploadFile = useCallback<UploadFile>(
    async (fieldPath, file) => {
      const stored = await uploadBlankAttachment(submissionId, fieldPath, file)
      await refreshAfterFileChange()
      return stored
    },
    [refreshAfterFileChange, submissionId],
  )

  const removeFile = useCallback<RemoveFile>(
    async (attachment) => {
      await deleteBlankAttachment(submissionId, attachment.id, submission?.version ?? 0)
      await refreshAfterFileChange()
    },
    [refreshAfterFileChange, submission?.version, submissionId],
  )

  const viewer = useBlankAttachmentViewer(submissionId, submission?.attachments ?? [])

  /* ── PDF (#20, TODO(S7)) ─────────────────────────────────────────────── */
  const [pdfOpen, setPdfOpen] = useState(false)
  const pdf = useBlankPdf(submissionId, submission?.version ?? 0, pdfOpen && submission !== undefined)

  /* ── step dialogs ────────────────────────────────────────────────────── */
  const [acting, setActing] = useState<{ action: StepAction; step: BlankStep } | null>(null)
  const [cancelling, setCancelling] = useState(false)

  const startAction = useCallback(
    async (action: StepAction, step: BlankStep) => {
      // Unsaved open-section edits first — the act bumps the version
      if (await saveOpen()) setActing({ action, step })
    },
    [saveOpen],
  )

  const finish = useCallback(
    (result: BlankSubmission) => {
      adopt(result)
      setDirty(false)
      setActing(null)
      setCancelling(false)
    },
    [adopt],
  )

  /* ── states ─────────────────────────────────────────────────────────── */
  if (detail.isError || version.isError) {
    return (
      <div className="space-y-6">
        <Button variant="ghost" size="sm" onClick={back.go} className="-ml-2">
          <ArrowLeft />
          {back.label}
        </Button>
        <EmptyState
          icon={FileText}
          title={t('blanks.fill.loadFailed')}
          description={errorMessage(blankErrorCode(detail.error ?? version.error))}
        />
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
          <Skeleton className="h-24 w-full" />
        </Card>
        <Card className="space-y-3 p-5">
          <Skeleton className="h-6 w-1/4" />
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-12 w-full" />
        </Card>
      </div>
    )
  }

  const title = blankNumberLabel(submission.templateCode, submission.number)
  const busy = save.isPending
  const fileProps = {
    uploadFile,
    removeFile,
    openFile: viewer.open,
  }

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
              {title} · {submission.templateTitle}
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
            {submission.dueDate !== null && (
              <span className="tabular text-[var(--fg-secondary)]">· {t('blanks.view.due', { date: date(submission.dueDate) })}</span>
            )}
            {submission.missingCount > 0 && (
              <Badge tone="warning">{t('blanks.list.missingN', { n: num(submission.missingCount) })}</Badge>
            )}
          </span>
        }
        actions={
          <>
            {dirty && (
              <Button variant="primary" onClick={() => void saveOpen()} disabled={busy || conflict}>
                {busy ? <LoaderCircle className="animate-spin" /> : <Save />}
                {t('blanks.view.save')}
              </Button>
            )}
            {submission.can.cancel && (
              <Button onClick={() => setCancelling(true)} disabled={busy}>
                <Ban />
                {t('blanks.fill.cancel')}
              </Button>
            )}
            <Button onClick={() => setPdfOpen(true)}>
              <FileDown />
              {t('blanks.view.pdf')}
            </Button>
          </>
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
      {submission.status === 'Cancelled' && submission.cancelReason !== null && (
        <Alert tone="info">{t('blanks.fill.cancelledReason', { reason: submission.cancelReason })}</Alert>
      )}
      {submission.status === 'Draft' && submission.returnComment !== null && (
        <Alert tone="warning">{t('blanks.fill.returned', { comment: submission.returnComment })}</Alert>
      )}
      {submission.status === 'InApproval' && submission.can.actionableSteps.length > 0 && (
        <Alert tone="info">{t('blanks.view.yourTurn')}</Alert>
      )}

      <BlankForm
        definition={definition}
        answers={answers}
        onChange={(next) => {
          setAnswers(next)
          setDirty(true)
        }}
        editableSections={openSections}
        readOnly={openSections.length === 0}
        computed={submission.computed}
        serverError={serverError}
        attachments={submission.attachments}
        searchRef={searchBlankRef}
        searchDocRef={searchBlankDocRef}
        uploadFile={openSections.length > 0 ? uploadFile : undefined}
        removeFile={openSections.length > 0 ? removeFile : undefined}
        openFile={viewer.open}
      />

      <BlankStepsPanel
        submission={submission}
        definition={definition}
        disabled={busy || conflict}
        onAction={(action, step) => void startAction(action, step)}
      />

      {acting?.action === 'approve' && (
        <StepApproveDialog
          submission={submission}
          definition={definition}
          step={acting.step}
          onClose={() => setActing(null)}
          onDone={finish}
          {...fileProps}
        />
      )}
      {acting?.action === 'sign' && (
        <StepSignDialog
          submission={submission}
          definition={definition}
          step={acting.step}
          onClose={() => setActing(null)}
          onDone={finish}
          {...fileProps}
        />
      )}
      {(acting?.action === 'return' || acting?.action === 'reject') && (
        <StepCommentDialog
          kind={acting.action}
          submission={submission}
          step={acting.step}
          onClose={() => setActing(null)}
          onDone={finish}
        />
      )}
      {cancelling && (
        <CancelDialog
          submission={submission}
          expectedVersion={() => submission.version}
          onClose={() => setCancelling(false)}
          onDone={finish}
        />
      )}

      {viewer.element}
      <FileViewer
        open={pdfOpen}
        onClose={() => setPdfOpen(false)}
        title={`${title}.pdf`}
        subtitle={submission.templateTitle}
        kind="pdf"
        blob={pdf.data ?? null}
        error={pdf.isError}
        onDownload={pdf.data ? () => pdf.data && saveBlobAs(pdf.data, `${title.replace(/[^\p{L}\p{N}-]+/gu, '_')}.pdf`) : undefined}
      />
    </div>
  )
}
