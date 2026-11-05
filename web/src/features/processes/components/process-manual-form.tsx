import { useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { CircleAlert, CircleCheck, CircleMinus, LoaderCircle } from 'lucide-react'
import { useState, type ReactNode } from 'react'

import { useAuth } from '@/features/auth'
import { DocumentDialog } from '@/features/employees/components/document-dialog'
import {
  EMPLOYEES_EDIT,
  EMPLOYEES_VIEW,
  RECRUITING_VIEW,
} from '@/features/employees/lib/employee-presentation'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import {
  Alert,
  Button,
  buttonVariants,
  Checkbox,
  DateField,
  Dialog,
  Select,
  TextArea,
  TextField,
} from '@/shared/ui'

import type {
  ProcessAutoFill,
  ProcessAutoItem,
  ProcessChecklistMark,
  ProcessManualInput,
  ProcessStepInputs,
} from '../api/processes-api'
import { processKeys } from '../api/processes-queries'
import {
  checklistRows,
  initialManualValues,
  isManualInputRequired,
  manualMarks,
  manualText,
  missingRequiredItems,
  PROCESS_MISSING_REASON_MAX,
  toManualInputs,
  validateManualValues,
  type ChecklistRow,
  type ManualChecklistValue,
  type ManualFieldError,
  type ManualFormValue,
  type ManualFormValues,
} from '../lib/process-manual-form'

/**
 * A step's manual inputs and its «Bajarildi» button (SPEC §11.5, §10 #10). The rules live in `lib/process-manual-form.ts`.
 * Mistakes appear only after the first press, so an untouched form is not all red. `locked` keeps the whole form
 * disabled while the write endpoint is missing; `children` are the buttons beside «Bajarildi».
 *
 * Data-backed checklist items are not answered here: what is missing is filled in with «To'ldirish», and «Bajarildi» stays
 * shut until it is. `canCloseWithReason` (`processes.manage`) may close anyway, with a reason the server audits.
 */
export function ProcessManualForm({
  inputs,
  locked = false,
  pending = false,
  finished = false,
  canCloseWithReason = false,
  failure = null,
  caseId,
  onSubmit,
  children,
}: {
  inputs: readonly ProcessManualInput[]
  locked?: boolean
  pending?: boolean
  /** The write went through, whatever became of the reload after it: nothing is left for the reason dialog to wait on. */
  finished?: boolean
  canCloseWithReason?: boolean
  /** The last refusal, repeated inside the reason dialog because that covers the panel's own alert. */
  failure?: string | null
  /** The case a «To'ldirish» pointing at another step stays in. */
  caseId: string
  onSubmit: (inputs: ProcessStepInputs, missingReason?: string) => void
  children?: ReactNode
}) {
  const { t } = useLocale()
  const queryClient = useQueryClient()
  const [values, setValues] = useState<ManualFormValues>(() => initialManualValues(inputs))
  const [submitted, setSubmitted] = useState(false)
  const [reasonOpen, setReasonOpen] = useState(false)
  const [uploading, setUploading] = useState<ProcessAutoFill | null>(null)

  const errors = validateManualValues(inputs, values)
  const missing = missingRequiredItems(inputs, values)
  const blocked = missing.length > 0
  const valid = Object.keys(errors).length === 0

  const setValue = (id: string, value: ManualFormValue) => {
    setValues((previous) => ({ ...previous, [id]: value }))
  }

  return (
    <>
      <form
        noValidate
        onSubmit={(event) => {
          event.preventDefault()
          setSubmitted(true)
          if (valid && !blocked) onSubmit(toManualInputs(inputs, values))
        }}
      >
        <fieldset disabled={locked || pending} className="min-w-0 space-y-4">
          {inputs.map((input) => (
            <ManualField
              key={input.id}
              input={input}
              required={isManualInputRequired(input, values)}
              value={values[input.id]}
              error={submitted ? errors[input.id] : undefined}
              onChange={(value) => setValue(input.id, value)}
              caseId={caseId}
              onUpload={setUploading}
            />
          ))}

          {blocked && (
            <p className="text-footnote text-[var(--fg-secondary)]">
              {t('processes.case.form.missingHint')}
            </p>
          )}

          <div className="flex flex-wrap items-center gap-2">
            <Button type="submit" variant="primary" disabled={blocked}>
              {pending && <LoaderCircle className="animate-spin" />}
              {t('processes.case.complete')}
            </Button>
            {blocked && canCloseWithReason && (
              <Button
                type="button"
                variant="secondary"
                onClick={() => {
                  setSubmitted(true)
                  if (valid) setReasonOpen(true)
                }}
              >
                {t('processes.case.form.closeWithReason')}
              </Button>
            )}
            {children}
          </div>
        </fieldset>
      </form>

      {reasonOpen && !finished && (
        <MissingReasonDialog
          missing={missing}
          pending={pending}
          failure={failure}
          onClose={() => setReasonOpen(false)}
          onConfirm={(reason) => onSubmit(toManualInputs(inputs, values), reason)}
        />
      )}

      {uploading?.employeeId != null && (
        <DocumentDialog
          employeeId={uploading.employeeId}
          mode="create"
          item={null}
          initialTypeId={uploading.documentTypeId ?? ''}
          onClose={() => {
            setUploading(null)
            void queryClient.invalidateQueries({ queryKey: processKeys.all })
          }}
        />
      )}
    </>
  )
}

/** Closing past missing data is an exception on the record, so it names what is missing and cannot go without a reason. */
function MissingReasonDialog({
  missing,
  pending,
  failure,
  onClose,
  onConfirm,
}: {
  missing: readonly ChecklistRow[]
  pending: boolean
  failure: string | null
  onClose: () => void
  onConfirm: (reason: string) => void
}) {
  const { t } = useLocale()
  const [reason, setReason] = useState('')
  const trimmed = reason.trim()

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!pending}
      title={t('processes.case.form.reasonTitle')}
      description={t('processes.case.form.reasonText')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={pending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="primary"
            disabled={pending || trimmed === ''}
            onClick={() => onConfirm(trimmed)}
          >
            {pending && <LoaderCircle className="animate-spin" />}
            {t('processes.case.form.closeWithReason')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {failure !== null && <Alert tone="danger">{failure}</Alert>}
        <ul className="space-y-1 text-footnote">
          {missing.map((row) => (
            <li key={row.option.value} className="flex gap-2">
              <CircleAlert aria-hidden className="mt-0.5 size-3.5 shrink-0 text-ios-orange" />
              {row.option.label}
            </li>
          ))}
        </ul>
        <TextArea
          label={`${t('processes.case.form.reasonLabel')} *`}
          maxLength={PROCESS_MISSING_REASON_MAX}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
        />
      </div>
    </Dialog>
  )
}

function useFieldErrorText(input: ProcessManualInput, error: ManualFieldError | undefined) {
  const { t, num } = useLocale()
  if (error === undefined) return undefined

  return t(`processes.case.form.errors.${error}`, {
    min: num(input.min ?? 0),
    max: num(input.max ?? 0),
  })
}

function ManualField({
  input,
  required,
  value,
  error,
  onChange,
  caseId,
  onUpload,
}: {
  input: ProcessManualInput
  required: boolean
  value: ManualFormValue | undefined
  error: ManualFieldError | undefined
  onChange: (value: ManualFormValue) => void
  caseId: string
  onUpload: (fill: ProcessAutoFill) => void
}) {
  const { t } = useLocale()
  const errorText = useFieldErrorText(input, error)
  const label = required ? `${input.label} *` : input.label

  switch (input.type) {
    case 'Text':
      return (
        <TextField
          label={label}
          value={manualText(value)}
          onChange={(event) => onChange(event.target.value)}
          error={errorText}
        />
      )
    case 'LongText':
      return (
        <TextArea
          label={label}
          value={manualText(value)}
          onChange={(event) => onChange(event.target.value)}
          error={errorText}
        />
      )
    case 'Date':
      return (
        <DateField label={label} value={manualText(value)} onChange={onChange} error={errorText} />
      )
    case 'WholeNumber':
      return (
        <TextField
          label={label}
          inputMode="numeric"
          value={manualText(value)}
          onChange={(event) => onChange(event.target.value)}
          error={errorText}
        />
      )
    case 'Choice':
      return (
        <Select
          label={label}
          placeholder={t('processes.case.form.choose')}
          options={input.options}
          value={manualText(value)}
          onChange={(event) => onChange(event.target.value)}
          error={errorText}
        />
      )
    case 'Checklist':
      return (
        <ManualChecklist
          label={label}
          input={input}
          marks={manualMarks(value)}
          error={errorText}
          onChange={onChange}
          caseId={caseId}
          onUpload={onUpload}
        />
      )
  }
}

/**
 * Each plain item is ticked or marked «yo'q / kerak emas» (SPEC §3.1), so neither mark is the state «not looked at yet».
 * A data-backed item takes no mark: it is filled in where it lives, or it already reads as settled below the rest.
 */
function ManualChecklist({
  label,
  input,
  marks,
  error,
  onChange,
  caseId,
  onUpload,
}: {
  label: string
  input: ProcessManualInput
  marks: ManualChecklistValue
  error: string | undefined
  onChange: (value: ManualChecklistValue) => void
  caseId: string
  onUpload: (fill: ProcessAutoFill) => void
}) {
  const { t } = useLocale()
  const rows = checklistRows(input)

  const setMark = (option: string, mark: ProcessChecklistMark | undefined) => {
    const { [option]: _previous, ...others } = marks
    onChange(mark === undefined ? others : { ...others, [option]: mark })
  }

  return (
    <div className="space-y-2">
      <p className="text-footnote font-medium text-[var(--fg-secondary)]">{label}</p>
      <ul className="divide-y divide-[var(--border)] rounded-md border border-[var(--border)]">
        {rows.missing.map(({ option, auto }) => (
          <li
            key={option.value}
            className="flex flex-wrap items-center justify-between gap-2 px-3 py-2"
          >
            <span className="flex min-w-0 items-start gap-2 text-subhead">
              <CircleAlert aria-hidden className="mt-0.5 size-4 shrink-0 text-ios-orange" />
              <span className="min-w-0">
                {option.label}
                <span className="block text-footnote text-[var(--fg-secondary)]">
                  {t('processes.case.form.missing')}
                </span>
              </span>
            </span>
            {auto?.fill != null && (
              <FillAction fill={auto.fill} caseId={caseId} onUpload={onUpload} />
            )}
          </li>
        ))}

        {rows.plain.map(({ option }) => {
          const mark = marks[option.value]
          return (
            <li
              key={option.value}
              className="flex flex-wrap items-center justify-between gap-2 px-3 py-2"
            >
              <Checkbox
                checked={mark === 'done'}
                onChange={(checked) => setMark(option.value, checked ? 'done' : undefined)}
                label={option.label}
              />
              <Button
                type="button"
                size="sm"
                variant={mark === 'na' ? 'soft' : 'ghost'}
                aria-pressed={mark === 'na'}
                aria-label={`${option.label}: ${t('processes.case.marks.na')}`}
                onClick={() =>
                  setMark(option.value, mark === 'na' ? undefined : 'na')
                }
              >
                {t('processes.case.marks.na')}
              </Button>
            </li>
          )
        })}

        {rows.known.map(({ option, auto }) => (
          <SettledRow
            key={option.value}
            icon={<CircleCheck aria-hidden className="mt-0.5 size-4 shrink-0 text-ios-green" />}
            label={option.label}
            note={auto === null ? null : <KnownSource auto={auto} />}
          />
        ))}

        {rows.notApplicable.map(({ option, auto }) => (
          <SettledRow
            key={option.value}
            icon={<CircleMinus aria-hidden className="mt-0.5 size-4 shrink-0 text-ios-gray" />}
            label={option.label}
            note={
              auto?.reason === 'Female'
                ? t('processes.case.form.notNeededFemale')
                : t('processes.case.form.notNeeded')
            }
          />
        ))}
      </ul>
      {error !== undefined && (
        <p role="alert" className={cn('text-footnote text-ios-red')}>
          {error}
        </p>
      )}
    </div>
  )
}

/** Small on purpose: nobody has to do anything about these rows, they only show the system looked. */
function SettledRow({
  icon,
  label,
  note,
}: {
  icon: ReactNode
  label: string
  note: ReactNode
}) {
  return (
    <li className="flex items-start gap-2 px-3 py-1.5 text-footnote">
      {icon}
      <span className="min-w-0">
        {label}
        {note !== null && <span className="text-[var(--fg-secondary)]"> · {note}</span>}
      </span>
    </li>
  )
}

function KnownSource({ auto }: { auto: ProcessAutoItem }) {
  const { t } = useLocale()

  if (auto.autoSource === null) return t('processes.case.form.known')

  const source = t(`processes.case.form.source.${auto.autoSource}`)
  return auto.sourceDetail === null ? source : `${source}: ${auto.sourceDetail}`
}

/**
 * «To'ldirish». A document uploads right here for someone who may edit the employee; otherwise it leads to the place the
 * data lives, and to nothing at all for someone who could not open that place.
 */
function FillAction({
  fill,
  caseId,
  onUpload,
}: {
  fill: ProcessAutoFill
  caseId: string
  onUpload: (fill: ProcessAutoFill) => void
}) {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const { employeeId, candidateId, stepCode } = fill
  const className = buttonVariants({ variant: 'secondary', size: 'sm' })
  const label = t('processes.case.form.fill')

  switch (fill.kind) {
    case 'EmployeeDocument':
      if (employeeId === null) return null
      if (hasPermission(EMPLOYEES_EDIT)) {
        return (
          <Button type="button" size="sm" variant="secondary" onClick={() => onUpload(fill)}>
            {label}
          </Button>
        )
      }
      return hasPermission(EMPLOYEES_VIEW) ? (
        <Link
          to="/xodimlar/$employeeId"
          params={{ employeeId }}
          search={{ tab: 'documents' }}
          className={className}
        >
          {label}
        </Link>
      ) : null
    case 'EmployeeCard':
      return employeeId !== null && hasPermission(EMPLOYEES_VIEW) ? (
        <Link to="/xodimlar/$employeeId" params={{ employeeId }} className={className}>
          {label}
        </Link>
      ) : null
    case 'CandidateCard':
      return candidateId !== null && hasPermission(RECRUITING_VIEW) ? (
        <Link to="/nomzodlar/$candidateId" params={{ candidateId }} className={className}>
          {label}
        </Link>
      ) : null
    case 'CaseStep':
      return stepCode === null ? null : (
        <Link
          to="/mening-ishlarim/$caseId"
          params={{ caseId }}
          search={{ step: stepCode }}
          className={className}
        >
          {label}
        </Link>
      )
  }
}
