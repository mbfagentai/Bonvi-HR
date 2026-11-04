import { LoaderCircle, Undo2, XCircle } from 'lucide-react'
import { useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog, TextArea } from '@/shared/ui'

import type { BlankStep, BlankSubmission } from '../api/blanks-api'
import { useRejectBlankStep, useReturnBlankStep } from '../api/blanks-queries'
import { blankErrorCode } from '../lib/blank-error'

/**
 * Return to draft (#14) or reject (#15) — both end the step with a required comment.
 * Return: the blank goes back to Draft, the comment shows on the fill page until the next
 * submit. Reject: the blank ends as Rejected — the text says it cannot be undone.
 */
export function StepCommentDialog({
  kind,
  submission,
  step,
  onClose,
  onDone,
}: {
  kind: 'return' | 'reject'
  submission: BlankSubmission
  step: BlankStep
  onClose: () => void
  onDone: (result: BlankSubmission) => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const returnStep = useReturnBlankStep()
  const rejectStep = useRejectBlankStep()
  const mutation = kind === 'return' ? returnStep : rejectStep
  const [comment, setComment] = useState('')
  const [failure, setFailure] = useState<string | null>(null)

  const run = () => {
    if (comment.trim() === '') return
    setFailure(null)
    mutation.mutate(
      { id: submission.id, stepCode: step.stepCode, body: { expectedVersion: submission.version, comment: comment.trim() } },
      { onSuccess: onDone, onError: (error) => setFailure(blankErrorCode(error)) },
    )
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!mutation.isPending}
      title={
        kind === 'return'
          ? t('blanks.steps.returnTitle', { step: step.title })
          : t('blanks.steps.rejectTitle', { step: step.title })
      }
      description={kind === 'return' ? t('blanks.steps.returnText') : t('blanks.steps.rejectText')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={mutation.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            variant={kind === 'reject' ? 'danger' : 'primary'}
            onClick={run}
            disabled={mutation.isPending || comment.trim() === ''}
          >
            {mutation.isPending ? <LoaderCircle className="animate-spin" /> : kind === 'reject' ? <XCircle /> : <Undo2 />}
            {kind === 'return' ? t('blanks.steps.return') : t('blanks.steps.reject')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}
        <TextArea
          label={t('blanks.steps.commentRequired')}
          value={comment}
          rows={4}
          maxLength={2000}
          autoFocus
          disabled={mutation.isPending}
          onChange={(event) => setComment(event.target.value)}
        />
      </div>
    </Dialog>
  )
}
