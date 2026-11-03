import { Check } from 'lucide-react'
import type { ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'

import type {
  ChoiceAnswer,
  ChoiceField as ChoiceFieldDef,
  FieldOption,
  JsonValue,
  MultiField,
  OptionExtra,
} from '../../lib/definition-types'

export type RenderExtra = (
  extra: OptionExtra,
  value: JsonValue | undefined,
  onChange: (value: JsonValue) => void,
  path: string,
) => ReactNode

function isChoice(value: JsonValue | undefined): value is { [key: string]: JsonValue } & { value: string } {
  return typeof value === 'object' && value !== null && !Array.isArray(value) && typeof value['value'] === 'string'
}

function toChoices(value: JsonValue | undefined): ChoiceAnswer[] {
  if (Array.isArray(value)) return value.filter(isChoice).map((v) => toChoice(v))
  return isChoice(value) ? [toChoice(value)] : []
}

function toChoice(value: { [key: string]: JsonValue } & { value: string }): ChoiceAnswer {
  return value['extra'] === undefined ? { value: value.value } : { value: value.value, extra: value['extra'] }
}

function serialize(item: ChoiceAnswer): JsonValue {
  return item.extra === undefined || item.extra === null ? { value: item.value } : { value: item.value, extra: item.extra }
}

/**
 * One option row — iOS list style: the whole row is the hit target, the
 * selection is a check mark (multi: a rounded box), never a tiny radio dot.
 */
function OptionRow({
  option,
  selected,
  multi,
  disabled,
  onToggle,
  children,
}: {
  option: FieldOption
  selected: boolean
  multi: boolean
  disabled: boolean
  onToggle: () => void
  children?: ReactNode
}) {
  return (
    <div className="border-b border-[var(--border)] last:border-b-0">
      <button
        type="button"
        role={multi ? 'checkbox' : 'radio'}
        aria-checked={selected}
        disabled={disabled}
        onClick={onToggle}
        className={cn(
          'flex min-h-11 w-full items-center gap-3 px-3.5 py-2 text-left text-body',
          'hover:bg-[var(--bg-hover)] disabled:cursor-not-allowed disabled:opacity-50',
        )}
      >
        <span
          aria-hidden
          className={cn(
            'flex size-5 shrink-0 items-center justify-center rounded-full border',
            multi && 'rounded-[6px]',
            selected
              ? 'border-[var(--accent)] bg-[var(--accent)] text-[var(--fg-on-accent)]'
              : 'border-[var(--border-strong)]',
          )}
        >
          {selected && <Check className="size-3.5" strokeWidth={3} />}
        </span>
        <span className="min-w-0 flex-1">{option.label}</span>
      </button>
      {children && <div className="px-3.5 pb-3 pl-11">{children}</div>}
    </div>
  )
}

/**
 * `choice` (one) and `multi` (many, `maxSelected`) with per-option extra
 * inputs (text / number / date / ref …). The extra input appears under the
 * selected option and its value is stored as `{ value, extra }` (§4.3).
 */
export function ChoiceField({
  field,
  value,
  onChange,
  path,
  disabled = false,
  renderExtra,
}: {
  field: ChoiceFieldDef | MultiField
  value: JsonValue | undefined
  onChange: (value: JsonValue) => void
  path: string
  disabled?: boolean
  renderExtra: RenderExtra
}) {
  const { t } = useLocale()
  const multi = field.type === 'multi'
  const selected = toChoices(value)
  const max = field.type === 'multi' ? field.maxSelected : undefined
  const full = max !== undefined && selected.length >= max

  const toggle = (option: FieldOption) => {
    const exists = selected.some((s) => s.value === option.value)
    if (!multi) {
      onChange(exists ? null : { value: option.value })
      return
    }
    const next = exists ? selected.filter((s) => s.value !== option.value) : [...selected, { value: option.value }]
    onChange(next.length === 0 ? null : next.map(serialize))
  }

  const setExtra = (optionValue: string, extra: JsonValue) => {
    const next = selected.map((s) => (s.value === optionValue ? { value: s.value, extra } : s))
    onChange(multi ? next.map(serialize) : (next[0] ? serialize(next[0]) : null))
  }

  return (
    <div className="space-y-1.5">
      {max !== undefined && (
        <p className="text-footnote text-[var(--fg-secondary)]">{t('blanks.form.maxSelected', { max })}</p>
      )}
      <div
        role={multi ? 'group' : 'radiogroup'}
        aria-label={field.label}
        className="overflow-hidden rounded-md border border-[var(--border)] bg-[var(--bg-elevated)]"
      >
        {field.options.map((option) => {
          const index = selected.findIndex((s) => s.value === option.value)
          const item = selected[index]
          const isSelected = index >= 0
          const extraPath = multi ? `${path}[${index}].extra` : `${path}.extra`
          return (
            <OptionRow
              key={option.value}
              option={option}
              selected={isSelected}
              multi={multi}
              disabled={disabled || (multi && full && !isSelected)}
              onToggle={() => toggle(option)}
            >
              {isSelected && option.extra && item
                ? renderExtra(option.extra, item.extra, (extra) => setExtra(option.value, extra), extraPath)
                : null}
            </OptionRow>
          )
        })}
      </div>
    </div>
  )
}

/** Ha / Yo'q — two segments; a third tap on the active one clears it. */
export function YesNoInput({
  value,
  onChange,
  disabled = false,
  label,
}: {
  value: JsonValue | undefined
  onChange: (value: boolean | null) => void
  disabled?: boolean
  label?: string
}) {
  const { t } = useLocale()
  const options: [boolean, string][] = [
    [true, t('blanks.form.yes')],
    [false, t('blanks.form.no')],
  ]
  return (
    <div role="radiogroup" aria-label={label} className="inline-flex rounded-md bg-[var(--bg-sunken)] p-0.5">
      {options.map(([option, text]) => (
        <button
          key={text}
          type="button"
          role="radio"
          aria-checked={value === option}
          disabled={disabled}
          onClick={() => onChange(value === option ? null : option)}
          className={cn(
            'h-9 min-w-20 rounded-[8px] px-4 text-subhead font-medium transition-colors',
            value === option
              ? 'bg-[var(--bg-elevated)] text-[var(--fg)] shadow-card'
              : 'text-[var(--fg-secondary)] hover:text-[var(--fg)]',
            'disabled:cursor-not-allowed disabled:opacity-50',
          )}
        >
          {text}
        </button>
      ))}
    </div>
  )
}

/** Score 1–5 (fixed scale, §4.3). */
export function ScoreInput({
  value,
  onChange,
  disabled = false,
  label,
}: {
  value: JsonValue | undefined
  onChange: (value: number | null) => void
  disabled?: boolean
  label?: string
}) {
  return (
    <div role="radiogroup" aria-label={label} className="inline-flex gap-1">
      {[1, 2, 3, 4, 5].map((score) => (
        <button
          key={score}
          type="button"
          role="radio"
          aria-checked={value === score}
          disabled={disabled}
          onClick={() => onChange(value === score ? null : score)}
          className={cn(
            'size-9 rounded-full border text-subhead font-semibold tabular-nums transition-colors',
            value === score
              ? 'border-[var(--accent)] bg-[var(--accent)] text-[var(--fg-on-accent)]'
              : 'border-[var(--border)] text-[var(--fg-secondary)] hover:bg-[var(--bg-hover)]',
            'disabled:cursor-not-allowed disabled:opacity-50',
          )}
        >
          {score}
        </button>
      ))}
    </div>
  )
}
