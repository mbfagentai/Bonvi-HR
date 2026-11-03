import { formatDate, formatDateTime, formatMoney, formatNumber } from '@/shared/i18n/format'
import type { LocaleCode } from '@/shared/i18n/locales'
import type { BadgeTone } from '@/shared/ui'

import type {
  BlankDefinition,
  BlankSubmissionStatus,
  ColumnField,
  ComputedField,
  Field,
  FieldOption,
  JsonValue,
  Section,
  SectionFill,
} from './definition-types'

/** Status → badge tone (list, view and fill pages share it). */
export const STATUS_TONE: Record<BlankSubmissionStatus, BadgeTone> = {
  Draft: 'neutral',
  InApproval: 'warning',
  Approved: 'success',
  Rejected: 'danger',
  Cancelled: 'neutral',
}

/** Status → i18n key. */
export const STATUS_KEY: Record<BlankSubmissionStatus, string> = {
  Draft: 'blanks.form.status.draft',
  InApproval: 'blanks.form.status.inApproval',
  Approved: 'blanks.form.status.approved',
  Rejected: 'blanks.form.status.rejected',
  Cancelled: 'blanks.form.status.cancelled',
}

export const EMPTY_MARK = '—'

export function sectionFill(section: Section): SectionFill {
  return section.fill ?? { at: 'draft' }
}

/** Index of fields by id (top level only; table columns are addressed by path). */
export function fieldsById(definition: BlankDefinition): Map<string, Field> {
  const map = new Map<string, Field>()
  for (const section of definition.sections) for (const field of section.fields) map.set(field.id, field)
  return map
}

/** Label of the field a server/client error path points at: `t[2].c` → «T › 3 › C». */
export function labelForPath(definition: BlankDefinition, path: string): string | null {
  const match = /^([a-zA-Z0-9]+)(?:\[(\d+)\])?(?:\.([a-zA-Z0-9]+))?/.exec(path)
  if (!match) return null
  const [, fieldId = '', row, col] = match
  const field = fieldsById(definition).get(fieldId)
  if (!field) return null
  const parts = [field.label ?? field.id]
  if (row !== undefined) {
    const fixed = field.type === 'table' ? field.fixedRows?.[Number(row)] : undefined
    parts.push(fixed ? fixed.label : String(Number(row) + 1))
  }
  if (col && field.type === 'table') {
    const column = field.columns.find((c) => c.id === col)
    if (column) parts.push(column.label ?? column.id)
  }
  return parts.join(' › ')
}

function optionLabel(options: FieldOption[], value: string): string {
  return options.find((o) => o.value === value)?.label ?? value
}

function isObject(value: JsonValue | undefined): value is { [key: string]: JsonValue } {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function str(value: JsonValue | undefined): string {
  return typeof value === 'string' ? value : ''
}

function extraText(value: JsonValue | undefined, locale: LocaleCode): string {
  if (value === undefined || value === null || value === '') return ''
  if (typeof value === 'string') return /^\d{4}-\d{2}-\d{2}$/.test(value) ? formatDate(value, locale) : value
  if (typeof value === 'number') return formatNumber(value, locale)
  if (isObject(value)) return str(value['label']) || str(value['number'])
  return ''
}

/** Plain-text rendering of a computed value — numbers to 2 decimals. */
export function formatComputed(field: ComputedField, value: JsonValue | undefined, locale: LocaleCode, yesNo: [string, string]): string {
  if (value === undefined || value === null) return EMPTY_MARK
  switch (field.valueType) {
    case 'number':
      return typeof value === 'number' ? formatNumber(Math.round(value * 100) / 100, locale) : EMPTY_MARK
    case 'date':
      return typeof value === 'string' ? formatDate(value, locale) : EMPTY_MARK
    case 'bool':
      return typeof value === 'boolean' ? (value ? yesNo[0] : yesNo[1]) : EMPTY_MARK
    case 'text':
      return String(value)
  }
}

/**
 * Read-only text of one stored answer. `yesNo` = translated [«Ha», «Yo'q»].
 * Files, tables and static text are rendered by their own components.
 */
export function formatAnswer(
  field: Field | ColumnField,
  value: JsonValue | undefined,
  locale: LocaleCode,
  yesNo: [string, string],
): string {
  if (value === undefined || value === null || value === '') return EMPTY_MARK
  switch (field.type) {
    case 'text':
    case 'longtext':
      return typeof value === 'string' ? value : EMPTY_MARK
    case 'int':
    case 'year':
    case 'score':
      return typeof value === 'number' ? String(value) : EMPTY_MARK
    case 'percent':
      return typeof value === 'number' ? `${formatNumber(value, locale)} %` : EMPTY_MARK
    case 'money':
      return typeof value === 'number' ? formatMoney(value, locale) : EMPTY_MARK
    case 'date':
      return typeof value === 'string' ? formatDate(value, locale) : EMPTY_MARK
    case 'datetime':
      return typeof value === 'string' ? formatDateTime(value, locale) : EMPTY_MARK
    case 'month':
      return typeof value === 'string' ? value.split('-').reverse().join('.') : EMPTY_MARK
    case 'dateRange':
      return isObject(value)
        ? `${formatDate(str(value['from']), locale)} – ${formatDate(str(value['to']), locale)}`
        : EMPTY_MARK
    case 'yesno':
    case 'check':
      return typeof value === 'boolean' ? (value ? yesNo[0] : yesNo[1]) : EMPTY_MARK
    case 'choice': {
      if (!isObject(value)) return EMPTY_MARK
      const label = optionLabel(field.options, str(value['value']))
      const extra = extraText(value['extra'], locale)
      return extra ? `${label}: ${extra}` : label
    }
    case 'multi': {
      if (!Array.isArray(value) || value.length === 0) return EMPTY_MARK
      return value
        .filter(isObject)
        .map((item) => {
          const label = optionLabel(field.options, str(item['value']))
          const extra = extraText(item['extra'], locale)
          return extra ? `${label}: ${extra}` : label
        })
        .join('; ')
    }
    case 'ref':
      return isObject(value) ? str(value['label']) || EMPTY_MARK : EMPTY_MARK
    case 'docRef':
      return isObject(value) ? `${str(value['templateCode'])} № ${str(value['number'])}` : EMPTY_MARK
    case 'order':
      return isObject(value)
        ? `№ ${str(value['number'])}${value['date'] ? `, ${formatDate(str(value['date']), locale)}` : ''}`
        : EMPTY_MARK
    case 'computed':
      return formatComputed(field, value, locale, yesNo)
    case 'file':
    case 'table':
    case 'static':
      return EMPTY_MARK
  }
}

/** `static` text with `{{fieldId}}` placeholders filled from the answers. */
export function renderStaticText(
  text: string,
  resolve: (fieldId: string) => string,
): string {
  return text.replace(/\{\{\s*([a-zA-Z0-9]+)\s*\}\}/g, (_, id: string) => resolve(id))
}

/** Human file size: «1,2 MB». */
export function formatFileSize(bytes: number, locale: LocaleCode): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${formatNumber(Math.round(bytes / 102.4) / 10, locale)} KB`
  return `${formatNumber(Math.round(bytes / (1024 * 102.4)) / 10, locale)} MB`
}
