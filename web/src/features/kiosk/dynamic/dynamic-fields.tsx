import { Check, Eraser } from 'lucide-react'
import { useId, useState, type ChangeEvent, type ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { formatUzPhone } from '@/shared/lib/phone'
import { Button, DateField, MoneyInput, PhoneInput, Select } from '@/shared/ui'
import type { KioskLanguage, KioskSession } from '../api/kiosk-api'
import {
  CheckRow,
  ChoiceGroup,
  DigitsField,
  FieldMessage,
  KioskField,
  KioskText,
  KioskTextArea,
  Label,
  MonthField,
  ScreenHint,
} from '../components/kiosk-fields'
import { AddRowButton, PickerSheet, RowSheet, SummaryRow } from '../components/kiosk-rows'
import { PhotoCapture } from '../components/photo-capture'
import { autofill } from '../components/screens/screen-props'
import { SignaturePad } from '../components/signature-pad'
import type { FieldErrors } from '../lib/kiosk-draft'
import { openSheet, scrollToFirstError } from '../lib/scroll-to-error'
import { BIND, textIn, type FormField, type KioskDefinition } from './definition'
import {
  LIMIT,
  errorsUnder,
  flagOf,
  grouped,
  levelsOf,
  listOf,
  newId,
  periodOf,
  rangeOf,
  referralOf,
  rowErrors,
  rowsOf,
  signatureNameOf,
  textOf,
  type DynamicDraft,
  type EvalContext,
  type LevelsDraft,
  type RepeatRow,
} from './dynamic-draft'

/*
 * One renderer per field kind (SPEC-anketa-konstruktori §7). Every control is
 * the built-in form's own (`KioskText`, `ChoiceGroup`, `SummaryRow` …), so the
 * constructor's form looks and behaves exactly like the form candidates know;
 * only the words and the rules come from the definition.
 */

/** Path → translated error (or `undefined`). */
export type ErrorOf = (path: string) => string | undefined

/** What every renderer needs besides its own value. */
export interface FieldEnv {
  definition: KioskDefinition
  draft: DynamicDraft
  evalContext: EvalContext
  session: KioskSession
  language: KioskLanguage
  today: string
  tablet: boolean
  /** The screen's title — a table or level list whose label repeats it shows no second heading. */
  screenTitle: string
  /** «Familiya Ism Otasining ismi» for the consents' `{{fullName}}`. */
  fullName: string
  photo: Blob | null
  onPhoto: (photo: Blob | null) => void
  hasSignature: boolean
  onSignature: (signature: Blob | null) => void
  /** The errors shown on this screen — a table line turns red when anything under it is wrong. */
  shown: FieldErrors
}

export interface InputProps {
  field: FormField
  /** Validation path — `jobs[1].period` inside a table. */
  path: string
  value: unknown
  onChange: (value: unknown) => void
  error: ErrorOf
  env: FieldEnv
}

/** Up to this many choices a question is tiles; more — a picker (the built-in form's rule). */
const TILES_MAX = 12
/** Short labels sit two per row. */
const SHORT_LABEL = 16

/** `MoneyInput` keeps a decimal part; the form takes whole so'm. */
const wholeSom = (digits: string) => digits.split('.')[0] ?? ''

/** Browser autofill for the bound personal fields (off on the shared tablet). */
const AUTOFILL: Readonly<Record<string, string>> = {
  [BIND.lastName]: 'family-name',
  [BIND.firstName]: 'given-name',
  [BIND.middleName]: 'additional-name',
  'candidate.address': 'street-address',
}

function useTexts(env: FieldEnv) {
  return (value: Parameters<typeof textIn>[0]) => textIn(value, env.language)
}

/** A top-level field: its draft value, the «same as» tick and the renderer. */
export function FieldView({
  field,
  env,
  error,
  setValue,
  setSame,
}: {
  field: FormField
  env: FieldEnv
  error: ErrorOf
  setValue: (id: string, value: unknown) => void
  setSame: (id: string, same: boolean) => void
}) {
  const text = useTexts(env)
  const value = env.draft.values[field.id]

  if (field.sameAs !== null) {
    const same = env.draft.same[field.id] === true

    return (
      <div className="space-y-3">
        <CheckRow checked={same} onChange={(checked) => setSame(field.id, checked)} label={text(field.sameAs.label)} />
        {same ? (
          <FieldMessage error={error(field.id)} />
        ) : (
          <FieldInput field={field} path={field.id} value={value} onChange={(next) => setValue(field.id, next)} error={error} env={env} />
        )}
      </div>
    )
  }

  return <FieldInput field={field} path={field.id} value={value} onChange={(next) => setValue(field.id, next)} error={error} env={env} />
}

/** The control for one field (or one table column) — controlled by `value` / `onChange`. */
export function FieldInput(props: InputProps): ReactNode {
  const { field, path, value, onChange, error, env } = props
  const { t } = useLocale()
  const text = useTexts(env)

  const label = text(field.label)
  const hint = field.hint === null ? undefined : text(field.hint)
  const limit = field.maxLength ?? (field.kind === 'long_text' ? LIMIT.long : LIMIT.short)

  switch (field.kind) {
    case 'short_text':
      return (
        <KioskText
          path={path}
          label={label}
          required={field.required}
          hint={hint}
          value={textOf(value)}
          maxLength={limit}
          autoComplete={field.bind !== null && AUTOFILL[field.bind] ? autofill(env.tablet, AUTOFILL[field.bind] ?? 'off') : 'off'}
          onChange={(event: ChangeEvent<HTMLInputElement>) => onChange(event.target.value)}
          error={error(path)}
        />
      )

    case 'long_text':
      return (
        <div className="space-y-1.5">
          <KioskTextArea
            path={path}
            label={label}
            required={field.required}
            value={textOf(value)}
            maxLength={limit}
            onChange={onChange}
            error={error(path)}
          />
          {hint !== undefined && error(path) === undefined && <FieldMessage hint={hint} />}
        </div>
      )

    case 'number': {
      const negative = (field.min ?? -1) < 0
      return (
        <KioskText
          path={path}
          label={label}
          required={field.required}
          hint={hint}
          inputMode="numeric"
          autoComplete="off"
          value={textOf(value)}
          maxLength={11}
          inputClassName="tabular"
          onChange={(event: ChangeEvent<HTMLInputElement>) => {
            const raw = event.target.value
            const digits = raw.replaceAll(/\D/gu, '').slice(0, 10)
            onChange(negative && raw.trimStart().startsWith('-') ? `-${digits}` : digits)
          }}
          error={error(path)}
        />
      )
    }

    case 'money':
      return (
        <KioskField path={path}>
          <MoneyInput
            size="lg"
            label={<Label required={field.required}>{label}</Label>}
            hint={hint}
            value={textOf(value)}
            onChange={(digits) => onChange(wholeSom(digits))}
            error={error(path)}
          />
        </KioskField>
      )

    case 'date': {
      const pastOnly = field.bind === BIND.birthDate || field.minAge !== null || field.maxAge !== null
      return (
        <KioskField path={path}>
          <DateField
            size="lg"
            label={<Label required={field.required}>{label}</Label>}
            hint={hint}
            value={textOf(value)}
            {...(pastOnly ? { max: env.today } : {})}
            {...(field.notBeforeToday ? { min: env.today } : {})}
            onChange={onChange}
            error={error(path)}
          />
        </KioskField>
      )
    }

    case 'month':
      return (
        <MonthField path={path} label={label} required={field.required} hint={hint} value={textOf(value)} onChange={onChange} error={error(path)} />
      )

    case 'year':
      return (
        <DigitsField
          path={path}
          label={label}
          required={field.required}
          hint={hint}
          placeholder="yyyy"
          value={textOf(value)}
          maxLength={4}
          onChange={onChange}
          error={error(path)}
        />
      )

    case 'phone':
      return field.mobileOnly || field.bind === BIND.phone ? (
        <KioskField path={path}>
          <PhoneInput
            size="lg"
            label={<Label required={field.required}>{label}</Label>}
            hint={hint}
            value={textOf(value)}
            onChange={onChange}
            error={error(path)}
          />
        </KioskField>
      ) : (
        <KioskText
          path={path}
          label={label}
          required={field.required}
          hint={hint}
          type="tel"
          inputMode="tel"
          autoComplete="off"
          placeholder="+998 90 123 45 67"
          value={textOf(value)}
          maxLength={LIMIT.loosePhone}
          onChange={(event: ChangeEvent<HTMLInputElement>) => onChange(event.target.value)}
          error={error(path)}
        />
      )

    case 'email':
      return (
        <KioskText
          path={path}
          label={label}
          required={field.required}
          hint={hint}
          type="email"
          inputMode="email"
          autoCapitalize="none"
          autoComplete={autofill(env.tablet, 'email')}
          value={textOf(value)}
          maxLength={LIMIT.short}
          onChange={(event: ChangeEvent<HTMLInputElement>) => onChange(event.target.value)}
          error={error(path)}
        />
      )

    case 'telegram':
      return (
        <KioskText
          path={path}
          label={label}
          required={field.required}
          hint={hint}
          leading="@"
          placeholder="username"
          autoCapitalize="none"
          autoComplete="off"
          value={textOf(value)}
          maxLength={33}
          onChange={(event: ChangeEvent<HTMLInputElement>) => onChange(event.target.value.replace(/^@/u, ''))}
          error={error(path)}
        />
      )

    case 'yes_no': {
      const flag = flagOf(value)
      return (
        <ChoiceGroup
          path={path}
          label={label}
          required={field.required}
          hint={hint}
          columns={2}
          value={flag === null ? null : flag ? 'yes' : 'no'}
          onChange={(next) => onChange(next === 'yes')}
          error={error(path)}
          options={[
            { value: 'yes', label: field.yes === null ? t('kiosk.yes') : text(field.yes) },
            { value: 'no', label: field.no === null ? t('kiosk.no') : text(field.no) },
          ]}
        />
      )
    }

    case 'choice':
      return <ChoiceInput {...props} />

    case 'multi_choice':
      return <MultiInput {...props} />

    case 'levels':
      return <LevelsInput {...props} />

    case 'repeat':
      return <RepeatInput {...props} />

    case 'period':
      return <PeriodInput {...props} />

    case 'year_range':
      return <YearRangeInput {...props} />

    case 'consent':
      return <ConsentInput {...props} />

    case 'info':
      return <InfoBlock text={text(field.text ?? field.label)} />

    case 'vacancy':
      return <VacancyInput {...props} />

    case 'photo':
      return <PhotoCapture photo={env.photo} onChange={env.onPhoto} error={error(path)} />

    case 'signature':
      return <SignatureInput {...props} />

    case 'referral':
      return <ReferralInput {...props} />

    case 'checklist':
      return null
  }
}

/* ─────────────────────────────────────────────────────────── choices */

function ChoiceInput({ field, path, value, onChange, error, env }: InputProps) {
  const { t } = useLocale()
  const text = useTexts(env)

  const chosen = textOf(value)
  const label = text(field.label)
  const hint = field.hint === null ? undefined : text(field.hint)
  const options = field.options.map((option) => ({ value: option.value, label: text(option.label) }))
  const current = options.some((option) => option.value === chosen) ? chosen : null

  if (options.length > TILES_MAX) {
    return (
      <KioskField path={path}>
        <Select
          label={<Label required={field.required}>{label}</Label>}
          hint={hint}
          placeholder={t('kiosk.choose')}
          options={options}
          value={current ?? ''}
          onChange={(event) => onChange(event.target.value)}
          error={error(path)}
        />
      </KioskField>
    )
  }

  const short = options.every((option) => option.label.length <= SHORT_LABEL)
  const columns = field.style === 'list' ? 1 : field.style === 'tiles' && short ? 2 : 'auto'

  if (field.groups.length === 0) {
    return (
      <ChoiceGroup
        path={path}
        label={label}
        required={field.required}
        hint={hint}
        columns={columns}
        value={current}
        onChange={onChange}
        error={error(path)}
        options={options}
      />
    )
  }

  // Grouped options: one titled block of tiles per group, the ungrouped ones last
  const blocks = [
    ...field.groups.map((group) => ({ id: group.id, title: text(group.label) })),
    { id: '', title: '' },
  ]
    .map((block) => ({
      ...block,
      options: options.filter((_, at) => (field.options[at]?.group ?? '') === block.id),
    }))
    .filter((block) => block.options.length > 0)

  return (
    <div data-field={path} className="space-y-4">
      <p className="text-body leading-snug font-medium">
        <Label required={field.required}>{label}</Label>
      </p>
      {blocks.map((block) => (
        <ChoiceGroup
          key={block.id}
          path={`${path}@${block.id}`}
          label={block.title === '' ? undefined : <span className="text-subhead text-[var(--fg-secondary)]">{block.title}</span>}
          columns={columns}
          value={current}
          onChange={onChange}
          options={block.options}
        />
      ))}
      <FieldMessage error={error(path)} hint={hint} />
    </div>
  )
}

/** Several answers: short codes («B», «C») as a compact grid, longer answers as ticked rows. */
function MultiInput({ field, path, value, onChange, error, env }: InputProps) {
  const text = useTexts(env)
  const labelId = useId()
  const chosen = listOf(value)
  const options = field.options.map((option) => ({ value: option.value, label: text(option.label) }))
  const compact = options.every((option) => option.label.length <= 4)

  const toggle = (option: string) =>
    onChange(
      chosen.includes(option)
        ? chosen.filter((item) => item !== option)
        : field.options.filter((item) => item.value === option || chosen.includes(item.value)).map((item) => item.value),
    )

  const invalid = error(path) !== undefined

  return (
    <div data-field={path}>
      <p id={labelId} className="mb-2.5 text-body leading-snug font-medium text-[var(--fg)]">
        <Label required={field.required}>{text(field.label)}</Label>
      </p>

      {compact ? (
        <div role="group" aria-labelledby={labelId} className="grid grid-cols-4 gap-2.5 sm:grid-cols-7">
          {options.map((option) => {
            const selected = chosen.includes(option.value)
            return (
              <button
                key={option.value}
                type="button"
                aria-pressed={selected}
                onClick={() => toggle(option.value)}
                className={cn(
                  'h-[52px] rounded-xl border text-body font-semibold tabular transition-colors',
                  selected
                    ? 'border-[var(--accent)] bg-[var(--accent)] text-[var(--fg-on-accent)]'
                    : invalid
                      ? 'border-ios-red/60 bg-[var(--bg-elevated)]'
                      : 'border-[var(--border-strong)] bg-[var(--bg-elevated)] active:bg-[var(--bg-active)]',
                )}
              >
                {option.label}
              </button>
            )
          })}
        </div>
      ) : (
        <div role="group" aria-labelledby={labelId} className="space-y-2.5">
          {options.map((option) => (
            <CheckRow
              key={option.value}
              checked={chosen.includes(option.value)}
              onChange={() => toggle(option.value)}
              label={option.label}
            />
          ))}
        </div>
      )}

      <FieldMessage error={error(path)} hint={field.hint === null ? undefined : text(field.hint)} className="mt-2" />
    </div>
  )
}

/* ─────────────────────────────────────────────────────────── levels */

/** Level buttons of any scale (0–5, 1–4, 1–10) in equal columns; the chosen one again clears it. */
function ScalePicker({
  min,
  max,
  value,
  onChange,
  label,
  invalid = false,
}: {
  min: number
  max: number
  value: number | null
  onChange: (value: number | null) => void
  label: string
  invalid?: boolean
}) {
  const levels = Array.from({ length: Math.max(0, max - min + 1) }, (_, at) => min + at)

  return (
    <div
      role="radiogroup"
      aria-label={label}
      className="grid gap-1.5"
      style={{ gridTemplateColumns: `repeat(${Math.min(levels.length, 6)}, minmax(0, 1fr))` }}
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

/**
 * A level table (languages, computer skills). One aspect — every row inline
 * with its picker, grouped like the paper (the computer list); several
 * aspects — one line per row and the aspects in a sheet (the languages).
 */
function LevelsInput({ field, path, value, onChange, error, env }: InputProps) {
  const { t } = useLocale()
  const text = useTexts(env)
  const aspects = field.aspects.length
  const levels = levelsOf(value, aspects)
  const scale = field.scale ?? { min: 0, max: 0 }
  const label = text(field.label)
  const hint = field.hint === null ? null : text(field.hint)

  const write = (next: LevelsDraft) => onChange(next)
  const setRow = (option: string, row: (number | null)[]) => {
    const rows = { ...levels.rows }
    if (row.every((level) => level === null)) delete rows[option]
    else rows[option] = row
    write({ ...levels, rows })
  }

  const heading = label !== '' && label !== env.screenTitle && (
    <p className="text-body leading-snug font-medium">
      <Label required={field.required}>{label}</Label>
    </p>
  )

  if (aspects === 1) {
    const blocks = [
      ...field.groups.map((group) => ({ id: group.id, title: text(group.label) })),
      { id: '', title: '' },
    ]
      .map((block) => ({ ...block, options: field.options.filter((option) => (option.group ?? '') === block.id) }))
      .filter((block) => block.options.length > 0)

    return (
      <div data-field={path} className="space-y-5">
        {heading}
        {hint !== null && <ScreenHint>{hint}</ScreenHint>}

        {blocks.map((block) => (
          <section key={block.id} className="space-y-2.5">
            {block.title !== '' && <h2 className="text-body font-medium">{block.title}</h2>}
            <ul className="divide-y divide-[var(--border)] rounded-xl border border-[var(--border-strong)] bg-[var(--bg-elevated)]">
              {block.options.map((option) => {
                const name = text(option.label)
                const rowPath = `${path}.rows.${option.value}`
                return (
                  <li
                    key={option.value}
                    data-field={rowPath}
                    className="flex min-h-14 items-center gap-2 py-1 pr-1 pl-3.5 sm:gap-4 sm:py-2 sm:pr-2 sm:pl-4"
                  >
                    <span className="min-w-0 flex-1 text-body leading-snug">
                      {name.replaceAll(' / ', ' / ')}
                      {field.requiredRows.includes(option.value) && (
                        <span aria-hidden className="text-ios-red">
                          {' *'}
                        </span>
                      )}
                    </span>
                    <div className="w-[188px] shrink-0 sm:w-64">
                      <ScalePicker
                        min={scale.min}
                        max={scale.max}
                        label={name}
                        value={levels.rows[option.value]?.[0] ?? null}
                        invalid={error(rowPath) !== undefined}
                        onChange={(level) => setRow(option.value, [level])}
                      />
                    </div>
                  </li>
                )
              })}
            </ul>
          </section>
        ))}

        <FieldMessage error={error(path) ?? field.requiredRows.map((row) => error(`${path}.rows.${row}`)).find(Boolean)} />
      </div>
    )
  }

  return <AspectLevels field={field} path={path} levels={levels} write={write} setRow={setRow} heading={heading} hint={hint} error={error} env={env} t={t} />
}

/** Several aspects: a line per row, the aspects in a sheet; «other» rows with their own name. */
function AspectLevels({
  field,
  path,
  levels,
  write,
  setRow,
  heading,
  hint,
  error,
  env,
  t,
}: {
  field: FormField
  path: string
  levels: LevelsDraft
  write: (next: LevelsDraft) => void
  setRow: (option: string, row: (number | null)[]) => void
  heading: ReactNode
  hint: string | null
  error: ErrorOf
  env: FieldEnv
  t: ReturnType<typeof useLocale>['t']
}) {
  const text = useTexts(env)
  const blank = () => field.aspects.map(() => null as number | null)
  /** `rows.<value>` or `other.<index>`; kept while the sheet slides away. */
  const [shown, setShown] = useState<string | null>(null)
  const [isOpen, setIsOpen] = useState(false)

  const show = (key: string) => {
    setShown(key)
    setIsOpen(true)
  }

  const otherIndex = shown?.startsWith('other.') ? Number(shown.slice(6)) : null
  const option = shown?.startsWith('rows.') ? field.options.find((item) => item.value === shown.slice(5)) : undefined
  const otherRow = otherIndex === null ? undefined : levels.other[otherIndex]
  const current = option !== undefined ? (levels.rows[option.value] ?? blank()) : otherRow?.levels

  const setOther = (index: number, changes: Partial<LevelsDraft['other'][number]>) =>
    write({ ...levels, other: levels.other.map((row, at) => (at === index ? { ...row, ...changes } : row)) })

  const removeOther = (index: number) => write({ ...levels, other: levels.other.filter((_, at) => at !== index) })

  const close = () => {
    // An «other» row opened and left empty is not kept as a blank line
    if (otherIndex !== null && otherRow !== undefined && otherRow.name.trim() === '' && otherRow.levels.every((level) => level === null)) {
      removeOther(otherIndex)
      setShown(null)
    }
    setIsOpen(false)
  }

  const levelsLine = (row: (number | null)[]) =>
    field.aspects.map((aspect, at) => `${text(aspect.label)} ${row[at] ?? '—'}`).join(' · ')

  const sheetBase = option !== undefined ? `${path}.rows.${option.value}` : `${path}.other[${otherIndex ?? 0}]`
  const sheetTitle = option !== undefined ? text(option.label) : (otherRow?.name.trim() || (field.other === null ? '' : text(field.other.label)))

  return (
    <div data-field={path} className="space-y-4">
      {heading}
      {hint !== null && <ScreenHint>{hint}</ScreenHint>}

      <div className="space-y-2.5">
        {field.options.map((item) => {
          const row = levels.rows[item.value]
          const rowPath = `${path}.rows.${item.value}`
          const empty = row === undefined || row.every((level) => level === null)

          return (
            <SummaryRow
              key={item.value}
              path={rowPath}
              title={
                <>
                  {text(item.label)}
                  {field.requiredRows.includes(item.value) && (
                    <span aria-hidden className="text-ios-red">
                      {' *'}
                    </span>
                  )}
                </>
              }
              subtitle={empty ? t('kiosk.row.notSet') : levelsLine(row)}
              placeholder={empty}
              invalid={error(rowPath) !== undefined}
              invalidText={error(rowPath)}
              onOpen={() => show(`rows.${item.value}`)}
            />
          )
        })}

        {field.other !== null &&
          levels.other.map((row, index) => {
            const rowPath = `${path}.other[${index}]`
            const empty = row.levels.every((level) => level === null)
            return (
              <SummaryRow
                key={row.key}
                path={rowPath}
                title={row.name.trim() || text(field.other?.label)}
                subtitle={empty ? t('kiosk.row.notSet') : levelsLine(row.levels)}
                placeholder={empty}
                invalid={Object.keys(errorsUnder(env.shown, rowPath)).length > 0}
                invalidText={error(`${rowPath}.name`)}
                onOpen={() => show(`other.${index}`)}
                onRemove={() => removeOther(index)}
              />
            )
          })}

        {field.other !== null && levels.other.length < field.other.max && (
          <AddRowButton
            onClick={() => {
              write({ ...levels, other: [...levels.other, { key: newId(), name: '', levels: blank() }] })
              show(`other.${levels.other.length}`)
            }}
          >
            {t('kiosk.dyn.add', { label: text(field.other.label) })}
          </AddRowButton>
        )}

        <FieldMessage error={error(path)} />
      </div>

      <PickerSheet
        open={isOpen && current !== undefined}
        title={sheetTitle}
        onClose={close}
        secondary={
          current !== undefined && current.some((level) => level !== null) ? (
            <Button
              type="button"
              variant="ghost"
              size="lg"
              onClick={() => {
                if (option !== undefined) setRow(option.value, blank())
                else if (otherIndex !== null) setOther(otherIndex, { levels: blank() })
              }}
              className="mr-auto"
            >
              <Eraser />
              {t('kiosk.row.clear')}
            </Button>
          ) : undefined
        }
      >
        {current !== undefined && (
          <>
            {otherIndex !== null && otherRow !== undefined && (
              <KioskText
                path={`${sheetBase}.name`}
                label={t('kiosk.dyn.otherName')}
                value={otherRow.name}
                maxLength={LIMIT.otherName}
                onChange={(event: ChangeEvent<HTMLInputElement>) => setOther(otherIndex, { name: event.target.value })}
                error={error(`${sheetBase}.name`)}
              />
            )}

            {field.aspects.map((aspect, at) => (
              <div key={aspect.id} className="space-y-2">
                <p className="text-body font-medium">{text(aspect.label)}</p>
                <ScalePicker
                  min={field.scale?.min ?? 0}
                  max={field.scale?.max ?? 0}
                  label={text(aspect.label)}
                  value={current[at] ?? null}
                  invalid={error(sheetBase) !== undefined && current[at] === null}
                  onChange={(level) => {
                    const next = current.map((item, index) => (index === at ? level : item))
                    if (option !== undefined) setRow(option.value, next)
                    else if (otherIndex !== null) setOther(otherIndex, { levels: next })
                  }}
                />
              </div>
            ))}

            <FieldMessage error={error(sheetBase)} />
          </>
        )}
      </PickerSheet>
    </div>
  )
}

/* ─────────────────────────────────────────────────────────── tables */

/** A cell as it reads on the line of its row. */
function cellText(column: FormField, value: unknown, language: KioskLanguage, t: ReturnType<typeof useLocale>['t']): string {
  switch (column.kind) {
    case 'yes_no': {
      const flag = flagOf(value)
      if (flag === null) return ''
      const own = flag ? column.yes : column.no
      return own === null ? t(flag ? 'kiosk.yes' : 'kiosk.no') : textIn(own, language)
    }
    case 'choice': {
      const option = column.options.find((item) => item.value === textOf(value))
      return option === undefined ? '' : textIn(option.label, language)
    }
    case 'number':
    case 'money': {
      const digits = textOf(value).trim()
      return digits === '' || Number.isNaN(Number(digits)) ? digits : grouped(Number(digits))
    }
    case 'date': {
      const match = /^(\d{4})-(\d{2})-(\d{2})$/u.exec(textOf(value))
      return match === null ? '' : `${match[3]}.${match[2]}.${match[1]}`
    }
    case 'phone': {
      const raw = textOf(value).trim()
      return column.mobileOnly && /^\d{9}$/u.test(raw) ? `+998 ${formatUzPhone(raw)}` : raw
    }
    case 'period': {
      const period = periodOf(value)
      const to = period.current && column.allowCurrent ? t('kiosk.row.present') : period.to.trim()
      return period.from.trim() === '' && to === '' ? '' : `${period.from.trim() || '…'} – ${to || '…'}`
    }
    case 'year_range': {
      const range = rangeOf(value)
      return range.from.trim() === '' && range.to.trim() === '' ? '' : `${range.from.trim() || '…'} – ${range.to.trim() || '…'}`
    }
    default:
      return textOf(value).trim()
  }
}

/** The row's line: the `summary` template (`{organization} — {position}`), empty parts and their separators dropped. */
function rowTitle(field: FormField, row: RepeatRow, language: KioskLanguage, t: ReturnType<typeof useLocale>['t']): string {
  const template = field.summary ?? `{${field.columns[0]?.id ?? ''}}`
  const parts = template.split(/(\{[^{}]+\})/u).filter((part) => part !== '')

  const out: string[] = []
  let pendingSeparator = ''
  for (const part of parts) {
    const name = /^\{([^{}]+)\}$/u.exec(part)?.[1]
    if (name === undefined) {
      pendingSeparator += part
      continue
    }

    const column = field.columns.find((item) => item.id === name)
    const value = column === undefined ? '' : cellText(column, row.values[name], language, t)
    if (value === '') continue

    out.push(out.length > 0 ? pendingSeparator : pendingSeparator.trimStart().length > 0 && !/^[\s—–\-·,:;/|]+$/u.test(pendingSeparator) ? pendingSeparator : '')
    out.push(value)
    pendingSeparator = ''
  }

  return out.join('').trim()
}

/** The row's second line: the cells the title does not show, a few of them. */
function rowSubtitle(field: FormField, row: RepeatRow, language: KioskLanguage, t: ReturnType<typeof useLocale>['t']): string {
  const inTitle = new Set([...(field.summary ?? `{${field.columns[0]?.id ?? ''}}`).matchAll(/\{([^{}]+)\}/gu)].map((match) => match[1]))
  return field.columns
    .filter((column) => !inTitle.has(column.id))
    .map((column) => cellText(column, row.values[column.id], language, t))
    .filter(Boolean)
    .slice(0, 3)
    .join(' · ')
}

interface Editing {
  /** Position in the list; `rows.length` for a new row. */
  index: number
  row: RepeatRow
  attempted: boolean
  open: boolean
}

/**
 * A repeating table (education, jobs, family): one line per row, the row's
 * columns in a sheet. «Saqlash» checks the row with the form's own rules and
 * only then writes it into the draft (the built-in tables' behaviour).
 */
function RepeatInput({ field, path, value, onChange, error, env }: InputProps) {
  const { t } = useLocale()
  const text = useTexts(env)
  const rows = rowsOf(value)
  const [editing, setEditing] = useState<Editing | null>(null)

  const label = text(field.label)
  const hint = field.hint === null ? null : text(field.hint)
  const limit = field.maxRows ?? LIMIT.repeatRows

  const check = (index: number, row: RepeatRow) => rowErrors(env.definition, env.draft, env.evalContext, field, index, row)

  const sheetErrors: FieldErrors = editing === null || !editing.open || !editing.attempted ? {} : check(editing.index, editing.row)
  const sheetError = useSheetError(sheetErrors)

  const close = () => setEditing((current) => (current === null ? null : { ...current, open: false }))

  const save = () => {
    if (editing === null || !editing.open) return

    const found = check(editing.index, editing.row)
    if (Object.keys(found).length > 0) {
      setEditing({ ...editing, attempted: true })
      scrollToFirstError(found, openSheet() ?? document)
      return
    }

    const next = [...rows]
    next[editing.index] = editing.row
    onChange(next)
    close()
  }

  const heading = label !== '' && label !== env.screenTitle && (
    <p className="text-body leading-snug font-medium">
      <Label required={field.required}>{label}</Label>
    </p>
  )

  return (
    <div className="space-y-4">
      {heading}
      {hint !== null && <ScreenHint>{hint}</ScreenHint>}

      <div className="space-y-2.5">
        {rows.map((row, index) => {
          const rowPath = `${path}[${index}]`
          return (
            <SummaryRow
              key={row.key}
              path={rowPath}
              title={rowTitle(field, row, env.language, t) || t('kiosk.row.untitled')}
              subtitle={rowSubtitle(field, row, env.language, t) || undefined}
              invalid={Object.keys(errorsUnder(env.shown, rowPath)).length > 0}
              invalidText={t('kiosk.row.invalid')}
              onOpen={() => {
                const invalid = Object.keys(check(index, row)).length > 0
                setEditing({ index, row, attempted: invalid, open: true })
              }}
              onRemove={() => onChange(rows.filter((_, at) => at !== index))}
            />
          )
        })}

        {rows.length < limit && (
          <AddRowButton onClick={() => setEditing({ index: rows.length, row: { key: newId(), values: {} }, attempted: false, open: true })}>
            {t('kiosk.dyn.add', { label })}
          </AddRowButton>
        )}

        {/* After the lines, so «scroll to error» reaches a row's error before this one. */}
        <div data-field={path}>
          <FieldMessage error={error(path)} />
        </div>
      </div>

      <RowSheet open={editing?.open === true} title={label} onCancel={close} onSave={save}>
        {editing !== null &&
          field.columns.map((column) => (
            <FieldInput
              key={column.id}
              field={column}
              path={`${path}[${editing.index}].${column.id}`}
              value={editing.row.values[column.id]}
              onChange={(next) =>
                setEditing((current) =>
                  current === null ? null : { ...current, row: { ...current.row, values: { ...current.row.values, [column.id]: next } } },
                )
              }
              error={sheetError}
              env={env}
            />
          ))}
      </RowSheet>
    </div>
  )
}

/** Translated errors of the open sheet. */
function useSheetError(errors: FieldErrors): ErrorOf {
  const { t } = useLocale()
  return (path: string) => {
    const found = errors[path]
    return found === undefined ? undefined : t(`kiosk.error.${found.code}`, found.values)
  }
}

/** «Boshlagan (oy.yil)» – «Tugatgan (oy.yil)» and, when allowed, «Hozir ham shu yerda». */
function PeriodInput({ field, path, value, onChange, error, env }: InputProps) {
  const { t } = useLocale()
  const text = useTexts(env)
  const period = periodOf(value)
  const current = period.current && field.allowCurrent

  return (
    <div data-field={path} className="space-y-3">
      <p className="text-body leading-snug font-medium">
        <Label required={field.required}>{text(field.label)}</Label>
      </p>
      <div className="grid grid-cols-2 gap-4">
        <MonthField
          path={`${path}.from`}
          label={t('kiosk.field.jobFrom')}
          value={period.from}
          onChange={(from) => onChange({ ...period, from })}
          error={error(`${path}.from`)}
        />
        {!current && (
          <MonthField
            path={`${path}.to`}
            label={t('kiosk.field.jobTo')}
            value={period.to}
            onChange={(to) => onChange({ ...period, to })}
            error={error(`${path}.to`)}
          />
        )}
      </div>
      {field.allowCurrent && (
        <CheckRow
          checked={current}
          onChange={(checked) => onChange({ ...period, current: checked, to: checked ? '' : period.to })}
          label={t('kiosk.field.jobCurrent')}
        />
      )}
      <FieldMessage error={error(path)} hint={field.hint === null ? undefined : text(field.hint)} />
    </div>
  )
}

function YearRangeInput({ field, path, value, onChange, error, env }: InputProps) {
  const { t } = useLocale()
  const text = useTexts(env)
  const range = rangeOf(value)

  return (
    <div data-field={path} className="space-y-3">
      <p className="text-body leading-snug font-medium">
        <Label required={field.required}>{text(field.label)}</Label>
      </p>
      <div className="grid grid-cols-2 gap-4">
        <DigitsField
          path={`${path}.from`}
          label={t('kiosk.field.startYear')}
          placeholder="yyyy"
          value={range.from}
          maxLength={4}
          onChange={(from) => onChange({ ...range, from })}
          error={error(`${path}.from`)}
        />
        <DigitsField
          path={`${path}.to`}
          label={t('kiosk.field.endYear')}
          placeholder="yyyy"
          value={range.to}
          maxLength={4}
          onChange={(to) => onChange({ ...range, to })}
          error={error(`${path}.to`)}
        />
      </div>
      <FieldMessage error={error(path)} hint={field.hint === null ? undefined : text(field.hint)} />
    </div>
  )
}

/* ─────────────────────────────────────────────────────────── system parts */

/** A statement the candidate ticks on its own; `{{fullName}}` becomes their name. */
function ConsentInput({ field, path, value, onChange, error, env }: InputProps) {
  const text = useTexts(env)
  const checked = value === true
  const statement = text(field.text ?? field.label)
  const name = env.fullName.trim() === '' ? '____________' : env.fullName

  return (
    <div data-field={path} className="space-y-1.5">
      <label
        className={cn(
          'flex cursor-pointer items-start gap-3 rounded-xl border p-4 select-none',
          'transition-colors duration-150 has-[:focus-visible]:ring-4 has-[:focus-visible]:ring-[var(--accent)]/20',
          checked
            ? 'border-[var(--accent)] bg-[var(--accent-soft)]'
            : error(path)
              ? 'border-ios-red/60 bg-[var(--bg-elevated)]'
              : 'border-[var(--border-strong)] bg-[var(--bg-elevated)]',
        )}
      >
        <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} className="sr-only" />
        <span
          aria-hidden
          className={cn(
            'mt-0.5 grid size-6 shrink-0 place-items-center rounded-md border-2 transition-colors',
            checked ? 'border-[var(--accent)] bg-[var(--accent)]' : 'border-[var(--border-strong)]',
          )}
        >
          {checked && <Check className="size-4 text-[var(--fg-on-accent)]" strokeWidth={3.5} />}
        </span>
        <span className="space-y-2 text-callout leading-snug">
          {statement.split('\n').map((paragraph, at) => (
            <span key={at} className="block">
              {paragraph.split('{{fullName}}').map((piece, index) => (
                <span key={index}>
                  {index > 0 && <strong className="font-semibold">{name}</strong>}
                  {piece}
                </span>
              ))}
            </span>
          ))}
        </span>
      </label>
      <FieldMessage error={error(path)} />
    </div>
  )
}

function InfoBlock({ text }: { text: string }) {
  if (text.trim() === '') return null
  return (
    <div className="space-y-2 rounded-xl bg-[var(--bg-sunken)] p-4 text-callout leading-relaxed text-[var(--fg-secondary)]">
      {text.split('\n').map((paragraph, at) => (
        <p key={at}>{paragraph}</p>
      ))}
    </div>
  )
}

/** The vacancy — tiles up to six, a picker beyond; «Umumiy» only while the general pool takes forms. */
function VacancyInput({ field, path, value, onChange, error, env }: InputProps) {
  const { t } = useLocale()
  const text = useTexts(env)
  const { session } = env
  const label = text(field.label) || t('kiosk.vacancy.label')

  const options = [
    ...(session.generalPoolAvailable ? [{ value: 'general', label: t('kiosk.vacancy.general') }] : []),
    ...session.vacancies.map((vacancy) => ({ value: vacancy.id, label: vacancy.title })),
  ]
  const chosen = textOf(value)
  const current = options.some((option) => option.value === chosen) ? chosen : null
  // The hint speaks of «Umumiy» — shown only while that choice is offered (the built-in rule)
  const hint = session.generalPoolAvailable && field.hint !== null ? text(field.hint) : null

  return (
    <>
      {hint !== null && <ScreenHint>{hint}</ScreenHint>}
      {options.length <= 6 ? (
        <ChoiceGroup
          path={path}
          label={label}
          required
          columns={1}
          value={current}
          onChange={onChange}
          error={error(path)}
          options={options}
        />
      ) : (
        <KioskField path={path}>
          <Select
            label={<Label required>{label}</Label>}
            placeholder={t('kiosk.vacancy.placeholder')}
            options={options}
            value={current ?? ''}
            onChange={(event) => onChange(event.target.value)}
            error={error(path)}
          />
        </KioskField>
      )}
    </>
  )
}

/** Typed full name, the finger signature and today's date. */
function SignatureInput({ field, path, value, onChange, error, env }: InputProps) {
  const { t, date } = useLocale()
  const text = useTexts(env)

  return (
    <>
      <KioskText
        path={`${path}.name`}
        label={text(field.label) || t('kiosk.field.signatureName')}
        required={field.required}
        hint={field.hint === null ? undefined : text(field.hint)}
        value={signatureNameOf(value)}
        maxLength={LIMIT.signatureName}
        autoComplete="off"
        onChange={(event: ChangeEvent<HTMLInputElement>) => onChange({ name: event.target.value })}
        error={error(`${path}.name`)}
      />

      <div data-field={`${path}.pad`} className="space-y-2.5">
        <p className="text-body font-medium">
          <Label required={env.evalContext.requireFiles}>{t('kiosk.signature.label')}</Label>
        </p>
        <SignaturePad hasSignature={env.hasSignature} onChange={env.onSignature} error={error(`${path}.pad`)} />
      </div>

      <p className="text-callout text-[var(--fg-secondary)]">{t('kiosk.consent.date', { date: date(env.today) })}</p>
    </>
  )
}

/** «Qayerdan bildingiz?» — HR's active channels; a channel that asks for detail asks «Aniqroq yozing». */
function ReferralInput({ field, path, value, onChange, error, env }: InputProps) {
  const { t } = useLocale()
  const text = useTexts(env)
  const referral = referralOf(value)
  const channels = env.session.referralChannels
  const options = channels.map((channel) => ({ value: channel.id, label: channel.name }))
  const chosen = channels.find((channel) => channel.id === referral.channel)
  const label = text(field.label) || t('kiosk.field.referralSource')
  const channelPath = `${path}.channel`

  return (
    <>
      {options.length <= TILES_MAX ? (
        <ChoiceGroup
          path={channelPath}
          label={label}
          required={field.required}
          hint={field.hint === null ? undefined : text(field.hint)}
          columns="auto"
          value={chosen === undefined ? null : chosen.id}
          onChange={(channel) => onChange({ ...referral, channel })}
          error={error(channelPath)}
          options={options}
        />
      ) : (
        <KioskField path={channelPath}>
          <Select
            label={<Label required={field.required}>{label}</Label>}
            placeholder={t('kiosk.field.referralPlaceholder')}
            options={options}
            value={chosen === undefined ? '' : chosen.id}
            onChange={(event) => onChange({ ...referral, channel: event.target.value })}
            error={error(channelPath)}
          />
        </KioskField>
      )}
      {chosen?.asksDetail === true && (
        <KioskText
          path={`${path}.detail`}
          label={t('kiosk.field.referralDetail')}
          placeholder={t('kiosk.field.referralDetailPlaceholder')}
          required
          value={referral.detail}
          maxLength={LIMIT.referralDetail}
          autoComplete="off"
          onChange={(event: ChangeEvent<HTMLInputElement>) => onChange({ ...referral, detail: event.target.value })}
          error={error(`${path}.detail`)}
        />
      )}
    </>
  )
}
