import { Check } from 'lucide-react'
import { useCallback, useId, type ChangeEvent, type ComponentProps, type ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { TextArea, TextField } from '@/shared/ui'
import type { FieldErrors } from '../lib/kiosk-draft'

/*
 * Building blocks of the public form. The candidate may be on a phone, often
 * filling a form like this for the first time, so everything is larger than
 * in the HR app: labels at body size in the main text colour, inputs 52 px,
 * choices as full-width tiles. `data-field` carries the validation path so the
 * wizard can scroll to the first error.
 */

/**
 * The shared controls (`TextField`, `Select`, `DateField`, …) draw a small grey
 * label for HR forms; on the public form the question is the main text.
 */
const FIELD_LOOK = cn(
  '[&_label]:text-body [&_label]:leading-snug [&_label]:font-medium [&_label]:text-[var(--fg)]',
  '[&_p]:text-subhead [&_select]:h-[52px] [&_select]:text-body',
)

/** `path` → translated message, or `undefined` when the field is fine. */
export function useFieldError(errors: FieldErrors) {
  const { t } = useLocale()

  return useCallback(
    (path: string): string | undefined => {
      const error = errors[path]
      return error === undefined ? undefined : t(`kiosk.error.${error.code}`, error.values)
    },
    [errors, t],
  )
}

/** Question text with the red «*» of a required answer (the start screen explains it). */
export function Label({ children, required = false }: { children: ReactNode; required?: boolean }) {
  return (
    <>
      {children}
      {required && (
        <span aria-hidden className="text-ios-red">
          {' *'}
        </span>
      )}
    </>
  )
}

/** One-line explanation under the screen title. */
export function ScreenHint({ children }: { children: ReactNode }) {
  return <p className="text-callout leading-relaxed text-[var(--fg-secondary)]">{children}</p>
}

/** Wraps a shared control: validation path for «scroll to error» and the larger label. */
export function KioskField({
  path,
  className,
  children,
}: {
  path: string
  className?: string
  children: ReactNode
}) {
  return (
    <div data-field={path} className={cn(FIELD_LOOK, className)}>
      {children}
    </div>
  )
}

/** Text input, 52 px. */
export function KioskText({
  path,
  className,
  label,
  required,
  ...props
}: Omit<ComponentProps<typeof TextField>, 'size' | 'variant'> & { path: string; required?: boolean }) {
  return (
    <KioskField path={path} className={className}>
      <TextField size="lg" label={label === undefined ? undefined : <Label required={required}>{label}</Label>} {...props} />
    </KioskField>
  )
}

/** Long answer. The counter appears near the limit. */
export function KioskTextArea({
  path,
  label,
  required,
  value,
  onChange,
  error,
  maxLength,
  rows = 3,
  placeholder,
  className,
}: {
  path: string
  label: ReactNode
  required?: boolean
  value: string
  onChange: (value: string) => void
  error?: string | undefined
  maxLength: number
  rows?: number
  placeholder?: string
  className?: string
}) {
  const nearLimit = value.length > maxLength * 0.8

  return (
    <KioskField path={path} className={className}>
      <TextArea
        label={<Label required={required}>{label}</Label>}
        value={value}
        rows={rows}
        placeholder={placeholder}
        error={error}
        maxLength={maxLength}
        hint={nearLimit ? `${value.length} / ${maxLength}` : undefined}
        onChange={(event) => onChange(event.target.value)}
      />
    </KioskField>
  )
}

/** Digits only (years, counts, months of service). */
export function DigitsField({
  path,
  value,
  onChange,
  maxLength,
  ...props
}: Omit<ComponentProps<typeof KioskText>, 'value' | 'onChange' | 'inputMode'> & {
  value: string
  onChange: (value: string) => void
  maxLength: number
}) {
  return (
    <KioskText
      path={path}
      inputMode="numeric"
      autoComplete="off"
      value={value}
      maxLength={maxLength}
      onChange={(event: ChangeEvent<HTMLInputElement>) =>
        onChange(event.target.value.replaceAll(/\D/gu, '').slice(0, maxLength))
      }
      inputClassName="tabular"
      {...props}
    />
  )
}

/** `mm.yyyy` with the dot inserted while typing — the paper's «oy.yil». */
export function MonthField({
  path,
  value,
  onChange,
  ...props
}: Omit<ComponentProps<typeof KioskText>, 'value' | 'onChange' | 'inputMode'> & {
  value: string
  onChange: (value: string) => void
}) {
  const mask = (raw: string) => {
    const digits = raw.replaceAll(/\D/gu, '').slice(0, 6)
    return digits.length > 2 ? `${digits.slice(0, 2)}.${digits.slice(2)}` : digits
  }

  return (
    <KioskText
      path={path}
      inputMode="numeric"
      autoComplete="off"
      placeholder="mm.yyyy"
      value={value}
      onChange={(event: ChangeEvent<HTMLInputElement>) => onChange(mask(event.target.value))}
      inputClassName="tabular"
      {...props}
    />
  )
}

export interface ChoiceOption<T extends string> {
  value: T
  label: string
}

/** Tile look shared by single and multiple choice. */
function tile(selected: boolean, invalid: boolean) {
  return cn(
    'relative flex min-h-[52px] cursor-pointer items-center gap-3 rounded-xl border px-4 py-3 select-none',
    'text-body leading-snug transition-colors duration-150 ease-[var(--ease-ios)]',
    'has-[:focus-visible]:ring-4 has-[:focus-visible]:ring-[var(--accent)]/20',
    selected
      ? 'border-[var(--accent)] bg-[var(--accent-soft)] font-medium text-[var(--accent-on-soft)] ring-1 ring-[var(--accent)]'
      : invalid
        ? 'border-ios-red/60 bg-[var(--bg-elevated)]'
        : 'border-[var(--border-strong)] bg-[var(--bg-elevated)] hover:bg-[var(--bg-hover)] active:bg-[var(--bg-active)]',
  )
}

/** Question text above a group of tiles. */
function GroupLabel({ id, children }: { id: string; children: ReactNode }) {
  return (
    <p id={id} className="mb-2.5 text-body leading-snug font-medium text-[var(--fg)]">
      {children}
    </p>
  )
}

/**
 * Single choice as large tiles — the screen equivalent of the paper's ☐ boxes.
 * Radio inputs underneath, so a screen reader and a keyboard work as usual.
 *
 * `columns`: `2` — always two per row (short answers: «Ha / Yo'q»,
 * «Ko'p qavatli uy / Hovli»); `auto` — one per row on a phone, two from 640 px
 * (longer answers).
 */
export function ChoiceGroup<T extends string>({
  path,
  label,
  required = false,
  options,
  value,
  onChange,
  error,
  hint,
  columns = 'auto',
  className,
}: {
  path: string
  label?: ReactNode
  required?: boolean
  options: readonly ChoiceOption<T>[]
  value: T | null
  onChange: (value: T) => void
  error?: string | undefined
  hint?: ReactNode
  columns?: 1 | 2 | 'auto'
  className?: string
}) {
  const name = useId()
  const labelId = `${name}-label`

  return (
    <div data-field={path} className={cn('min-w-0', className)}>
      {label !== undefined && (
        <GroupLabel id={labelId}>
          <Label required={required}>{label}</Label>
        </GroupLabel>
      )}

      <div
        role="radiogroup"
        aria-labelledby={label === undefined ? undefined : labelId}
        aria-invalid={error ? true : undefined}
        className={cn(
          'grid gap-2.5',
          columns === 2 ? 'grid-cols-2' : columns === 1 ? 'grid-cols-1' : 'grid-cols-1 sm:grid-cols-2',
        )}
      >
        {options.map((option) => {
          const selected = option.value === value

          return (
            <label key={option.value} className={tile(selected, Boolean(error))}>
              <input
                type="radio"
                name={name}
                value={option.value}
                checked={selected}
                onChange={() => onChange(option.value)}
                className="sr-only"
              />
              <span
                aria-hidden
                className={cn(
                  'grid size-[22px] shrink-0 place-items-center rounded-full border-2 transition-colors',
                  selected ? 'border-[var(--accent)] bg-[var(--accent)]' : 'border-[var(--border-strong)]',
                )}
              >
                {selected && <Check className="size-3 text-[var(--fg-on-accent)]" strokeWidth={4} />}
              </span>
              <span className="min-w-0">{option.label}</span>
            </label>
          )
        })}
      </div>

      <FieldMessage error={error} hint={hint} className="mt-2" />
    </div>
  )
}

/** «Ha» / «Yo'q». `null` until answered — a required question is not pre-filled. */
export function YesNo({
  path,
  label,
  required = false,
  value,
  onChange,
  error,
  hint,
  className,
}: {
  path: string
  label: ReactNode
  required?: boolean
  value: boolean | null
  onChange: (value: boolean) => void
  error?: string | undefined
  hint?: ReactNode
  className?: string
}) {
  const { t } = useLocale()

  return (
    <ChoiceGroup
      path={path}
      label={label}
      required={required}
      hint={hint}
      error={error}
      className={className}
      columns={2}
      value={value === null ? null : value ? 'yes' : 'no'}
      onChange={(next) => onChange(next === 'yes')}
      options={[
        { value: 'yes', label: t('kiosk.yes') },
        { value: 'no', label: t('kiosk.no') },
      ]}
    />
  )
}

/** Several choices (driver licence categories). */
export function MultiChoice({
  path,
  label,
  required = false,
  options,
  value,
  onChange,
  error,
}: {
  path: string
  label: ReactNode
  required?: boolean
  options: readonly string[]
  value: readonly string[]
  onChange: (value: string[]) => void
  error?: string | undefined
}) {
  const labelId = useId()

  const toggle = (option: string) =>
    onChange(
      value.includes(option)
        ? value.filter((item) => item !== option)
        : options.filter((item) => item === option || value.includes(item)),
    )

  return (
    <div data-field={path}>
      <GroupLabel id={labelId}>
        <Label required={required}>{label}</Label>
      </GroupLabel>
      <div role="group" aria-labelledby={labelId} className="grid grid-cols-4 gap-2.5 sm:grid-cols-7">
        {options.map((option) => {
          const selected = value.includes(option)
          return (
            <button
              key={option}
              type="button"
              aria-pressed={selected}
              onClick={() => toggle(option)}
              className={cn(
                'h-[52px] rounded-xl border text-body font-semibold tabular transition-colors',
                selected
                  ? 'border-[var(--accent)] bg-[var(--accent)] text-[var(--fg-on-accent)]'
                  : error
                    ? 'border-ios-red/60 bg-[var(--bg-elevated)]'
                    : 'border-[var(--border-strong)] bg-[var(--bg-elevated)] active:bg-[var(--bg-active)]',
              )}
            >
              {option}
            </button>
          )
        })}
      </div>
      <FieldMessage error={error} className="mt-2" />
    </div>
  )
}

/** A tick on its own row («Hozir ham shu yerda ishlayman»), the whole row is the target. */
export function CheckRow({
  checked,
  onChange,
  label,
}: {
  checked: boolean
  onChange: (checked: boolean) => void
  label: ReactNode
}) {
  return (
    <label
      className={cn(
        'flex min-h-[52px] cursor-pointer items-center gap-3 rounded-xl border px-4 py-3 select-none',
        'has-[:focus-visible]:ring-4 has-[:focus-visible]:ring-[var(--accent)]/20',
        checked
          ? 'border-[var(--accent)] bg-[var(--accent-soft)]'
          : 'border-[var(--border-strong)] bg-[var(--bg-elevated)] active:bg-[var(--bg-active)]',
      )}
    >
      <input
        type="checkbox"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
        className="sr-only"
      />
      <span
        aria-hidden
        className={cn(
          'grid size-[22px] shrink-0 place-items-center rounded-md border-2 transition-colors',
          checked ? 'border-[var(--accent)] bg-[var(--accent)]' : 'border-[var(--border-strong)]',
        )}
      >
        {checked && <Check className="size-3.5 text-[var(--fg-on-accent)]" strokeWidth={3.5} />}
      </span>
      <span className="text-body leading-snug">{label}</span>
    </label>
  )
}

/**
 * Level buttons in one equal-width row: 0–5 for languages, 1–4 for computer
 * skills. The numbers are the paper's scale; the legend on the screen
 * explains them. Tapping the chosen level again clears it.
 */
export function LevelPicker({
  levels,
  value,
  onChange,
  label,
  invalid = false,
}: {
  levels: readonly number[]
  value: number | null
  onChange: (value: number | null) => void
  label: string
  invalid?: boolean
}) {
  return (
    <div
      role="radiogroup"
      aria-label={label}
      className={cn('grid gap-1.5', levels.length === 4 ? 'grid-cols-4' : 'grid-cols-6')}
    >
      {levels.map((level) => (
        <button
          key={level}
          type="button"
          role="radio"
          aria-checked={value === level}
          onClick={() => onChange(value === level ? null : level)}
          className={cn(
            'grid h-12 min-w-0 place-items-center rounded-xl border text-body font-semibold tabular transition-colors',
            value === level
              ? 'border-[var(--accent)] bg-[var(--accent)] text-[var(--fg-on-accent)]'
              : invalid
                ? 'border-ios-red/60 bg-[var(--bg-elevated)]'
                : 'border-[var(--border-strong)] bg-[var(--bg-elevated)] active:bg-[var(--bg-active)]',
          )}
        >
          {level}
        </button>
      ))}
    </div>
  )
}

export function FieldMessage({
  error,
  hint,
  className,
}: {
  error?: string | undefined
  hint?: ReactNode
  className?: string
}) {
  if (error) {
    return (
      <p role="alert" className={cn('text-subhead text-ios-red', className)}>
        {error}
      </p>
    )
  }

  return hint === undefined ? null : (
    <p className={cn('text-subhead text-[var(--fg-tertiary)]', className)}>{hint}</p>
  )
}
