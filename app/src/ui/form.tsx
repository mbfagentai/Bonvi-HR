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
 * Xato xabari HAR DOIM maydon OSTIDA, yuqorida umumiy ro'yxat yo'q.
 * Uzun formada (jadval so'rovi — bir necha sana) yuborishda birinchi
 * xatoli maydonga aylantiriladi: `Field` uni `data-invalid` bilan belgilaydi.
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
    // `data-invalid` — yuborishda birinchi xatoli maydonga aylantirish uchun (UX §E.5).
    <div data-invalid={error ? '' : undefined}>
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
  emptyText,
  invalid = false,
  onChange,
}: {
  sheetTitle: string
  value: string | null
  options: readonly SelectOption[]
  placeholder: string
  /**
   * Ro'yxat bo'sh bo'lganda varaqda chiqadigan matn. Busiz bo'sh varaq
   * faqat sarlavha bilan ochilardi va xodim «ilova buzildi» deb o'ylardi
   * (2026-10-17: prod'da «Yo'qlik turlari» ma'lumotnomasi bo'sh edi).
   */
  emptyText?: string
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
        {options.length === 0 && emptyText ? (
          <p className="rounded-card bg-bg-elevated px-4 py-5 text-center text-callout text-fg-secondary">
            {emptyText}
          </p>
        ) : null}
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

/**
 * Vaqt tanlash qatori — `DateRow` ning egizagi (UX §E.5).
 *
 * Qiymat IXTIYORIY: bo'sh qator «butun kun» degani, shu sababli varaqda
 * tanlangan vaqtni olib tashlash tugmasi ham bor.
 */
export function TimeRow({
  sheetTitle,
  value,
  placeholder,
  clearLabel,
  invalid = false,
  onChange,
}: {
  sheetTitle: string
  /** `HH:mm` yoki bo'sh satr. */
  value: string
  placeholder: string
  /**
   * Tanlangan vaqtni olib tashlash tugmasi — bo'sh qiymat bilan `onChange`.
   * Berilmasa tugma yo'q: MAJBURIY vaqtni bo'shatishning ma'nosi yo'q.
   */
  clearLabel?: string
  invalid?: boolean
  onChange: (value: string) => void
}) {
  const { t } = useTranslation()
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
        <span
          className={clsx(
            'nums min-w-0 flex-1 truncate text-callout',
            value ? 'text-fg' : 'text-fg-tertiary',
          )}
        >
          {value || placeholder}
        </span>
        <Icon name="clock" className="ml-2 h-5 w-5 text-fg-tertiary" />
      </button>

      <Sheet open={open} title={sheetTitle} onClose={() => setOpen(false)}>
        <input
          type="time"
          value={draft}
          onChange={(event: ChangeEvent<HTMLInputElement>) => setDraft(event.target.value)}
          className="nums h-12 w-full rounded-control border border-border-strong bg-bg-elevated px-3 text-callout text-fg"
        />

        <div className="mt-4 space-y-2">
          <Button
            block
            onClick={() => {
              // Brauzer soniyani ham qo'shishi mumkin («08:30:00») —
              // forma faqat soat va daqiqani saqlaydi.
              if (draft) onChange(draft.slice(0, 5))
              setOpen(false)
            }}
          >
            {t('common.confirm')}
          </Button>
          {value && clearLabel ? (
            <Button
              variant="plain"
              block
              onClick={() => {
                onChange('')
                setOpen(false)
              }}
            >
              {clearLabel}
            </Button>
          ) : null}
          <Button variant="plain" block onClick={() => setOpen(false)}>
            {t('common.cancel')}
          </Button>
        </div>
      </Sheet>
    </>
  )
}

/**
 * Bo'lakli tanlagich — iOS «segmented control» (Verifix'dagi «Часть дня /
 * Весь день / Несколько дней» radiosi).
 *
 * ⚠️ Yorliq QISQARTIRILMAYDI: 320 px ekranda «Bir necha kun» ikki qatorga
 * tushadi — asosiy yorliqni «…» bilan kesish taqiqlangan (UX §F.5).
 * Bo'laklar orasida flex `gap` yo'q (Safari 14.1 dan past, UX §F.2).
 */
export function SegmentedControl<T extends string>({
  label,
  value,
  options,
  onChange,
}: {
  /** Ekran o'quvchi uchun guruh nomi — ko'rinadigan yorliq bo'lmasa ham. */
  label: string
  value: T
  options: ReadonlyArray<{ value: T; label: string }>
  onChange: (value: T) => void
}) {
  return (
    <div role="radiogroup" aria-label={label} className="flex rounded-control bg-bg-sunken p-1">
      {options.map((option) => {
        const selected = option.value === value

        return (
          <button
            key={option.value}
            type="button"
            role="radio"
            aria-checked={selected}
            onClick={() => {
              if (!selected) onChange(option.value)
            }}
            className={clsx(
              'min-h-[40px] min-w-0 flex-1 rounded-chip px-2 py-1.5 text-center text-subhead leading-tight transition-colors duration-150 ease-ios',
              selected
                ? 'bg-bg-elevated font-semibold text-fg shadow-[0_1px_3px_rgba(0,0,0,0.12)]'
                : 'font-medium text-fg-secondary active:bg-bg-pressed',
            )}
          >
            {option.label}
          </button>
        )
      })}
    </div>
  )
}

/**
 * Yorliqli o'chirgich (UX §E.2, `toggle` qatori). Butun qator bosiladi —
 * kichik dumaloqni nishonga olish shart emas (UX §D.8, 44 px).
 */
export function SwitchRow({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
}) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      onClick={() => onChange(!checked)}
      className="flex min-h-[44px] w-full items-center rounded-control text-left active:bg-bg-pressed"
    >
      <span className="min-w-0 flex-1 text-body text-fg">{label}</span>
      <span
        aria-hidden="true"
        className={clsx(
          'relative ml-3 inline-block h-[31px] w-[51px] shrink-0 rounded-pill transition-colors duration-150 ease-ios',
          checked ? 'bg-accent' : 'bg-border-strong',
        )}
      >
        <span
          className={clsx(
            'absolute left-[2px] top-[2px] h-[27px] w-[27px] rounded-pill bg-fg-on-accent shadow-[0_1px_3px_rgba(0,0,0,0.2)] transition-transform duration-150 ease-ios',
            checked ? 'translate-x-[20px]' : 'translate-x-0',
          )}
        />
      </span>
    </button>
  )
}
