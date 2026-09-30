import clsx from 'clsx'
import { useState, type ChangeEvent, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

import { useFormat } from '@/lib/format'
import { Button } from './button'
import { Icon } from './icon'
import { Sheet } from './sheet'

/**
 * Forma boshqaruvlari (UX §E.5).
 *
 * ⚠️ Har bir maydon kamida 16 px: pastroq bo'lsa iOS Safari fokusda
 * sahifani kattalashtiradi va qaytarmaydi. Global CSS shuni
 * kafolatlaydi — bu yerda `text-callout` ataylab takrorlangan.
 *
 * Xato xabari HAR DOIM maydon OSTIDA, yuqorida umumiy ro'yxat yo'q:
 * bu ilovadagi formalar uch maydondan oshmaydi.
 */
interface FieldProps {
  label: string
  required?: boolean
  helper?: string
  error?: string | null
  children: ReactNode
}

export function Field({ label, required = false, helper, error, children }: FieldProps) {
  return (
    <div>
      <label className="mb-1.5 block text-footnote font-medium text-fg-secondary">
        {label}
        {required ? <span className="text-danger-text"> *</span> : null}
      </label>
      {children}
      {error ? (
        <p className="mt-1.5 text-footnote text-danger-text server-text">{error}</p>
      ) : helper ? (
        <p className="mt-1.5 text-footnote text-fg-tertiary">{helper}</p>
      ) : null}
    </div>
  )
}

interface TextInputProps {
  value: string
  onChange: (value: string) => void
  onBlur?: () => void
  type?: 'text' | 'password' | 'tel'
  placeholder?: string
  invalid?: boolean
  autoFocus?: boolean
  inputMode?: 'text' | 'numeric' | 'tel'
  autoComplete?: string
}

export function TextInput({
  value,
  onChange,
  onBlur,
  type = 'text',
  placeholder,
  invalid = false,
  autoFocus = false,
  inputMode,
  autoComplete,
}: TextInputProps) {
  return (
    <input
      type={type}
      value={value}
      placeholder={placeholder}
      inputMode={inputMode}
      autoComplete={autoComplete}
      // eslint-disable-next-line jsx-a11y/no-autofocus -- faqat majburiy
      // parol ekranida ishlatiladi (UX §H.2): boshqa joyda klaviatura
      // kontentni yopib qo'yardi.
      autoFocus={autoFocus}
      onChange={(event: ChangeEvent<HTMLInputElement>) => onChange(event.target.value)}
      onBlur={onBlur}
      className={clsx(
        'h-12 w-full rounded-control border bg-bg-elevated px-3 text-callout text-fg',
        'placeholder:text-fg-tertiary',
        invalid ? 'border-danger' : 'border-border-strong',
      )}
    />
  )
}

export function TextArea({
  value,
  onChange,
  placeholder,
  rows = 3,
}: {
  value: string
  onChange: (value: string) => void
  placeholder?: string
  rows?: number
}) {
  return (
    <textarea
      value={value}
      rows={rows}
      placeholder={placeholder}
      onChange={(event: ChangeEvent<HTMLTextAreaElement>) => onChange(event.target.value)}
      className="w-full rounded-control border border-border-strong bg-bg-elevated px-3 py-2.5 text-callout text-fg placeholder:text-fg-tertiary"
    />
  )
}

/** Varaqdagi ro'yxat ochadigan qator — hech qachon xom `<select>`. */
interface SelectOption {
  id: string
  name: string
}

export function SelectRow({
  sheetTitle,
  value,
  options,
  placeholder,
  invalid = false,
  onChange,
}: {
  sheetTitle: string
  value: string | null
  options: readonly SelectOption[]
  placeholder: string
  invalid?: boolean
  onChange: (id: string) => void
}) {
  const [open, setOpen] = useState(false)
  const selected = options.filter((option) => option.id === value)[0] ?? null

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className={clsx(
          'flex h-12 w-full items-center rounded-control border bg-bg-elevated px-3 text-left active:bg-bg-pressed',
          invalid ? 'border-danger' : 'border-border-strong',
        )}
      >
        <span
          className={clsx(
            'min-w-0 flex-1 truncate text-callout server-text',
            selected ? 'text-fg' : 'text-fg-tertiary',
          )}
        >
          {selected ? selected.name : placeholder}
        </span>
        <Icon name="chevron-right" className="ml-2 h-5 w-5 text-fg-tertiary" />
      </button>

      <Sheet open={open} title={sheetTitle} onClose={() => setOpen(false)}>
        <div className="overflow-hidden rounded-card bg-bg-elevated">
          {options.map((option) => (
            <button
              key={option.id}
              type="button"
              onClick={() => {
                onChange(option.id)
                setOpen(false)
              }}
              className="flex min-h-[52px] w-full items-center border-t border-border px-1 py-2 text-left first:border-t-0 active:bg-bg-pressed"
            >
              <span className="min-w-0 flex-1 text-body text-fg server-text">{option.name}</span>
              {option.id === value ? (
                <Icon name="check-circle" className="ml-2 h-5 w-5 text-accent-text" />
              ) : null}
            </button>
          ))}
        </div>
      </Sheet>
    </>
  )
}

/**
 * Sana tanlash qatori.
 *
 * ⚠️ `<input type="date">` telefonlar orasida juda turlicha chiziladi,
 * shu sababli u BIZNING varaq ichida turadi: yomon chizilgan native
 * tanlagich hech bo'lmaganda ekranni buzmaydi va kerak bo'lsa
 * platformaga qarab almashtiriladi (UX §E.5).
 */
export function DateRow({
  sheetTitle,
  value,
  min,
  max,
  invalid = false,
  onChange,
}: {
  sheetTitle: string
  value: string
  min?: string
  max?: string
  invalid?: boolean
  onChange: (iso: string) => void
}) {
  const { t } = useTranslation()
  const format = useFormat()
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState(value)

  return (
    <>
      <button
        type="button"
        onClick={() => {
          setDraft(value)
          setOpen(true)
        }}
        className={clsx(
          'flex h-12 w-full items-center rounded-control border bg-bg-elevated px-3 text-left active:bg-bg-pressed',
          invalid ? 'border-danger' : 'border-border-strong',
        )}
      >
        <span className="nums min-w-0 flex-1 truncate text-callout text-fg">
          {value ? format.date(value) : '—'}
        </span>
        <Icon name="calendar" className="ml-2 h-5 w-5 text-fg-tertiary" />
      </button>

      <Sheet open={open} title={sheetTitle} onClose={() => setOpen(false)}>
        <input
          type="date"
          value={draft}
          min={min}
          max={max}
          onChange={(event: ChangeEvent<HTMLInputElement>) => setDraft(event.target.value)}
          className="h-12 w-full rounded-control border border-border-strong bg-bg-elevated px-3 text-callout text-fg"
        />

        <div className="mt-4 space-y-2">
          <Button
            block
            onClick={() => {
              if (draft) onChange(draft)
              setOpen(false)
            }}
          >
            {t('common.confirm')}
          </Button>
          <Button variant="plain" block onClick={() => setOpen(false)}>
            {t('common.cancel')}
          </Button>
        </div>
      </Sheet>
    </>
  )
}
