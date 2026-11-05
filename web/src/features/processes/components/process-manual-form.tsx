import { LoaderCircle } from 'lucide-react'
import { useState, type ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Button, Checkbox, DateField, Select, TextArea, TextField } from '@/shared/ui'

import type {
  ProcessChecklistMark,
  ProcessManualInput,
  ProcessStepInputs,
} from '../api/processes-api'
import {
  initialManualValues,
  isManualInputRequired,
  manualMarks,
  manualText,
  toManualInputs,
  validateManualValues,
  type ManualChecklistValue,
  type ManualFieldError,
  type ManualFormValue,
  type ManualFormValues,
} from '../lib/process-manual-form'

/**
 * A step's manual inputs and its «Bajarildi» button (SPEC §11.5, §10 #10). The rules live in `lib/process-manual-form.ts`.
 * Mistakes appear only after the first press, so an untouched form is not all red. `locked` keeps the whole form
 * disabled while the write endpoint is missing; `children` are the buttons beside «Bajarildi».
 */
export function ProcessManualForm({
  inputs,
  locked = false,
  pending = false,
  onSubmit,
  children,
}: {
  inputs: readonly ProcessManualInput[]
  locked?: boolean
  pending?: boolean
  onSubmit: (inputs: ProcessStepInputs) => void
  children?: ReactNode
}) {
  const { t } = useLocale()
  const [values, setValues] = useState<ManualFormValues>(() => initialManualValues(inputs))
  const [submitted, setSubmitted] = useState(false)

  const errors = validateManualValues(inputs, values)

  const setValue = (id: string, value: ManualFormValue) => {
    setValues((previous) => ({ ...previous, [id]: value }))
  }

  return (
    <form
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        setSubmitted(true)
        if (Object.keys(errors).length === 0) onSubmit(toManualInputs(inputs, values))
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
          />
        ))}

        <div className="flex flex-wrap items-center gap-2">
          <Button type="submit" variant="primary">
            {pending && <LoaderCircle className="animate-spin" />}
            {t('processes.case.complete')}
          </Button>
          {children}
        </div>
      </fieldset>
    </form>
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
}: {
  input: ProcessManualInput
  required: boolean
  value: ManualFormValue | undefined
  error: ManualFieldError | undefined
  onChange: (value: ManualFormValue) => void
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
        />
      )
  }
}

/** Each item is ticked or marked «yo'q / kerak emas» (SPEC §3.1), so neither mark is the state «not looked at yet». */
function ManualChecklist({
  label,
  input,
  marks,
  error,
  onChange,
}: {
  label: string
  input: ProcessManualInput
  marks: ManualChecklistValue
  error: string | undefined
  onChange: (value: ManualChecklistValue) => void
}) {
  const { t } = useLocale()

  const setMark = (option: string, mark: ProcessChecklistMark | undefined) => {
    const { [option]: _previous, ...others } = marks
    onChange(mark === undefined ? others : { ...others, [option]: mark })
  }

  return (
    <div className="space-y-2">
      <p className="text-footnote font-medium text-[var(--fg-secondary)]">{label}</p>
      <ul className="divide-y divide-[var(--border)] rounded-md border border-[var(--border)]">
        {input.options.map((option) => {
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
      </ul>
      {error !== undefined && (
        <p role="alert" className={cn('text-footnote text-ios-red')}>
          {error}
        </p>
      )}
    </div>
  )
}
