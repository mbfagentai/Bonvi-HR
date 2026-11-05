import type {
  ProcessChecklistMark,
  ProcessManualInput,
  ProcessStepInputs,
  ProcessStepInputValue,
  ProcessStepPayload,
} from '../api/processes-api'

/*
 * The manual-input form's rules (SPEC §3.1 `ManualInput`, §10 #10), kept free of React so they can be tested. The server
 * validates the same inputs again; these rules exist so the user hears about a mistake before the round trip.
 */

/** What a field holds while it is edited: every scalar is a string, a checklist maps option value to its mark. */
export type ManualChecklistValue = Readonly<Record<string, ProcessChecklistMark>>
export type ManualFormValue = string | ManualChecklistValue
export type ManualFormValues = Readonly<Record<string, ManualFormValue>>

export type ManualFieldError =
  | 'required'
  | 'checklistIncomplete'
  | 'notWholeNumber'
  | 'belowMin'
  | 'aboveMax'
  | 'invalidChoice'

export type ManualFormErrors = Readonly<Record<string, ManualFieldError>>

const WHOLE_NUMBER = /^-?\d+$/u

const NO_MARKS: ManualChecklistValue = {}

export function initialManualValues(inputs: readonly ProcessManualInput[]): ManualFormValues {
  return Object.fromEntries(
    inputs.map((input) => [input.id, input.type === 'Checklist' ? NO_MARKS : '']),
  )
}

export function manualText(value: ManualFormValue | undefined): string {
  return typeof value === 'string' ? value : ''
}

export function manualMarks(value: ManualFormValue | undefined): ManualChecklistValue {
  return value === undefined || typeof value === 'string' ? NO_MARKS : value
}

/**
 * Whether the field must be filled right now. A condition on another input is answered here; a condition on a server
 * fact (`requiredWhen.fact`) cannot be, so the server answers 400 `jarayon.kiritish_notogri` for it.
 */
export function isManualInputRequired(
  input: ProcessManualInput,
  values: ManualFormValues,
): boolean {
  if (input.required) return true

  const when = input.requiredWhen
  return (
    when !== null && when.input !== null && manualText(values[when.input]).trim() === when.equalsValue
  )
}

function fieldError(
  input: ProcessManualInput,
  values: ManualFormValues,
): ManualFieldError | undefined {
  const required = isManualInputRequired(input, values)

  if (input.type === 'Checklist') {
    const marks = manualMarks(values[input.id])
    return required && input.options.some((option) => marks[option.value] === undefined)
      ? 'checklistIncomplete'
      : undefined
  }

  const text = manualText(values[input.id]).trim()
  if (text === '') return required ? 'required' : undefined

  if (input.type === 'Choice') {
    return input.options.some((option) => option.value === text) ? undefined : 'invalidChoice'
  }

  if (input.type === 'WholeNumber') {
    if (!WHOLE_NUMBER.test(text)) return 'notWholeNumber'
    const number = Number(text)
    if (input.min !== null && number < input.min) return 'belowMin'
    if (input.max !== null && number > input.max) return 'aboveMax'
  }

  return undefined
}

/** Only the fields in error appear, so an empty object means the form may go. */
export function validateManualValues(
  inputs: readonly ProcessManualInput[],
  values: ManualFormValues,
): ManualFormErrors {
  return Object.fromEntries(
    inputs.flatMap((input) => {
      const error = fieldError(input, values)
      return error === undefined ? [] : [[input.id, error] as const]
    }),
  )
}

/** The `done` body: empty fields are left out, so the server sees «not answered» rather than an empty string. */
export function toManualInputs(
  inputs: readonly ProcessManualInput[],
  values: ManualFormValues,
): ProcessStepInputs {
  return Object.fromEntries(
    inputs.flatMap<readonly [string, ProcessStepInputValue]>((input) => {
      if (input.type === 'Checklist') {
        const marks = manualMarks(values[input.id])
        return Object.keys(marks).length === 0 ? [] : [[input.id, marks]]
      }

      const text = manualText(values[input.id]).trim()
      if (text === '') return []
      return [[input.id, input.type === 'WholeNumber' ? Number(text) : text]]
    }),
  )
}

export type RecordedManualInput =
  | { kind: 'text'; id: string; label: string; text: string }
  | { kind: 'date'; id: string; label: string; date: string }
  | {
      kind: 'checklist'
      id: string
      label: string
      items: ReadonlyArray<{ value: string; label: string; mark: ProcessChecklistMark }>
    }

function isChecklistMark(value: unknown): value is ProcessChecklistMark {
  return value === 'done' || value === 'na'
}

/**
 * The answers of a finished step, in the definition's order and words. A payload a fact wrote (`value`, `daysToClose`)
 * has no matching input, so it draws nothing here.
 */
export function describeRecordedInputs(
  inputs: readonly ProcessManualInput[],
  payload: ProcessStepPayload | null,
): RecordedManualInput[] {
  if (payload === null) return []

  return inputs.flatMap<RecordedManualInput>((input) => {
    const { id, label } = input
    const value = payload[id]

    if (input.type === 'Checklist') {
      if (typeof value !== 'object' || value === null) return []
      const marks = value as Readonly<Record<string, unknown>>
      const items = input.options.flatMap((option) => {
        const mark = marks[option.value]
        return isChecklistMark(mark) ? [{ value: option.value, label: option.label, mark }] : []
      })
      return items.length === 0 ? [] : [{ kind: 'checklist', id, label, items }]
    }

    if (typeof value !== 'string' && typeof value !== 'number') return []
    const text = String(value)
    if (text === '') return []

    if (input.type === 'Date') return [{ kind: 'date', id, label, date: text }]
    if (input.type === 'Choice') {
      const option = input.options.find((candidate) => candidate.value === text)
      return [{ kind: 'text', id, label, text: option?.label ?? text }]
    }
    return [{ kind: 'text', id, label, text }]
  })
}
