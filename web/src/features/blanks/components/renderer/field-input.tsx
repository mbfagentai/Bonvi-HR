import { useId, type ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { todayIso } from '@/shared/lib/today'
import { Checkbox, DateField, MoneyInput, PercentInput, TextArea, TextField } from '@/shared/ui'

import { resolveDateBound, textMaxLength } from '../../lib/answers-zod'
import { EMPTY_MARK, formatAnswer, renderStaticText } from '../../lib/blank-presentation'
import type { ColumnField, Field, JsonValue, OptionExtra } from '../../lib/definition-types'
import { ChoiceField, ScoreInput, YesNoInput } from './choice-field'
import { ComputedFieldView } from './computed-field'
import { FileField } from './file-field'
import { DocRefField, RefField } from './ref-field'
import { useRenderer } from './renderer-context'
import { TableField } from './table-field'

/** Tashkent offset — datetime answers are stored as ISO with offset (§4.3, +5 rule). */
const TASHKENT_OFFSET = '+05:00'

type AnyField = Field | ColumnField

function isObject(value: JsonValue | undefined): value is { [key: string]: JsonValue } {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function str(value: JsonValue | undefined): string {
  return typeof value === 'string' ? value : ''
}

function numText(value: JsonValue | undefined): string {
  return typeof value === 'number' ? String(value) : ''
}

/** Digits (optionally signed) → integer, '' → null; anything else keeps the old value. */
function parseInteger(text: string, previous: JsonValue | undefined): JsonValue {
  const trimmed = text.trim()
  if (trimmed === '') return null
  if (!/^-?\d{1,15}$/.test(trimmed)) return previous ?? null
  return Number(trimmed)
}

/** A synthetic field for an option's extra input (`{ value, extra }`). */
function extraField(extra: OptionExtra, id: string): ColumnField {
  const base = { id, label: extra.label, required: extra.required ?? false }
  switch (extra.type) {
    case 'ref':
      return { ...base, type: 'ref', refKind: extra.refKind ?? 'employee' }
    case 'docRef':
      return { ...base, type: 'docRef', templateCodes: extra.templateCodes ?? [] }
    default:
      return { ...base, type: extra.type }
  }
}

/** Label + hint + error around controls the UI kit does not wrap itself. */
function FieldShell({
  label,
  hint,
  error,
  compact,
  children,
}: {
  label: ReactNode
  hint: string | undefined
  error: string | undefined
  compact: boolean
  children: ReactNode
}) {
  const id = useId()
  if (compact) {
    return (
      <div className="space-y-1">
        {children}
        {error && (
          <p role="alert" className="text-footnote text-ios-red">
            {error}
          </p>
        )}
      </div>
    )
  }
  return (
    <div role="group" aria-labelledby={`${id}-label`} className="space-y-2">
      <p id={`${id}-label`} className="text-footnote font-medium text-[var(--fg-secondary)]">
        {label}
      </p>
      {children}
      {error ? (
        <p role="alert" className="text-footnote text-ios-red">
          {error}
        </p>
      ) : (
        hint && <p className="text-footnote text-[var(--fg-tertiary)]">{hint}</p>
      )}
    </div>
  )
}

/**
 * One field of a blank — the type switch of the renderer (§4.3).
 *
 * `path` is the answers path (`fieldId`, `table[i].col`, `field.extra`): it keys
 * errors and computed values. `compact` = inside a table cell (no visible
 * label; the column header names it). `readOnly` prints the stored value.
 */
export function FieldInput({
  field,
  path,
  value,
  onChange,
  readOnly,
  compact = false,
}: {
  field: AnyField
  path: string
  value: JsonValue | undefined
  onChange: (value: JsonValue) => void
  readOnly: boolean
  compact?: boolean
}) {
  const { t, locale } = useLocale()
  const { answers, computed, errors, errorText } = useRenderer()
  const error = errorText(errors[path])
  const locked = readOnly || field.readOnly === true
  const plainLabel = field.label ?? ''
  const label: ReactNode = (
    <>
      {plainLabel}
      {field.required && !locked && <span className="text-ios-red"> *</span>}
    </>
  )
  const ariaLabel = compact ? plainLabel : undefined
  const yesNo: [string, string] = [t('blanks.form.yes'), t('blanks.form.no')]
  const today = todayIso()

  const renderExtra = (extra: OptionExtra, extraValue: JsonValue | undefined, set: (v: JsonValue) => void, extraPath: string) => (
    <FieldInput
      field={extraField(extra, `${field.id}Extra`)}
      path={extraPath}
      value={extraValue}
      onChange={set}
      readOnly={locked}
    />
  )

  // ── kinds that render the same way read-only and editable ──
  if (field.type === 'static') {
    const text = renderStaticText(field.text, (id) => {
      const target = answers[id]
      return typeof target === 'string' || typeof target === 'number' ? String(target) : isObject(target) ? str(target['label']) || str(target['value']) : EMPTY_MARK
    })
    return <p className="text-body whitespace-pre-line text-[var(--fg-secondary)]">{text}</p>
  }
  if (field.type === 'computed') return <ComputedFieldView field={field} value={computed[path]} compact={compact} />
  if (field.type === 'table') {
    return (
      <FieldShell label={label} hint={field.hint} error={error} compact={false}>
        <TableField
          field={field}
          path={path}
          value={value}
          onChange={onChange}
          disabled={locked}
          renderCell={(column, cellValue, setCell, cellPath) => (
            <FieldInput field={column} path={cellPath} value={cellValue} onChange={setCell} readOnly={locked} compact />
          )}
        />
      </FieldShell>
    )
  }
  if (field.type === 'file') {
    return (
      <FieldShell label={label} hint={field.hint} error={error} compact={compact}>
        <FileField field={field} path={path} value={value} onChange={onChange} disabled={locked} />
      </FieldShell>
    )
  }

  // ── read-only print ──
  if (locked) {
    const text = formatAnswer(field, value, locale, yesNo)
    const body = (
      <p className={text === EMPTY_MARK ? 'text-body text-[var(--fg-tertiary)]' : 'text-body whitespace-pre-line'}>{text}</p>
    )
    return compact ? (
      <div className="px-1 py-2">{body}</div>
    ) : (
      <FieldShell label={label} hint={undefined} error={error} compact={false}>
        {body}
      </FieldShell>
    )
  }

  // ── editable ──
  const set = (next: JsonValue) => onChange(next === '' ? null : next)

  switch (field.type) {
    case 'text':
      return (
        <TextField
          label={compact ? undefined : label}
          aria-label={ariaLabel}
          hint={field.hint}
          error={error}
          value={str(value)}
          maxLength={textMaxLength(field)}
          size={compact ? 'sm' : 'md'}
          onChange={(e) => set(e.target.value)}
        />
      )
    case 'longtext':
      return (
        <TextArea
          label={compact ? undefined : label}
          aria-label={ariaLabel}
          hint={field.hint}
          error={error}
          value={str(value)}
          maxLength={textMaxLength(field)}
          rows={compact ? 2 : 4}
          onChange={(e) => set(e.target.value)}
        />
      )
    case 'int':
    case 'year':
      return (
        <TextField
          label={compact ? undefined : label}
          aria-label={ariaLabel}
          hint={field.hint}
          error={error}
          inputMode="numeric"
          value={numText(value)}
          size={compact ? 'sm' : 'md'}
          onChange={(e) => onChange(parseInteger(e.target.value, value))}
        />
      )
    case 'money':
      return (
        <MoneyInput
          label={compact ? undefined : label}
          aria-label={ariaLabel}
          hint={field.hint}
          error={error}
          value={numText(value)}
          size={compact ? 'sm' : 'md'}
          onChange={(digits) => onChange(digits === '' ? null : Number(digits))}
        />
      )
    case 'percent':
      return (
        <PercentInput
          label={compact ? undefined : label}
          aria-label={ariaLabel}
          hint={field.hint ?? (field.passAt !== undefined ? t('blanks.form.passAt', { value: field.passAt }) : undefined)}
          error={error}
          value={numText(value)}
          size={compact ? 'sm' : 'md'}
          onChange={(canonical) => onChange(canonical === '' ? null : Number(canonical))}
        />
      )
    case 'date': {
      const min = resolveDateBound(field.min, today)
      const max = resolveDateBound(field.max, today)
      return (
        <DateField
          label={compact ? undefined : label}
          ariaLabel={ariaLabel}
          hint={field.hint}
          error={error}
          value={str(value)}
          size={compact ? 'sm' : 'md'}
          {...(min ? { min } : {})}
          {...(max ? { max } : {})}
          onChange={(iso) => set(iso)}
        />
      )
    }
    case 'datetime':
      return (
        <TextField
          label={compact ? undefined : label}
          aria-label={ariaLabel}
          hint={field.hint}
          error={error}
          type="datetime-local"
          value={str(value).slice(0, 16)}
          size={compact ? 'sm' : 'md'}
          onChange={(e) => set(e.target.value ? `${e.target.value}:00${TASHKENT_OFFSET}` : '')}
        />
      )
    case 'month':
      return (
        <TextField
          label={compact ? undefined : label}
          aria-label={ariaLabel}
          hint={field.hint}
          error={error}
          type="month"
          value={str(value)}
          size={compact ? 'sm' : 'md'}
          onChange={(e) => set(e.target.value)}
        />
      )
    case 'dateRange': {
      const range = isObject(value) ? value : {}
      const update = (key: 'from' | 'to', iso: string) => {
        const next = { from: str(range['from']), to: str(range['to']), [key]: iso }
        onChange(next.from === '' && next.to === '' ? null : next)
      }
      return (
        <FieldShell label={label} hint={field.hint} error={error ?? errorText(errors[`${path}.from`]) ?? errorText(errors[`${path}.to`])} compact={compact}>
          <div className="grid gap-2 sm:grid-cols-2">
            <DateField ariaLabel={t('blanks.form.from')} value={str(range['from'])} onChange={(iso) => update('from', iso)} size={compact ? 'sm' : 'md'} />
            <DateField ariaLabel={t('blanks.form.to')} value={str(range['to'])} onChange={(iso) => update('to', iso)} size={compact ? 'sm' : 'md'} />
          </div>
        </FieldShell>
      )
    }
    case 'yesno':
      return (
        <FieldShell label={label} hint={field.hint} error={error} compact={compact}>
          <YesNoInput value={value} onChange={onChange} label={plainLabel} />
        </FieldShell>
      )
    case 'check':
      return (
        <div className="space-y-1">
          <Checkbox checked={value === true} onChange={(checked) => onChange(checked)} label={compact ? undefined : label} />
          {error && (
            <p role="alert" className="text-footnote text-ios-red">
              {error}
            </p>
          )}
        </div>
      )
    case 'score':
      return (
        <FieldShell label={label} hint={field.hint} error={error} compact={compact}>
          <ScoreInput value={value} onChange={onChange} label={plainLabel} />
        </FieldShell>
      )
    case 'choice':
    case 'multi':
      return (
        <FieldShell label={label} hint={field.hint} error={error ?? errorText(errors[`${path}.value`])} compact={compact}>
          <ChoiceField field={field} value={value} onChange={onChange} path={path} renderExtra={renderExtra} />
        </FieldShell>
      )
    case 'ref':
      return (
        <FieldShell label={label} hint={field.hint} error={error} compact={compact}>
          <RefField refKind={field.refKind} value={value} onChange={onChange} label={plainLabel} invalid={error !== undefined} />
        </FieldShell>
      )
    case 'docRef':
      return (
        <FieldShell label={label} hint={field.hint} error={error} compact={compact}>
          <DocRefField
            templateCodes={field.templateCodes}
            value={value}
            onChange={onChange}
            label={plainLabel}
            invalid={error !== undefined}
          />
        </FieldShell>
      )
    case 'order': {
      const order = isObject(value) ? value : {}
      const update = (key: 'number' | 'date', text: string) => {
        const next = { number: str(order['number']), date: str(order['date']), [key]: text }
        onChange(next.number === '' && next.date === '' ? null : next)
      }
      return (
        <FieldShell label={label} hint={field.hint} error={error} compact={compact}>
          <div className="grid gap-2 sm:grid-cols-2">
            <TextField
              aria-label={t('blanks.form.orderNumber')}
              placeholder={t('blanks.form.orderNumber')}
              maxLength={50}
              value={str(order['number'])}
              error={errorText(errors[`${path}.number`])}
              onChange={(e) => update('number', e.target.value)}
            />
            <DateField
              ariaLabel={t('blanks.form.orderDate')}
              value={str(order['date'])}
              error={errorText(errors[`${path}.date`])}
              onChange={(iso) => update('date', iso)}
            />
          </div>
        </FieldShell>
      )
    }
  }
}
