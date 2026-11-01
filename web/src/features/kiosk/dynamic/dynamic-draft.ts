import { newId, signatureMatches, type FieldError, type FieldErrors, type KioskErrorCode } from '../lib/kiosk-draft'
import {
  BIND,
  BOUND_AGE,
  candidateFields,
  candidateSections,
  hasAnswer,
  type FormField,
  type FormScreen,
  type FormSection,
  type KioskDefinition,
} from './definition'

/*
 * The constructor's form on the client (SPEC-anketa-konstruktori §7): the
 * draft, which fields are visible, the client mirror of the server's checks
 * (§6.1) and the `{ fieldId: value }` answers the server takes.
 *
 * The rules are the server's (`DynamicFormValidator`); this copy only says
 * early, in the candidate's language, what the server would reject. Paths are
 * the server's too (`jobs[1].period`, `languages.rows.Russian`,
 * `signature.name`), so an error the server returns lands on the same field.
 */

/* ─────────────────────────────────────────────────────────── draft */

/** A row of a repeating table; `key` only keeps React rows stable. */
export interface RepeatRow {
  key: string
  values: Record<string, unknown>
}

export interface LevelsDraft {
  /** Option value → one level per aspect (`null` — not chosen). */
  rows: Record<string, (number | null)[]>
  other: { key: string; name: string; levels: (number | null)[] }[]
}

/** Months as the candidate types them: `mm.yyyy`. */
export interface PeriodDraft {
  from: string
  to: string
  current: boolean
}

export interface RangeDraft {
  from: string
  to: string
}

export interface ReferralDraft {
  channel: string
  detail: string
}

/**
 * What the screens edit. `values` holds each field's input as typed (digits,
 * `mm.yyyy`, nine phone digits, `'general'` for the general pool); `evaluate`
 * turns it into answers. Photo and signature stay in memory only.
 */
export interface DynamicDraft {
  clientSubmissionId: string
  values: Record<string, unknown>
  /** «Same as …» ticks (`same_as`), by field id. */
  same: Record<string, boolean>
}

export function emptyDynamicDraft(): DynamicDraft {
  return { clientSubmissionId: newId(), values: {}, same: {} }
}

export { newId }

/* Readers: a value of the wrong shape (an older draft, a changed kind) reads as empty. */

export function textOf(value: unknown): string {
  return typeof value === 'string' ? value : ''
}

export function flagOf(value: unknown): boolean | null {
  return typeof value === 'boolean' ? value : null
}

export function listOf(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : []
}

function levelArray(value: unknown, size: number): (number | null)[] {
  const source = Array.isArray(value) ? value : []
  return Array.from({ length: size }, (_, at) => {
    const level: unknown = source[at]
    return typeof level === 'number' && Number.isInteger(level) ? level : null
  })
}

export function levelsOf(value: unknown, aspects: number): LevelsDraft {
  const raw = typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {}
  const rows: LevelsDraft['rows'] = {}
  const rawRows = raw['rows']
  if (typeof rawRows === 'object' && rawRows !== null && !Array.isArray(rawRows)) {
    for (const [key, levels] of Object.entries(rawRows)) rows[key] = levelArray(levels, aspects)
  }

  const other = (Array.isArray(raw['other']) ? (raw['other'] as unknown[]) : []).map((item) => {
    const row = typeof item === 'object' && item !== null ? (item as Record<string, unknown>) : {}
    return {
      key: typeof row['key'] === 'string' ? row['key'] : newId(),
      name: textOf(row['name']),
      levels: levelArray(row['levels'], aspects),
    }
  })

  return { rows, other }
}

export function rowsOf(value: unknown): RepeatRow[] {
  return (Array.isArray(value) ? (value as unknown[]) : []).map((item) => {
    const row = typeof item === 'object' && item !== null ? (item as Record<string, unknown>) : {}
    const values = row['values']
    return {
      key: typeof row['key'] === 'string' ? row['key'] : newId(),
      values: typeof values === 'object' && values !== null && !Array.isArray(values) ? (values as Record<string, unknown>) : {},
    }
  })
}

export function periodOf(value: unknown): PeriodDraft {
  const raw = typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {}
  return { from: textOf(raw['from']), to: textOf(raw['to']), current: raw['current'] === true }
}

export function rangeOf(value: unknown): RangeDraft {
  const raw = typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {}
  return { from: textOf(raw['from']), to: textOf(raw['to']) }
}

export function referralOf(value: unknown): ReferralDraft {
  const raw = typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {}
  return { channel: textOf(raw['channel']), detail: textOf(raw['detail']) }
}

/** The signature's typed name (`{ name }`). */
export function signatureNameOf(value: unknown): string {
  return typeof value === 'object' && value !== null ? textOf((value as Record<string, unknown>)['name']) : ''
}

/* ─────────────────────────────────────────────────────────── storage */

const STORAGE_PREFIX = 'bonvi.kiosk.dyn.HR-F02.'

/**
 * Answers survive an accidental reload — sessionStorage, the tab's lifetime,
 * aged out by the device's rules exactly like the built-in form's draft. The
 * key carries the version: a draft of another version is dropped, never
 * reinterpreted against a form whose fields may have changed meaning.
 */
export const dynamicDraftStorage = {
  load(version: number, maxAgeMs: number): DynamicDraft | null {
    try {
      dropOtherVersions(version)

      const key = STORAGE_PREFIX + String(version)
      const raw = window.sessionStorage.getItem(key)
      if (raw === null) return null

      const parsed = JSON.parse(raw) as Partial<DynamicDraft> & { savedAt?: unknown }
      if (typeof parsed.savedAt !== 'number' || Date.now() - parsed.savedAt > maxAgeMs) {
        window.sessionStorage.removeItem(key)
        return null
      }

      if (typeof parsed.clientSubmissionId !== 'string') return null

      const values = typeof parsed.values === 'object' && parsed.values !== null ? parsed.values : {}
      const same = Object.fromEntries(
        Object.entries(typeof parsed.same === 'object' && parsed.same !== null ? parsed.same : {}).filter(
          (entry): entry is [string, boolean] => typeof entry[1] === 'boolean',
        ),
      )

      return { clientSubmissionId: parsed.clientSubmissionId, values, same }
    } catch {
      return null
    }
  },
  save(version: number, draft: DynamicDraft): void {
    try {
      window.sessionStorage.setItem(STORAGE_PREFIX + String(version), JSON.stringify({ ...draft, savedAt: Date.now() }))
    } catch {
      // Quota or blocked storage — the wizard keeps working from memory.
    }
  },
  clear(): void {
    try {
      dropOtherVersions(null)
    } catch {
      // ignore
    }
  },
}

/** Removes every stored constructor draft except `keep`'s. */
function dropOtherVersions(keep: number | null): void {
  const stale: string[] = []
  for (let index = 0; index < window.sessionStorage.length; index++) {
    const key = window.sessionStorage.key(index)
    if (key?.startsWith(STORAGE_PREFIX) && key !== STORAGE_PREFIX + String(keep)) stale.push(key)
  }
  for (const key of stale) window.sessionStorage.removeItem(key)
}

/* ─────────────────────────────────────────────────────────── evaluation */

export interface EvalContext {
  /** Tashkent today, `yyyy-MM-dd`. */
  today: string
  vacancyIds: ReadonlySet<string>
  generalPoolAvailable: boolean
  /** Active referral channels: id → `asksDetail`. */
  referralChannels: ReadonlyMap<string, boolean>
  hasPhoto: boolean
  hasSignature: boolean
  /** `false` in the preview: the sample PDF is drawn without photo and signature too. */
  requireFiles: boolean
}

export interface Evaluation {
  /** Path → error, every error at once (the server stops at the first). */
  errors: FieldErrors
  /** `{ fieldId: value }` — what is sent; hidden and empty answers are left out (R5). */
  answers: Record<string, unknown>
  /** Ids of the top-level fields shown right now. */
  visible: ReadonlySet<string>
}

/** Server limits (`DynamicFormValidator`). */
export const LIMIT = {
  short: 200,
  long: 1000,
  number: 1_000_000_000,
  repeatRows: 20,
  otherName: 60,
  signatureName: 200,
  loosePhone: 30,
  referralDetail: 200,
  minYear: 1900,
  maxYear: 2100,
  periodFromYear: 1950,
} as const

const DATE = /^(\d{4})-(\d{2})-(\d{2})$/u
const MONTH_TYPED = /^(\d{2})\.(\d{4})$/u
const TELEGRAM = /^[A-Za-z0-9_]{5,32}$/u
const EMAIL = /^[^@\s]+@[^@\s]+\.[^@\s]+$/u
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/iu

/** Control, direction-changing, invisible, private-use and unassigned characters (server: `HasForbiddenCharacter`). */
const FORBIDDEN = /[\p{Cc}\p{Co}\p{Cn}\p{Cs}​-‍⁠‪-‮⁦-⁩﻿]/u

export function hasForbiddenCharacter(text: string, multiline: boolean): boolean {
  return FORBIDDEN.test(multiline ? text.replaceAll(/[\n\r\t]/gu, '') : text)
}

/** «1 000 000 000» — limits in messages, as on paper. */
export function grouped(value: number): string {
  return String(value).replace(/\B(?=(\d{3})+(?!\d))/gu, ' ')
}

/** `mm.yyyy` → `yyyy-MM`, or `null` when it is not a month at all. */
export function typedMonthToIso(value: string): string | null {
  const match = MONTH_TYPED.exec(value.trim())
  if (match === null) return null
  const month = Number(match[1])
  return month >= 1 && month <= 12 ? `${match[2]}-${match[1]}` : null
}

function age(birth: string, today: string): number {
  const [by, bm, bd] = birth.split('-').map(Number)
  const [ty, tm, td] = today.split('-').map(Number)
  if (!by || !bm || !bd || !ty || !tm || !td) return Number.NaN

  let years = ty - by
  if (tm < bm || (tm === bm && td < bd)) years -= 1
  return years
}

function realDate(value: string): boolean {
  const match = DATE.exec(value)
  if (match === null) return false
  const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])]
  const date = new Date(Date.UTC(year, month - 1, day))
  return (
    date.getUTCFullYear() === year &&
    date.getUTCMonth() === month - 1 &&
    date.getUTCDate() === day &&
    year >= LIMIT.minYear &&
    year <= LIMIT.maxYear
  )
}

/** Does a condition hold on the answers cleaned so far — the server's order (R5). */
export function conditionHolds(field: FormField, answers: Readonly<Record<string, unknown>>): boolean {
  const condition = field.visibleIf
  if (condition === null) return true

  const value = answers[condition.field]
  if (condition.is !== null) return value === condition.is

  const chosen = Array.isArray(value) ? listOf(value) : typeof value === 'string' ? [value] : []
  return condition.in !== null && chosen.some((item) => condition.in?.includes(item))
}

/** The path's top-level field: `jobs[1].period` → `jobs`. */
export function fieldOfPath(path: string): string {
  return path.split(/[.[]/u)[0] ?? path
}

class Check {
  readonly errors: FieldErrors = {}
  readonly answers: Record<string, unknown> = {}
  readonly visible = new Set<string>()

  private readonly thisMonth: string
  private readonly year: number

  constructor(
    private readonly definition: KioskDefinition,
    private readonly draft: DynamicDraft,
    private readonly context: EvalContext,
  ) {
    this.thisMonth = context.today.slice(0, 7)
    this.year = Number(context.today.slice(0, 4)) || new Date().getFullYear()
  }

  run(): this {
    for (const field of candidateFields(this.definition)) {
      if (!conditionHolds(field, this.answers)) continue
      this.visible.add(field.id)

      if (field.kind === 'photo') {
        if (this.context.requireFiles && !this.context.hasPhoto) this.add(field.id, 'photo')
        continue
      }

      if (!hasAnswer(field.kind)) continue

      const value = this.field(field, this.input(field), field.id)
      if (value !== undefined) this.answers[field.id] = value
    }

    return this
  }

  /** The field's draft value; a ticked «same as» copies the other field's text. */
  private input(field: FormField): unknown {
    if (field.sameAs !== null && this.draft.same[field.id] === true) return this.draft.values[field.sameAs.field]
    return this.draft.values[field.id]
  }

  add(path: string, code: KioskErrorCode, values?: FieldError['values']): void {
    if (!(path in this.errors)) this.errors[path] = values === undefined ? { code } : { code, values }
  }

  /** Clean answer, or `undefined` (empty or wrong — the error is recorded). */
  field(field: FormField, value: unknown, path: string): unknown {
    switch (field.kind) {
      case 'short_text':
      case 'long_text':
        return this.text(field, value, path)
      case 'number':
      case 'money':
        return this.number(field, value, path)
      case 'date':
        return this.date(field, value, path)
      case 'month':
        return this.month(field, value, path)
      case 'year':
        return this.year_(field, value, path)
      case 'phone':
        return this.phone(field, value, path)
      case 'email':
        return this.pattern(field, value, path, EMAIL, 'email')
      case 'telegram':
        return this.pattern(field, textOf(value).trim().replace(/^@+/u, ''), path, TELEGRAM, 'telegram')
      case 'yes_no':
        return this.flag(field, value, path)
      case 'choice':
        return this.choice(field, value, path)
      case 'multi_choice':
        return this.multi(field, value, path)
      case 'levels':
        return this.levels(field, value, path)
      case 'repeat':
        return this.repeat(field, value, path)
      case 'period':
        return this.period(field, value, path)
      case 'year_range':
        return this.range(field, value, path)
      case 'consent':
        if (value !== true) this.add(path, 'consent')
        return value === true ? true : undefined
      case 'vacancy':
        return this.vacancy(value, path)
      case 'signature':
        return this.signature(field, value, path)
      case 'referral':
        return this.referral(field, value, path)
      default:
        return undefined
    }
  }

  private empty(field: FormField, path: string): undefined {
    if (field.required) this.add(path, 'required')
    return undefined
  }

  /** Text within its limit and free of forbidden characters; `null` — empty. */
  private clean(value: string, path: string, limit: number, multiline: boolean): string | null | undefined {
    const text = value.trim()
    if (text === '') return null
    if (text.length > limit) {
      this.add(path, 'tooLong', { max: limit })
      return undefined
    }
    if (hasForbiddenCharacter(text, multiline)) {
      this.add(path, 'characters')
      return undefined
    }
    return text
  }

  private text(field: FormField, value: unknown, path: string): unknown {
    const limit = field.maxLength ?? (field.kind === 'long_text' ? LIMIT.long : LIMIT.short)
    const text = this.clean(textOf(value), path, limit, field.kind === 'long_text')
    if (text === undefined) return undefined
    if (text === null) return this.empty(field, path)

    // «.», «-», «1» is not an answer (the built-in form's two-letter rule)
    if (field.minLetters > 0 && (text.match(/\p{L}/gu)?.length ?? 0) < field.minLetters) {
      this.add(path, 'answer')
      return undefined
    }

    return text
  }

  private whole(value: unknown): number | null | undefined {
    const text = textOf(value).trim()
    if (text === '') return null
    return /^-?\d{1,12}$/u.test(text) ? Number(text) : undefined
  }

  private number(field: FormField, value: unknown, path: string): unknown {
    const min = field.min ?? (field.kind === 'money' ? 0 : -LIMIT.number)
    const max = field.max ?? LIMIT.number
    const number = this.whole(value)

    if (number === null) return this.empty(field, path)
    if (number === undefined || number < min || number > max) {
      this.add(path, 'range', { min: grouped(min), max: grouped(max) })
      return undefined
    }
    return number
  }

  private date(field: FormField, value: unknown, path: string): unknown {
    const text = textOf(value).trim()
    if (text === '') return this.empty(field, path)
    if (!realDate(text)) {
      this.add(path, 'invalidDate')
      return undefined
    }

    const bound = field.bind === BIND.birthDate
    if (field.minAge !== null || field.maxAge !== null || bound) {
      const min = bound ? Math.max(field.minAge ?? BOUND_AGE.min, BOUND_AGE.min) : (field.minAge ?? 0)
      const max = bound ? Math.min(field.maxAge ?? BOUND_AGE.max, BOUND_AGE.max) : (field.maxAge ?? 150)
      const years = age(text, this.context.today)
      if (!(years >= min && years <= max)) {
        this.add(path, 'ageRange', { min, max })
        return undefined
      }
    }

    if (field.notBeforeToday && text < this.context.today) {
      this.add(path, 'notBeforeToday')
      return undefined
    }

    return text
  }

  /** `mm.yyyy` → `yyyy-MM` within [fromYear, last]; `null` — empty. */
  private monthValue(value: string, path: string, fromYear: number, last: string): string | null | undefined {
    if (value.trim() === '') return null

    const iso = typedMonthToIso(value)
    if (iso === null || Number(iso.slice(0, 4)) < fromYear) {
      this.add(path, 'month')
      return undefined
    }
    if (iso > last) {
      this.add(path, 'monthFuture')
      return undefined
    }
    return iso
  }

  private month(field: FormField, value: unknown, path: string): unknown {
    const iso = this.monthValue(textOf(value), path, LIMIT.minYear, `${LIMIT.maxYear}-12`)
    if (iso === null) return this.empty(field, path)
    return iso
  }

  private lastYear(field: FormField): number {
    return field.maxYear === 'today' ? this.year : (field.maxYear ?? LIMIT.maxYear)
  }

  private yearValue(field: FormField, value: unknown, path: string): number | null | undefined {
    const year = this.whole(value)
    if (year === null) return null

    const min = field.minYear ?? LIMIT.minYear
    const max = this.lastYear(field)
    if (year === undefined || year < min || year > max) {
      this.add(path, 'range', { min, max })
      return undefined
    }
    return year
  }

  private year_(field: FormField, value: unknown, path: string): unknown {
    const year = this.yearValue(field, value, path)
    if (year === null) return this.empty(field, path)
    return year
  }

  private phone(field: FormField, value: unknown, path: string): unknown {
    // The candidate's own phone goes to the card and finds a returning candidate: mobile only (SPEC §3)
    const mobile = field.mobileOnly || field.bind === BIND.phone
    const text = textOf(value).trim()
    if (text === '') return this.empty(field, path)

    if (mobile) {
      if (!/^\d{9}$/u.test(text)) {
        this.add(path, 'phone')
        return undefined
      }
      return `+998${text}`
    }

    const digits = text.replaceAll(/\D/gu, '').length
    if (text.length > LIMIT.loosePhone || !/^[\d+\-() ]+$/u.test(text) || digits < 7 || digits > 15) {
      this.add(path, 'phoneAny')
      return undefined
    }
    return text
  }

  private pattern(field: FormField, value: unknown, path: string, shape: RegExp, code: KioskErrorCode): unknown {
    const text = this.clean(textOf(value), path, LIMIT.short, false)
    if (text === undefined) return undefined
    if (text === null) return this.empty(field, path)
    if (!shape.test(text)) {
      this.add(path, code)
      return undefined
    }
    return text
  }

  private flag(field: FormField, value: unknown, path: string): unknown {
    const flag = flagOf(value)
    return flag === null ? this.empty(field, path) : flag
  }

  private choice(field: FormField, value: unknown, path: string): unknown {
    const chosen = textOf(value)
    // A value the form no longer offers reads as not chosen
    return field.options.some((option) => option.value === chosen) ? chosen : this.empty(field, path)
  }

  private multi(field: FormField, value: unknown, path: string): unknown {
    const chosen = new Set(listOf(value))
    const ordered = field.options.filter((option) => chosen.has(option.value)).map((option) => option.value)

    if (ordered.length === 0) return this.empty(field, path)
    if (field.minSelected !== null && ordered.length < field.minSelected) {
      this.add(path, 'selectedMin', { min: field.minSelected })
      return undefined
    }
    if (field.maxSelected !== null && ordered.length > field.maxSelected) {
      this.add(path, 'selectedMax', { max: field.maxSelected })
      return undefined
    }
    return ordered
  }

  private levels(field: FormField, value: unknown, path: string): unknown {
    const draft = levelsOf(value, field.aspects.length)
    const scale = field.scale ?? { min: 0, max: 0 }
    const inScale = (level: number | null) => level === null || (level >= scale.min && level <= scale.max)

    const rows: Record<string, (number | null)[]> = {}
    for (const option of field.options) {
      const levels = draft.rows[option.value]
      if (levels === undefined || levels.every((level) => level === null)) continue
      if (!levels.every(inScale)) {
        this.add(`${path}.rows.${option.value}`, 'invalid')
        return undefined
      }
      rows[option.value] = levels
    }

    let failed = false
    for (const required of field.requiredRows) {
      const levels = rows[required]
      if (levels === undefined || levels.some((level) => level === null)) {
        this.add(`${path}.rows.${required}`, 'levelsAll')
        failed = true
      }
    }

    const other: { name: string; levels: (number | null)[] }[] = []
    if (field.other !== null) {
      draft.other.forEach((row, index) => {
        const untouched = row.name.trim() === '' && row.levels.every((level) => level === null)
        if (untouched) return

        const at = `${path}.other[${index}]`
        const name = this.clean(row.name, `${at}.name`, LIMIT.otherName, false)
        if (name === null) this.add(`${at}.name`, 'otherName')
        if (name === null || name === undefined) {
          failed = true
          return
        }
        other.push({ name, levels: row.levels })
      })
    }

    if (failed) return undefined
    if (Object.keys(rows).length === 0 && other.length === 0) return this.empty(field, path)

    return other.length > 0 ? { rows, other } : { rows }
  }

  private repeat(field: FormField, value: unknown, path: string): unknown {
    const rows = rowsOf(value)
    const limit = field.maxRows ?? LIMIT.repeatRows
    const result: Record<string, unknown>[] = []
    let failed = false

    rows.forEach((row, index) => {
      const clean = this.row(field, row, index, path)
      if (clean === undefined) failed = true
      else if (Object.keys(clean).length > 0) result.push(clean)
    })

    if (rows.length > limit) {
      this.add(path, 'rowsMax', { max: limit })
      return undefined
    }
    if (failed) return undefined
    if (result.length === 0) {
      if (field.required) this.add(path, 'rowsMin', { min: Math.max(1, field.minRows) })
      return undefined
    }
    if (result.length < field.minRows) {
      this.add(path, 'rowsMin', { min: field.minRows })
      return undefined
    }
    return result
  }

  /** One row's clean columns, or `undefined` when a column is wrong. */
  row(field: FormField, row: RepeatRow, index: number, path: string): Record<string, unknown> | undefined {
    const clean: Record<string, unknown> = {}
    const before = Object.keys(this.errors).length

    // A row nobody filled is dropped as a whole — its required columns do not count
    const filled = field.columns.some((column) => !isBlank(row.values[column.id]))
    if (!filled) return clean

    for (const column of field.columns) {
      const value = this.field(column, row.values[column.id], `${path}[${index}].${column.id}`)
      if (value !== undefined) clean[column.id] = value
    }

    return Object.keys(this.errors).length > before ? undefined : clean
  }

  private period(field: FormField, value: unknown, path: string): unknown {
    const draft = periodOf(value)
    const current = draft.current && field.allowCurrent
    const from = this.monthValue(draft.from, `${path}.from`, LIMIT.periodFromYear, this.thisMonth)
    const to = current ? null : this.monthValue(draft.to, `${path}.to`, LIMIT.periodFromYear, this.thisMonth)
    if (from === undefined || to === undefined) return undefined

    if (from !== null && to !== null && from > to) {
      this.add(`${path}.to`, 'monthOrder')
      return undefined
    }
    if (from === null && to === null && !current) return this.empty(field, path)
    if (field.required && (from === null || (to === null && !current))) {
      this.add(from === null ? `${path}.from` : `${path}.to`, 'required')
      return undefined
    }

    return { ...(from === null ? {} : { from }), ...(to === null ? {} : { to }), ...(current ? { current: true } : {}) }
  }

  private range(field: FormField, value: unknown, path: string): unknown {
    const draft = rangeOf(value)
    const from = this.yearValue(field, draft.from, `${path}.from`)
    const to = this.yearValue(field, draft.to, `${path}.to`)
    if (from === undefined || to === undefined) return undefined

    if (from !== null && to !== null && from > to) {
      this.add(`${path}.to`, 'yearOrder')
      return undefined
    }
    if (from === null && to === null) return this.empty(field, path)
    if (field.required && (from === null || to === null)) {
      this.add(from === null ? `${path}.from` : `${path}.to`, 'required')
      return undefined
    }

    return { ...(from === null ? {} : { from }), ...(to === null ? {} : { to }) }
  }

  private vacancy(value: unknown, path: string): unknown {
    const chosen = textOf(value)
    if (chosen === 'general' && this.context.generalPoolAvailable) return undefined
    if (UUID.test(chosen) && this.context.vacancyIds.has(chosen)) return chosen

    this.add(path, 'vacancy')
    return undefined
  }

  private signature(field: FormField, value: unknown, path: string): unknown {
    if (this.context.requireFiles && !this.context.hasSignature) this.add(`${path}.pad`, 'signature')

    const namePath = `${path}.name`
    const name = this.clean(signatureNameOf(value), namePath, LIMIT.signatureName, false)
    if (name === undefined) return undefined
    if (name === null) return this.empty(field, namePath)

    const bound = (binding: string) => {
      const target = this.definition.sections
        .filter((section) => !section.office)
        .flatMap((section) => section.screens.flatMap((screen) => [...screen.fields]))
        .find((candidate) => candidate.bind === binding)
      return target === undefined ? '' : textOf(this.answers[target.id])
    }

    if (!signatureMatches(name, bound(BIND.lastName), bound(BIND.firstName))) {
      this.add(namePath, 'signatureName')
      return undefined
    }
    return { name }
  }

  private referral(field: FormField, value: unknown, path: string): unknown {
    const draft = referralOf(value)
    const channelPath = `${path}.channel`
    if (draft.channel === '') return this.empty(field, channelPath)

    const asksDetail = this.context.referralChannels.get(draft.channel)
    if (asksDetail === undefined) {
      this.add(channelPath, 'required')
      return undefined
    }
    if (!asksDetail) return { channel: draft.channel }

    const detailPath = `${path}.detail`
    const detail = this.clean(draft.detail, detailPath, LIMIT.referralDetail, false)
    if (detail === undefined) return undefined
    if (detail === null) {
      this.add(detailPath, 'required')
      return undefined
    }
    return { channel: draft.channel, detail }
  }
}

/** Nothing typed or chosen in a cell. */
function isBlank(value: unknown): boolean {
  if (value === undefined || value === null || value === false) return true
  if (typeof value === 'string') return value.trim() === ''
  if (Array.isArray(value)) return value.length === 0
  if (typeof value === 'object') return Object.values(value as Record<string, unknown>).every(isBlank)
  return false
}

/** Validates the whole draft: errors, the answers to send and what is visible. */
export function evaluate(definition: KioskDefinition, draft: DynamicDraft, context: EvalContext): Evaluation {
  const check = new Check(definition, draft, context).run()
  return { errors: check.errors, answers: check.answers, visible: check.visible }
}

/**
 * One row of a repeating table on its own — the sheet's «Saqlash». Paths are
 * the row's real ones (`jobs[2].period.from`).
 */
export function rowErrors(
  definition: KioskDefinition,
  draft: DynamicDraft,
  context: EvalContext,
  field: FormField,
  index: number,
  row: RepeatRow,
): FieldErrors {
  const check = new Check(definition, draft, context)
  check.row(field, row, index, field.id)
  return check.errors
}

/* ─────────────────────────────────────────────────────────── steps */

/** One wizard step: a definition screen and the section it belongs to. */
export interface WizardStep {
  section: FormSection
  screen: FormScreen
}

/** Every candidate screen, in form order. */
export function allSteps(definition: KioskDefinition): WizardStep[] {
  return candidateSections(definition).flatMap((section) => section.screens.map((screen) => ({ section, screen })))
}

/** The steps this candidate walks: a screen whose fields are all hidden is skipped. */
export function activeSteps(steps: readonly WizardStep[], visible: ReadonlySet<string>): WizardStep[] {
  return steps.filter((step) => step.screen.fields.some((field) => visible.has(field.id)))
}

/** The errors one screen owns (its fields' paths and everything under them). */
export function stepErrors(step: WizardStep, errors: FieldErrors): FieldErrors {
  const ids = new Set(step.screen.fields.map((field) => field.id))
  return Object.fromEntries(Object.entries(errors).filter(([path]) => ids.has(fieldOfPath(path))))
}

/** Errors at a path and under it (`jobs[1]` → `jobs[1].organization`). */
export function errorsUnder(errors: FieldErrors, base: string): FieldErrors {
  return Object.fromEntries(
    Object.entries(errors).filter(([path]) => path === base || path.startsWith(`${base}.`) || path.startsWith(`${base}[`)),
  )
}
