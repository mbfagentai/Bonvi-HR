import { z } from 'zod'

import { todayIso } from '@/shared/lib/today'

import { computeVisibility, isEmptyValue } from './conditions'
import type {
  Answers,
  BlankDefinition,
  ColumnField,
  DateBound,
  Field,
  FieldOption,
  JsonValue,
  Section,
  TableField,
} from './definition-types'

/**
 * Client-side answers validation built from a definition — the mirror of the
 * server's `BlankAnswersValidator` for instant inline feedback. The server stays
 * authoritative; its `detail` path errors land in the same {@link FieldErrors} map.
 *
 * Two modes (§5):
 * - save   (`requireFilled: false`) — type / limit checks only (autosave);
 * - submit (`requireFilled: true`)  — plus `required`, `minRows`, option extras.
 * Hidden fields (`when` false) are skipped entirely (§4.4).
 *
 * Issue messages are i18n key suffixes under `blanks.form.errors.*`; numeric
 * parameters (`min`, `max`) are taken from the field definition.
 */

export interface FieldError {
  /** Suffix under `blanks.form.errors.*`, or an absolute server message. */
  key: string
  params?: Record<string, string | number>
  /** Already translated text (server errors) — wins over `key`. */
  message?: string
}

/** Keyed by field path: `fieldId`, `fieldId.extra`, `table[i].col`, `table[i]`. */
export type FieldErrors = Record<string, FieldError>

export interface ValidateOptions {
  /** Only fields of these section ids are validated. Default: every section. */
  sections?: readonly string[]
  requireFilled: boolean
  /** ISO date used for `today` bounds — defaults to today in Tashkent. */
  today?: string
}

// ── Character rules ──────────────────────────────────────────────────────────

/** Control, bidi-override and zero-width characters (F02 rule). */
// eslint-disable-next-line no-control-regex
const FORBIDDEN_TEXT = /[\u0000-\u001F\u007F-\u009F​-‏‪-‮⁠-⁤⁦-⁩﻿]/
// eslint-disable-next-line no-control-regex
const FORBIDDEN_LONGTEXT = /[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F​-‏‪-‮⁠-⁤⁦-⁩﻿]/

const ISO_DATE = /^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$/
const ISO_MONTH = /^\d{4}-(0[1-9]|1[0-2])$/
const DATETIME_OFFSET = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})$/

export const TEXT_DEFAULT_MAX = 200
export const TEXT_HARD_MAX = 500
export const LONGTEXT_DEFAULT_MAX = 2000
export const LONGTEXT_HARD_MAX = 4000
export const MONEY_MAX = 1e12
export const TABLE_HARD_MAX_ROWS = 200
export const FILE_DEFAULT_MAX = 5

/** `today`, `today+N`, `today-N` or an ISO date → ISO date; anything else → undefined. */
export function resolveDateBound(bound: DateBound | undefined, today: string): string | undefined {
  if (!bound) return undefined
  const match = /^today(?:([+-])(\d{1,4}))?$/.exec(bound)
  if (!match) return ISO_DATE.test(bound) ? bound : undefined
  const [, sign, days] = match
  if (!sign || !days) return today
  const base = new Date(`${today}T00:00:00Z`)
  base.setUTCDate(base.getUTCDate() + (sign === '-' ? -1 : 1) * Number(days))
  return base.toISOString().slice(0, 10)
}

export function textMaxLength(field: Field): number {
  if (field.type === 'text') return Math.min(field.maxLength ?? TEXT_DEFAULT_MAX, TEXT_HARD_MAX)
  if (field.type === 'longtext')
    return Math.min(field.maxLength ?? LONGTEXT_DEFAULT_MAX, LONGTEXT_HARD_MAX)
  return TEXT_DEFAULT_MAX
}

// ── Per-field value schemas (non-empty values only) ─────────────────────────

const msg = (key: string) => ({ error: key })

const isoDate = (min?: string, max?: string) =>
  z
    .string(msg('invalid'))
    .regex(ISO_DATE, msg('invalidDate'))
    .refine((v) => !min || v >= min, msg('dateMin'))
    .refine((v) => !max || v <= max, msg('dateMax'))

const optionValue = (options: FieldOption[]) =>
  z.string(msg('invalidOption')).refine((v) => options.some((o) => o.value === v), msg('invalidOption'))

const choiceItem = (options: FieldOption[]) =>
  z.object({ value: optionValue(options), extra: z.json().optional() }, msg('invalid'))

function valueSchema(field: Field, today: string): z.ZodType | null {
  switch (field.type) {
    case 'text':
    case 'longtext': {
      const forbidden = field.type === 'text' ? FORBIDDEN_TEXT : FORBIDDEN_LONGTEXT
      return z
        .string(msg('invalid'))
        .refine((v) => v.trim().length <= textMaxLength(field), msg('tooLong'))
        .refine((v) => !forbidden.test(v), msg('invalidChars'))
    }
    case 'int':
      return z
        .number(msg('invalidNumber'))
        .int(msg('invalidNumber'))
        .refine((v) => field.min === undefined || v >= field.min, msg('numberMin'))
        .refine((v) => field.max === undefined || v <= field.max, msg('numberMax'))
    case 'percent':
      return z
        .number(msg('invalidNumber'))
        .min(0, msg('percentRange'))
        .max(100, msg('percentRange'))
        .refine((v) => Math.abs(v * 10 - Math.round(v * 10)) < 1e-9, msg('percentDecimals'))
    case 'money':
      return z
        .number(msg('invalidNumber'))
        .int(msg('invalidNumber'))
        .refine((v) => v >= (field.min ?? 0), msg('numberMin'))
        .refine((v) => v <= Math.min(field.max ?? MONEY_MAX, MONEY_MAX), msg('numberMax'))
    case 'date':
      return isoDate(resolveDateBound(field.min, today), resolveDateBound(field.max, today))
    case 'datetime': {
      const min = resolveDateBound(field.min, today)
      const max = resolveDateBound(field.max, today)
      return z
        .string(msg('invalid'))
        .regex(DATETIME_OFFSET, msg('invalidDate'))
        .refine((v) => !Number.isNaN(Date.parse(v)), msg('invalidDate'))
        .refine((v) => !min || v.slice(0, 10) >= min, msg('dateMin'))
        .refine((v) => !max || v.slice(0, 10) <= max, msg('dateMax'))
    }
    case 'month':
      return z
        .string(msg('invalid'))
        .regex(ISO_MONTH, msg('invalidDate'))
        .refine((v) => !field.min || v >= field.min, msg('dateMin'))
        .refine((v) => !field.max || v <= field.max, msg('dateMax'))
    case 'year':
      return z
        .number(msg('invalidNumber'))
        .int(msg('invalidNumber'))
        .refine((v) => v >= (field.min ?? 1900), msg('numberMin'))
        .refine((v) => v <= (field.max ?? 2100), msg('numberMax'))
    case 'dateRange':
      return z
        .object({ from: isoDate(), to: isoDate() }, msg('invalid'))
        .refine((v) => v.from <= v.to, msg('rangeOrder'))
    case 'yesno':
    case 'check':
      return z.boolean(msg('invalid'))
    case 'score':
      return z.number(msg('invalidNumber')).int(msg('invalidNumber')).min(1, msg('score')).max(5, msg('score'))
    case 'choice':
      return choiceItem(field.options)
    case 'multi':
      return z
        .array(choiceItem(field.options), msg('invalid'))
        .refine((v) => new Set(v.map((i) => i.value)).size === v.length, msg('duplicate'))
        .refine(
          (v) => field.maxSelected === undefined || v.length <= field.maxSelected,
          msg('tooManySelected'),
        )
    case 'ref':
      return z.object({ id: z.string().min(1, msg('invalid')), label: z.string() }, msg('invalid'))
    case 'docRef':
      return z.object(
        {
          submissionId: z.string().min(1, msg('invalid')),
          templateCode: z
            .string()
            .refine((v) => field.templateCodes.includes(v), msg('invalidOption')),
          number: z.string(),
        },
        msg('invalid'),
      )
    case 'order':
      return z.object(
        {
          number: z.string(msg('invalid')).trim().min(1, msg('required')).max(50, msg('tooLong')),
          date: isoDate(),
        },
        msg('invalid'),
      )
    case 'file':
      return z
        .array(z.string(), msg('invalid'))
        .max(field.maxFiles ?? FILE_DEFAULT_MAX, msg('tooManyFiles'))
    case 'table':
    case 'static':
    case 'computed':
      return null
  }
}

/** Numeric params for a message, taken from the field definition. */
function paramsFor(field: Field, today: string): Record<string, string | number> {
  const params: Record<string, string | number> = {}
  switch (field.type) {
    case 'text':
    case 'longtext':
      params['max'] = textMaxLength(field)
      break
    case 'int':
    case 'money':
    case 'year':
      if (field.min !== undefined) params['min'] = field.min
      if (field.max !== undefined) params['max'] = field.max
      break
    case 'date':
    case 'datetime': {
      const min = resolveDateBound(field.min, today)
      const max = resolveDateBound(field.max, today)
      if (min) params['min'] = min
      if (max) params['max'] = max
      break
    }
    case 'multi':
      if (field.maxSelected !== undefined) params['max'] = field.maxSelected
      break
    case 'file':
      params['max'] = field.maxFiles ?? FILE_DEFAULT_MAX
      break
    case 'table':
      params['min'] = field.minRows ?? 0
      params['max'] = Math.min(field.maxRows ?? TABLE_HARD_MAX_ROWS, TABLE_HARD_MAX_ROWS)
      break
    default:
      break
  }
  return params
}

// ── Paths ────────────────────────────────────────────────────────────────────

export function pathToString(path: readonly PropertyKey[]): string {
  return path.reduce<string>((acc, part) => {
    if (typeof part === 'number') return `${acc}[${part}]`
    const name = String(part)
    return acc === '' ? name : `${acc}.${name}`
  }, '')
}

// ── Walk ─────────────────────────────────────────────────────────────────────

interface Sink {
  add: (path: (string | number)[], key: string, field: Field) => void
}

function validateExtras(
  options: FieldOption[],
  items: { value: string; extra?: JsonValue }[],
  basePath: (string | number)[],
  field: Field,
  sink: Sink,
): void {
  items.forEach((item, i) => {
    const option = options.find((o) => o.value === item.value)
    if (option?.extra?.required && isEmptyValue(item.extra)) {
      const path = field.type === 'multi' ? [...basePath, i, 'extra'] : [...basePath, 'extra']
      sink.add(path, 'extraRequired', field)
    }
  })
}

function validateValue(
  field: Field | ColumnField,
  value: JsonValue | undefined,
  path: (string | number)[],
  options: Required<Omit<ValidateOptions, 'sections'>>,
  sink: Sink,
): void {
  if (field.type === 'static' || field.type === 'computed') return
  if (field.type === 'table') {
    validateTable(field, value, path, options, sink)
    return
  }
  if (isEmptyValue(value)) {
    if (options.requireFilled && field.required && !field.readOnly) sink.add(path, 'required', field)
    return
  }
  const schema = valueSchema(field, options.today)
  if (!schema) return
  const result = schema.safeParse(value)
  if (!result.success) {
    for (const issue of result.error.issues) {
      sink.add([...path, ...issue.path.map((p) => (typeof p === 'number' ? p : String(p)))], issue.message, field)
    }
    return
  }
  if (options.requireFilled && (field.type === 'choice' || field.type === 'multi')) {
    const items = Array.isArray(result.data) ? result.data : [result.data]
    validateExtras(field.options, items as { value: string; extra?: JsonValue }[], path, field, sink)
  }
}

function validateTable(
  field: TableField,
  value: JsonValue | undefined,
  path: (string | number)[],
  options: Required<Omit<ValidateOptions, 'sections'>>,
  sink: Sink,
): void {
  if (value !== undefined && value !== null && !Array.isArray(value)) {
    sink.add(path, 'invalid', field)
    return
  }
  const rows = value ?? []
  const maxRows = Math.min(field.maxRows ?? TABLE_HARD_MAX_ROWS, TABLE_HARD_MAX_ROWS)
  if (field.fixedRows) {
    const ids = rows.map((r) => (r && typeof r === 'object' && !Array.isArray(r) ? r['_row'] : null))
    const expected = field.fixedRows.map((r) => r.id)
    if (rows.length > 0 && (ids.length !== expected.length || ids.some((id, i) => id !== expected[i])))
      sink.add(path, 'fixedRows', field)
  } else {
    if (rows.length > maxRows) sink.add(path, 'tooManyRows', field)
    if (options.requireFilled && rows.length < (field.minRows ?? 0)) sink.add(path, 'tooFewRows', field)
  }
  if (options.requireFilled && field.required && rows.length === 0 && !field.readOnly)
    sink.add(path, 'required', field)

  rows.forEach((row, i) => {
    if (!row || typeof row !== 'object' || Array.isArray(row)) {
      sink.add([...path, i], 'invalid', field)
      return
    }
    const fixed = field.fixedRows?.[i]
    for (const column of field.columns) {
      if (fixed?.cells?.[column.id]?.readOnly) continue
      validateValue(column, row[column.id], [...path, i, column.id], options, sink)
    }
  })
}

/** The sections a caller may validate — `fill` default is draft. */
function scopedSections(definition: BlankDefinition, ids: readonly string[] | undefined): Section[] {
  if (!ids) return definition.sections
  const wanted = new Set(ids)
  return definition.sections.filter((s) => wanted.has(s.id))
}

/**
 * Zod schema for the answers object of this definition. `safeParse` yields
 * issues whose `path` is the field path and whose `message` is the error key.
 */
export function buildAnswersSchema(definition: BlankDefinition, options: ValidateOptions) {
  const resolved = { requireFilled: options.requireFilled, today: options.today ?? todayIso() }
  return z.record(z.string(), z.json()).superRefine((answers, ctx) => {
    const visibility = computeVisibility(definition, answers)
    const sink: Sink = {
      add: (path, key, field) =>
        ctx.addIssue({ code: 'custom', path, message: key, params: paramsFor(field, resolved.today) }),
    }
    for (const section of scopedSections(definition, options.sections)) {
      if (visibility.hiddenSections.has(section.id)) continue
      for (const field of section.fields) {
        if (visibility.hiddenFields.has(field.id)) continue
        validateValue(field, answers[field.id], [field.id], resolved, sink)
      }
    }
  })
}

/** Validate and flatten into a path → error map (first error per path wins). */
export function validateAnswers(
  definition: BlankDefinition,
  answers: Answers,
  options: ValidateOptions,
): FieldErrors {
  const result = buildAnswersSchema(definition, options).safeParse(answers)
  const errors: FieldErrors = {}
  if (result.success) return errors
  for (const issue of result.error.issues) {
    const path = pathToString(issue.path)
    if (errors[path]) continue
    const params =
      issue.code === 'custom' && issue.params
        ? (issue.params as Record<string, string | number>)
        : undefined
    errors[path] = params ? { key: issue.message, params } : { key: issue.message }
  }
  return errors
}
