import { Check } from 'lucide-react'
import { useId, type ReactNode } from 'react'

import { cn } from '@/shared/lib/cn'

/**
 * Belgilash katakchasi.
 *
 * Brauzerning o'z ko'rinishi o'chirilgan (`appearance-none`) va o'rniga
 * yumshoq burchakli kvadrat chiziladi — iOS'dagi ro'yxat belgisi kabi.
 * Belgi (✓) alohida element: `peer-checked` orqali kattalashib paydo bo'ladi,
 * shu sababli o'tish silliq bo'ladi.
 */
export function Checkbox({
  checked,
  onChange,
  label,
  id,
  disabled = false,
  className,
}: {
  checked: boolean
  onChange: (checked: boolean) => void
  label?: ReactNode
  id?: string
  disabled?: boolean
  className?: string
}) {
  const generatedId = useId()
  const fieldId = id ?? generatedId

  return (
    <label
      htmlFor={fieldId}
      className={cn(
        'inline-flex items-center gap-2.5 select-none',
        disabled ? 'cursor-not-allowed opacity-50' : 'cursor-pointer',
        className,
      )}
    >
      <span className="relative grid size-5 shrink-0 place-items-center">
        <input
          id={fieldId}
          type="checkbox"
          checked={checked}
          disabled={disabled}
          onChange={(event) => onChange(event.target.checked)}
          className={cn(
            'peer absolute inset-0 size-full appearance-none rounded-xs border',
            'border-[var(--border-strong)] bg-[var(--bg-elevated)]',
            'transition-colors duration-150 ease-[var(--ease-ios)]',
            'checked:border-[var(--accent)] checked:bg-[var(--accent)]',
            disabled ? 'cursor-not-allowed' : 'cursor-pointer',
          )}
        />
        <Check
          className={cn(
            'pointer-events-none relative size-3.5 text-[var(--fg-on-accent)]',
            'scale-50 opacity-0 transition-all duration-150 ease-[var(--ease-ios-out)]',
            'peer-checked:scale-100 peer-checked:opacity-100',
          )}
          strokeWidth={3.5}
        />
      </span>

      {label !== undefined && (
        <span className="text-subhead text-[var(--fg-secondary)]">{label}</span>
      )}
    </label>
  )
}
