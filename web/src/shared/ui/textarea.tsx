import { useId, type ComponentPropsWithRef, type ReactNode } from 'react'

import { cn } from '@/shared/lib/cn'

/**
 * Ko'p qatorli matn maydoni — izoh va tavsif uchun.
 *
 * `TextField` bilan bir xil qobiq: bir formada ikkalasi yonma-yon turganda
 * balandligidan boshqa farq sezilmasligi kerak.
 */
export function TextArea({
  label,
  hint,
  error,
  className,
  id,
  rows = 3,
  ...textarea
}: Omit<ComponentPropsWithRef<'textarea'>, 'size'> & {
  label?: ReactNode
  hint?: ReactNode
  error?: string | undefined
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

      <textarea
        id={fieldId}
        rows={rows}
        aria-invalid={error ? true : undefined}
        aria-describedby={error || hint ? messageId : undefined}
        className={cn(
          'w-full resize-y rounded-md border px-3.5 py-3 text-body leading-relaxed',
          'bg-[var(--bg-elevated)] placeholder:text-[var(--fg-tertiary)]',
          'transition-all duration-200 ease-[var(--ease-ios)] outline-none',
          error
            ? 'border-ios-red focus:ring-4 focus:ring-ios-red/12'
            : [
                'border-[var(--border-strong)] hover:border-[var(--fg-tertiary)]',
                'focus:border-[var(--accent)] focus:ring-4 focus:ring-[var(--accent)]/10',
              ],
          'disabled:cursor-not-allowed disabled:opacity-50',
        )}
        {...textarea}
      />

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
