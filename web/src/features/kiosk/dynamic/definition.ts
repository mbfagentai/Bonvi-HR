import type { KioskLanguage } from '../api/kiosk-api'

/*
 * The form HR built in HRMentor's constructor (SPEC-anketa-konstruktori §7,
 * HRMentor SPEC §19.3, `schema: 2`) as the session sends it. The server has
 * already checked it against BonviHR's profile (§5.2) — this reader only keeps
 * a broken or foreign payload from reaching the screens: anything it does not
 * understand makes the whole definition `null`, and `/anketa` falls back to
 * the built-in form instead of rendering half a form.
 */

export type FieldKind =
  | 'short_text'
  | 'long_text'
  | 'number'
  | 'money'
  | 'date'
  | 'month'
  | 'year'
  | 'phone'
  | 'email'
  | 'telegram'
  | 'yes_no'
  | 'choice'
  | 'multi_choice'
  | 'levels'
  | 'repeat'
  | 'period'
  | 'year_range'
  | 'consent'
  | 'info'
  | 'checklist'
  | 'vacancy'
  | 'photo'
  | 'signature'
  | 'referral'

const KINDS: ReadonlySet<string> = new Set<FieldKind>([
  'short_text',
  'long_text',
  'number',
  'money',
  'date',
  'month',
  'year',
  'phone',
  'email',
  'telegram',
  'yes_no',
  'choice',
  'multi_choice',
  'levels',
  'repeat',
  'period',
  'year_range',
  'consent',
  'info',
  'checklist',
  'vacancy',
  'photo',
  'signature',
  'referral',
])

/** Bindings the client itself needs (SPEC §3) — the rest only matter on the server. */
export const BIND = {
  lastName: 'candidate.lastName',
  firstName: 'candidate.firstName',
  middleName: 'candidate.middleName',
  birthDate: 'candidate.birthDate',
  phone: 'candidate.phone',
} as const

/** Whatever the field says, a bound birth date keeps the candidate between these ages (SPEC §3). */
export const BOUND_AGE = { min: 14, max: 100 } as const

/** One text in the three form languages; Latin is always there (HRMentor §18.5). */
export type FormText = Record<KioskLanguage, string>

export interface FormOption {
  value: string
  label: FormText
  group: string | null
}

export interface FormLabelled {
  id: string
  label: FormText
}

/** `visible_if`: `is` on a yes/no, `in` on a choice or multi-choice. */
export interface FormCondition {
  field: string
  is: boolean | null
  in: readonly string[] | null
}

export interface FormField {
  id: string
  kind: FieldKind
  label: FormText | null
  hint: FormText | null
  required: boolean
  bind: string | null
  visibleIf: FormCondition | null

  maxLength: number | null
  minLetters: number
  sameAs: { field: string; label: FormText } | null

  min: number | null
  max: number | null

  minAge: number | null
  maxAge: number | null
  notBeforeToday: boolean
  minYear: number | null
  /** `"max_year": "today"` → `'today'`. */
  maxYear: number | 'today' | null

  /** Uzbek mobile only (the default); `false` — a city or foreign number too. */
  mobileOnly: boolean

  yes: FormText | null
  no: FormText | null

  options: readonly FormOption[]
  groups: readonly FormLabelled[]
  style: 'tiles' | 'list' | null
  minSelected: number | null
  maxSelected: number | null

  aspects: readonly FormLabelled[]
  scale: { min: number; max: number } | null
  requiredRows: readonly string[]
  other: { max: number; label: FormText } | null

  columns: readonly FormField[]
  minRows: number
  maxRows: number | null
  summary: string | null

  allowCurrent: boolean
  text: FormText | null
}

export interface FormScreen {
  id: string
  title: FormText | null
  hint: FormText | null
  fields: readonly FormField[]
}

export interface FormSection {
  id: string
  /** B part — HR fills it on paper; never shown to the candidate. */
  office: boolean
  title: FormText
  hint: FormText | null
  screens: readonly FormScreen[]
}

/** `session.definition` — the version the candidate fills and sends back as `definitionVersion`. */
export interface KioskDefinition {
  /** Stored version; `0` in the preview (an unpublished draft). */
  version: number
  /** Printed version («4.0»), or «draft» in the preview. */
  label: string
  title: FormText
  sections: readonly FormSection[]
}

/* ─────────────────────────────────────────────────────────── reading */

/** The definition could not be read — the caller falls back to the built-in form. */
class Unreadable extends Error {}

type Raw = Record<string, unknown>

function isRecord(value: unknown): value is Raw {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function record(value: unknown): Raw {
  if (!isRecord(value)) throw new Unreadable()
  return value
}

function list(value: unknown): unknown[] {
  if (value === undefined || value === null) return []
  if (!Array.isArray(value)) throw new Unreadable()
  return value
}

function str(value: unknown): string {
  if (typeof value !== 'string') throw new Unreadable()
  return value
}

function optStr(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value : null
}

function int(value: unknown): number | null {
  return typeof value === 'number' && Number.isInteger(value) ? value : null
}

function text(value: unknown): FormText | null {
  if (!isRecord(value)) return null

  const latin = typeof value['uz-Latn'] === 'string' ? value['uz-Latn'].trim() : ''
  const pick = (language: KioskLanguage) => {
    const own = value[language]
    return typeof own === 'string' && own.trim() !== '' ? own.trim() : latin
  }

  return { 'uz-Latn': latin, 'uz-Cyrl': pick('uz-Cyrl'), ru: pick('ru') }
}

function requiredText(value: unknown): FormText {
  const read = text(value)
  if (read === null) throw new Unreadable()
  return read
}

function labelled(value: unknown): FormLabelled[] {
  return list(value).map((item) => {
    const raw = record(item)
    return { id: str(raw['id']), label: requiredText(raw['label']) }
  })
}

function field(raw: Raw): FormField {
  const kind = str(raw['kind'])
  if (!KINDS.has(kind)) throw new Unreadable()

  const condition = raw['visible_if']
  const sameAs = raw['same_as']
  const scale = raw['scale']
  const other = raw['other']
  const style = raw['style']
  const maxYear = raw['max_year']

  return {
    id: str(raw['id']),
    kind: kind as FieldKind,
    label: text(raw['label']),
    hint: text(raw['hint']),
    required: raw['required'] === true,
    bind: optStr(raw['bind']),
    visibleIf: isRecord(condition)
      ? {
          field: str(condition['field']),
          is: typeof condition['is'] === 'boolean' ? condition['is'] : null,
          in: Array.isArray(condition['in']) ? condition['in'].filter((item) => typeof item === 'string') : null,
        }
      : null,

    maxLength: int(raw['max_length']),
    minLetters: int(raw['min_letters']) ?? 0,
    sameAs: isRecord(sameAs) ? { field: str(sameAs['field']), label: requiredText(sameAs['label']) } : null,

    min: int(raw['min']),
    max: int(raw['max']),

    minAge: int(raw['min_age']),
    maxAge: int(raw['max_age']),
    notBeforeToday: raw['not_before_today'] === true,
    minYear: int(raw['min_year']),
    maxYear: maxYear === 'today' ? 'today' : int(maxYear),

    mobileOnly: raw['mobile_only'] !== false,

    yes: text(raw['yes']),
    no: text(raw['no']),

    options: list(raw['options']).map((item) => {
      const option = record(item)
      return { value: str(option['value']), label: requiredText(option['label']), group: optStr(option['group']) }
    }),
    groups: labelled(raw['groups']),
    style: style === 'tiles' || style === 'list' ? style : null,
    minSelected: int(raw['min_selected']),
    maxSelected: int(raw['max_selected']),

    aspects: labelled(raw['aspects']),
    scale: isRecord(scale) && int(scale['min']) !== null && int(scale['max']) !== null
      ? { min: int(scale['min']) ?? 0, max: int(scale['max']) ?? 0 }
      : null,
    requiredRows: list(raw['required_rows']).filter((item): item is string => typeof item === 'string'),
    other: isRecord(other) && int(other['max']) !== null
      ? { max: int(other['max']) ?? 0, label: requiredText(other['label']) }
      : null,

    columns: list(raw['columns']).map((column) => field(record(column))),
    minRows: int(raw['min_rows']) ?? 0,
    maxRows: int(raw['max_rows']),
    summary: optStr(raw['summary']),

    allowCurrent: raw['allow_current'] === true,
    text: text(raw['text']),
  }
}

/**
 * `{ version, label, definition }` of the session → the form, or `null` when
 * absent or unreadable (then the built-in form is used).
 */
export function parseDefinition(value: unknown): KioskDefinition | null {
  if (!isRecord(value)) return null

  try {
    const version = int(value['version'])
    const label = str(value['label'])
    const definition = record(value['definition'])
    if (version === null || version < 0) return null

    const sections = list(definition['sections']).map((item): FormSection => {
      const section = record(item)
      return {
        id: str(section['id']),
        office: section['audience'] === 'office',
        title: requiredText(section['title']),
        hint: text(section['hint']),
        screens: list(section['screens']).map((screenItem) => {
          const screen = record(screenItem)
          return {
            id: str(screen['id']),
            title: text(screen['title']),
            hint: text(screen['hint']),
            fields: list(screen['fields']).map((fieldItem) => field(record(fieldItem))),
          }
        }),
      }
    })

    return { version, label, title: requiredText(definition['title']), sections }
  } catch (error) {
    if (error instanceof Unreadable) return null
    throw error
  }
}

/* ─────────────────────────────────────────────────────────── helpers */

/** The candidate's sections, in form order. */
export function candidateSections(definition: KioskDefinition): FormSection[] {
  return definition.sections.filter((section) => !section.office)
}

/** Every top-level field the candidate fills, in form order. */
export function candidateFields(definition: KioskDefinition): FormField[] {
  return candidateSections(definition).flatMap((section) => section.screens.flatMap((screen) => [...screen.fields]))
}

/** Kinds that hold no answer — text on the screen, the photo travels as a file. */
export function hasAnswer(kind: FieldKind): boolean {
  return kind !== 'info' && kind !== 'checklist' && kind !== 'photo'
}

/** The text in the chosen language; Latin when that one is empty. */
export function textIn(value: FormText | null | undefined, language: KioskLanguage): string {
  if (value === null || value === undefined) return ''
  return value[language] || value['uz-Latn']
}
