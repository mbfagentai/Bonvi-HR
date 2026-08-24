import { useId, type ComponentPropsWithRef, type ReactNode } from 'react'

import { cn } from '@/shared/lib/cn'

type TextFieldProps = Omit<ComponentPropsWithRef<'input'>, 'size' | 'prefix'> & {
  label?: ReactNode
  /** Maydon ostidagi izoh. Xato chiqqanda o'rnini xatoga bo'shatadi. */
  hint?: ReactNode
  /** Tayyor matn — tarjima chaqiruvchi tomonda bajariladi. */
  error?: string | undefined
  /** Maydon ichidagi o'zgarmas prefiks: «+998», «UZS». */
  leading?: ReactNode
  /** O'ng chetdagi boshqaruv: parolni ko'rsatish tugmasi, tozalash. */
  trailing?: ReactNode
  /** Ichki `<input>` uchun qo'shimcha sinflar (tashqi `className` — o'ram uchun). */
  inputClassName?: string
}

/**
 * Yorliq + maydon + xato — formaning bir qatori.
 *
 * `Input` dan farqi: bu yerda maydon «qobiq» ichida turadi, shu sababli
 * ichiga prefiks va tugma qo'yish mumkin. Fokus halqasi `focus-within`
 * orqali butun qobiqqa tushadi — iOS'da ham shunday: kursor emas,
 * butun yacheyka yonadi.
 */
export function TextField({
  label,
  hint,
  error,
  leading,
  trailing,
  variant = 'outlined',
  size = 'md',
  className,
  inputClassName,
  id,
  ...input
}: TextFieldProps & {
  /**
   * `filled` — kulrang fonli, chegarasiz maydon.
   * iOS'dagi qidiruv va forma maydonlari shunday: qobiq fonga
   * chuqurcha bo'lib botadi, chegara chizig'i bilan ajratilmaydi.
   */
  variant?: 'outlined' | 'filled'
  size?: 'md' | 'lg'
}) {
  const generatedId = useId()
  const fieldId = id ?? generatedId
  const messageId = `${fieldId}-message`

  return (
    <div className={cn('space-y-2', className)}>
      {label !== undefined && (
        <label
          htmlFor={fieldId}
          className="block text-footnote font-medium text-[var(--fg-secondary)]"
        >
          {label}
        </label>
      )}

      <div
        className={cn(
          'flex items-center gap-2.5 rounded-md border transition-all duration-200 ease-[var(--ease-ios)]',
          size === 'lg' ? 'h-[52px] px-4' : 'h-12 px-3.5',

          variant === 'filled'
            ? [
                'border-transparent bg-[var(--bg-sunken)]',
                error
                  ? 'ring-2 ring-ios-red/40'
                  : 'hover:bg-[var(--bg-hover)] focus-within:bg-[var(--bg-elevated)] focus-within:border-[var(--accent)] focus-within:ring-4 focus-within:ring-[var(--accent)]/10',
              ]
            : [
                'bg-[var(--bg-elevated)]',
                error
                  ? 'border-ios-red focus-within:ring-4 focus-within:ring-ios-red/12'
                  : [
                      'border-[var(--border-strong)] hover:border-[var(--fg-tertiary)]',
                      'focus-within:border-[var(--accent)] focus-within:ring-4 focus-within:ring-[var(--accent)]/10',
                    ],
              ],

          input.disabled && 'opacity-50',
        )}
      >
        {leading !== undefined && (
          <span className="shrink-0 text-body text-[var(--fg-secondary)] select-none">
            {leading}
          </span>
        )}

        <input
          id={fieldId}
          aria-invalid={error ? true : undefined}
          aria-describedby={error || hint ? messageId : undefined}
          className={cn(
            'min-w-0 flex-1 bg-transparent text-body outline-none',
            'placeholder:text-[var(--fg-tertiary)]',
            'disabled:cursor-not-allowed',
            inputClassName,
          )}
          {...input}
        />

        {trailing}
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
