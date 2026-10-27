import { ChevronDown } from 'lucide-react'
import { useId, type ComponentPropsWithRef, type ReactNode } from 'react'

import { cn } from '@/shared/lib/cn'

export interface SelectOption {
  value: string
  label: string
  /** Ierarxiyani ko'rsatish uchun oldingi bo'shliqlar (bo'limlar daraxti). */
  depth?: number
  disabled?: boolean
}

/**
 * Tanlov maydoni.
 *
 * Ataylab brauzerning o'z ro'yxati (`<select>`): u har bir platformada
 * o'sha platformaning tanish ko'rinishida ochiladi — iOS'da g'ildirak,
 * macOS'da menyu. Uni React bilan qayta yozish faqat yomonlashtiradi.
 * Bizning ishimiz — yopiq holatdagi qobiqni qolgan maydonlarga o'xshatish.
 */
export function Select({
  label,
  hint,
  error,
  options,
  placeholder,
  size = 'md',
  className,
  id,
  ...select
}: Omit<ComponentPropsWithRef<'select'>, 'children' | 'size'> & {
  label?: ReactNode
  hint?: ReactNode
  error?: string | undefined
  options: readonly SelectOption[]
  /** Bo'sh qiymat uchun birinchi qator. Berilmasa — bo'sh qiymat yo'q. */
  placeholder?: string
  /**
   * `sm` — ro'yxat paneli qatorida, qidiruv maydoni yonida (`Input`, h-10,
   * 15 px). Standart `md` — forma maydoni (`TextField` bilan bir xil, h-12, 17 px).
   */
  size?: 'md' | 'sm'
}) {
  const generatedId = useId()
  const fieldId = id ?? generatedId
  const messageId = `${fieldId}-message`

  return (
    <div className={cn(size === 'sm' ? 'space-y-1.5' : 'space-y-2', className)}>
      {label !== undefined && (
        <label
          htmlFor={fieldId}
          className="block text-footnote font-medium text-[var(--fg-secondary)]"
        >
          {label}
        </label>
      )}

      <div className="relative">
        <select
          id={fieldId}
          aria-invalid={error ? true : undefined}
          aria-describedby={error || hint ? messageId : undefined}
          className={cn(
            'w-full appearance-none rounded-md border',
            size === 'sm' ? 'h-10 pr-9 pl-3 text-subhead' : 'h-12 pr-10 pl-3.5 text-body',
            'bg-[var(--bg-elevated)] text-[var(--fg)]',
            'transition-all duration-200 ease-[var(--ease-ios)] outline-none',
            error
              ? 'border-ios-red focus:ring-4 focus:ring-ios-red/12'
              : [
                  'border-[var(--border-strong)] hover:border-[var(--fg-tertiary)]',
                  'focus:border-[var(--accent)] focus:ring-4 focus:ring-[var(--accent)]/10',
                ],
            'disabled:cursor-not-allowed disabled:opacity-50',
          )}
          {...select}
        >
          {placeholder !== undefined && <option value="">{placeholder}</option>}

          {options.map((option) => (
            <option key={option.value} value={option.value} disabled={option.disabled}>
              {/* Daraxt chuqurligi bo'sh joy bilan — `<option>` ichida
                  boshqa belgilash ishlamaydi */}
              {'  '.repeat(option.depth ?? 0)}
              {option.label}
            </option>
          ))}
        </select>

        <ChevronDown
          aria-hidden
          className={cn(
            'pointer-events-none absolute top-1/2 size-4 -translate-y-1/2 text-[var(--fg-tertiary)]',
            size === 'sm' ? 'right-3' : 'right-3.5',
          )}
          strokeWidth={2.5}
        />
      </div>

      {error ? (
        <p id={messageId} role="alert" className="text-footnote text-ios-red">
          {error}
        </p>
      ) : hint !== undefined ? (
        <p id={messageId} className="text-footnote text-[var(--fg-tertiary)]">
          {hint}
        </p>
      ) : null}
    </div>
  )
}
