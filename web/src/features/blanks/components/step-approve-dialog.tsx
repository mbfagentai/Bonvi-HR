import { CheckCircle2, LoaderCircle } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, DateField, Dialog, Select, TextArea, TextField } from '@/shared/ui'

import type { BlankStep, BlankStepApproveInput, BlankSubmission } from '../api/blanks-api'
import { useApproveBlankStep } from '../api/blanks-queries'
import { validateAnswers, type FieldErrors } from '../lib/answers-zod'
import { blankErrorCode, blankServerError } from '../lib/blank-error'
import { searchBlankDocRef, searchBlankRef } from '../lib/blank-lookups'
import { sectionFill } from '../lib/blank-presentation'
import type { Answers, BlankAttachmentDto, BlankDefinition, JsonValue } from '../lib/definition-types'
import { stepDefinition } from './blank-steps-panel'
import { BlankForm, type BlankServerError } from './renderer/blank-form'
import type { RemoveFile, UploadFile } from './renderer/renderer-context'

const EMPTY_ERRORS: FieldErrors = {}

/** What every step dialog needs from the page (files of the step's own sections go through it). */
export interface StepDialogProps {
  submission: BlankSubmission
  definition: BlankDefinition
  step: BlankStep
  onClose: () => void
  onDone: (result: BlankSubmission) => void
  uploadFile?: UploadFile
  removeFile?: RemoveFile
  openFile?: (attachment: BlankAttachmentDto) => void
}

/**
 * Decision + comment + the step's own sections (`fill.at = step`), shared by the electronic
 * approval (#12) and the in-person signature (#13). Answers sent = only the step's section
 * fields — the server replaces exactly those sections (SPEC §5, #9 rule).
 */
export function useStepAct(submission: BlankSubmission, definition: BlankDefinition, step: BlankStep) {
  const stepDef = stepDefinition(definition, step.stepCode)
  const sectionIds = useMemo(
    () =>
      definition.sections
        .filter((section) => {
          const fill = sectionFill(section)
          return fill.at === 'step' && fill.step === step.stepCode
        })
        .map((section) => section.id),
    [definition, step.stepCode],
  )
  const fieldIds = useMemo(
    () =>
      new Set(definition.sections.filter((s) => sectionIds.includes(s.id)).flatMap((s) => s.fields.map((f) => f.id))),
    [definition, sectionIds],
  )

  const [answers, setAnswers] = useState<Answers>(submission.answers)
  const [decision, setDecision] = useState('')
  const [extra, setExtra] = useState('')
  const [comment, setComment] = useState('')
  const [checked, setChecked] = useState(false)
  const [serverError, setServerError] = useState<BlankServerError | null>(null)
  const [failure, setFailure] = useState<string | null>(null)

  const options = stepDef?.decision?.options ?? []
  const chosen = options.find((option) => option.value === decision)
  const decisionMissing = stepDef?.decision?.required === true && decision === ''
  const extraMissing = chosen?.extra?.required === true && extra.trim() === ''

  const fieldErrors = useMemo(
    () =>
      checked && sectionIds.length > 0
        ? validateAnswers(definition, answers, { requireFilled: true, sections: sectionIds })
        : EMPTY_ERRORS,
    [answers, checked, definition, sectionIds],
  )

  /** `null` — something is missing (errors are now shown). */
  function build(): BlankStepApproveInput | null {
    setChecked(true)
    setFailure(null)
    const errors = sectionIds.length > 0 ? validateAnswers(definition, answers, { requireFilled: true, sections: sectionIds }) : {}
    if (Object.keys(errors).length > 0 || decisionMissing || extraMissing) return null
    let decisionExtra: JsonValue | null = null
    if (chosen?.extra && extra.trim() !== '') {
      decisionExtra = chosen.extra.type === 'int' ? Number(extra) : extra.trim()
    }
    const own: Answers = {}
    for (const [key, value] of Object.entries(answers)) if (fieldIds.has(key)) own[key] = value
    return {
      expectedVersion: submission.version,
      decision: decision === '' ? null : decision,
      decisionExtra,
      comment: comment.trim() === '' ? null : comment.trim(),
      answers: sectionIds.length > 0 ? own : null,
    }
  }

  function fail(error: unknown) {
    const server = blankServerError(error)
    setServerError(server.detail !== null ? server : null)
    setFailure(blankErrorCode(error))
  }

  return {
    stepDef,
    sectionIds,
    answers,
    setAnswers,
    decision,
    setDecision: (value: string) => {
      setDecision(value)
      setExtra('')
    },
    extra,
    setExtra,
    comment,
    setComment,
    checked,
    decisionMissing,
    extraMissing,
    fieldErrors,
    serverError,
    failure,
    build,
    fail,
  }
}

export type StepAct = ReturnType<typeof useStepAct>

/** The form body of a step act — decision, its extra, the step's sections, comment. */
export function StepActFields({
  act,
  submission,
  definition,
  disabled,
  uploadFile,
  removeFile,
  openFile,
}: {
  act: StepAct
  submission: BlankSubmission
  definition: BlankDefinition
  disabled: boolean
  uploadFile?: UploadFile | undefined
  removeFile?: RemoveFile | undefined
  openFile?: ((attachment: BlankAttachmentDto) => void) | undefined
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const options = act.stepDef?.decision?.options ?? []
  const chosen = options.find((option) => option.value === act.decision)
  const fieldErrorCount = Object.keys(act.fieldErrors).length

  return (
    <div className="space-y-4">
      {act.failure !== null && <Alert tone="danger">{errorMessage(act.failure)}</Alert>}
      {fieldErrorCount > 0 && <Alert tone="danger">{t('blanks.fill.fixErrors', { n: fieldErrorCount })}</Alert>}

      {options.length > 0 && (
        <Select
          label={t('blanks.steps.decision')}
          placeholder={t('blanks.steps.chooseDecision')}
          options={options.map((option) => ({ value: option.value, label: option.label }))}
          value={act.decision}
          disabled={disabled}
          error={act.checked && act.decisionMissing ? t('blanks.steps.decisionRequired') : undefined}
          onChange={(event) => act.setDecision(event.target.value)}
        />
      )}
      {chosen?.extra !== undefined &&
        (chosen.extra.type === 'date' ? (
          <DateField
            label={chosen.extra.label}
            value={act.extra}
            disabled={disabled}
            error={act.checked && act.extraMissing ? t('blanks.steps.extraRequired') : undefined}
            onChange={(value) => act.setExtra(value)}
          />
        ) : chosen.extra.type === 'longtext' ? (
          <TextArea
            label={chosen.extra.label}
            value={act.extra}
            rows={3}
            maxLength={2000}
            disabled={disabled}
            error={act.checked && act.extraMissing ? t('blanks.steps.extraRequired') : undefined}
            onChange={(event) => act.setExtra(event.target.value)}
          />
        ) : (
          <TextField
            label={chosen.extra.label}
            value={act.extra}
            inputMode={chosen.extra.type === 'int' ? 'numeric' : undefined}
            maxLength={500}
            disabled={disabled}
            error={act.checked && act.extraMissing ? t('blanks.steps.extraRequired') : undefined}
            onChange={(event) =>
              act.setExtra(chosen.extra?.type === 'int' ? event.target.value.replace(/\D/g, '') : event.target.value)
            }
          />
        ))}

      {act.sectionIds.length > 0 && (
        <BlankForm
          definition={definition}
          answers={act.answers}
          onChange={act.setAnswers}
          editableSections={disabled ? [] : act.sectionIds}
          show="editable"
          computed={submission.computed}
          errors={act.fieldErrors}
          serverError={act.serverError}
          attachments={submission.attachments}
          searchRef={searchBlankRef}
          searchDocRef={searchBlankDocRef}
          uploadFile={disabled ? undefined : uploadFile}
          removeFile={disabled ? undefined : removeFile}
          openFile={openFile}
        />
      )}

      <TextArea
        label={t('blanks.steps.commentOptional')}
        value={act.comment}
        rows={3}
        maxLength={2000}
        disabled={disabled}
        onChange={(event) => act.setComment(event.target.value)}
      />
    </div>
  )
}

/** Electronic approval of one Open step — POST #12. */
export function StepApproveDialog({ submission, definition, step, onClose, onDone, uploadFile, removeFile, openFile }: StepDialogProps) {
  const { t } = useLocale()
  const approve = useApproveBlankStep()
  const act = useStepAct(submission, definition, step)

  const run = () => {
    const body = act.build()
    if (body === null) return
    approve.mutate({ id: submission.id, stepCode: step.stepCode, body }, { onSuccess: onDone, onError: act.fail })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!approve.isPending}
      size={act.sectionIds.length > 0 ? 'lg' : 'md'}
      title={t('blanks.steps.approveTitle', { step: step.title })}
      description={t('blanks.steps.approveText')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={approve.isPending}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" onClick={run} disabled={approve.isPending}>
            {approve.isPending ? <LoaderCircle className="animate-spin" /> : <CheckCircle2 />}
            {t('blanks.steps.approve')}
          </Button>
        </>
      }
    >
      <StepActFields
        act={act}
        submission={submission}
        definition={definition}
        disabled={approve.isPending}
        uploadFile={uploadFile}
        removeFile={removeFile}
        openFile={openFile}
      />
    </Dialog>
  )
}
