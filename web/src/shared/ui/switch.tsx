import { useId, type ReactNode } from 'react'

import { cn } from '@/shared/lib/cn'

/**
 * iOS almashtirgichi.
 *
 * Checkbox'dan farqi: bu «ha/yo'q» emas, DARHOL kuchga kiradigan holat
 * (yoqilgan/o'chirilgan). Shu sababli ko'rinishi ham boshqacha — sirg'aluvchi
 * tugmacha, yoqilganda yashil (Apple'ning standart rangi).
 *
 * Ichkarida haqiqiy `<input type="checkbox">` turadi: klaviatura, skrinriderlar
 * va forma bilan bog'lanish o'z-o'zidan ishlaydi.
 */
export function Switch({
  checked,
  onChange,
  label,
  hint,
  id,
  disabled = false,
  className,
  'aria-label': ariaLabel,
}: {
  checked: boolean
  onChange: (checked: boolean) => void
  label?: ReactNode
  hint?: ReactNode
  id?: string
  disabled?: boolean
  className?: string
  /** `label` berilmaganda skrinriderlar uchun nom — majburiy. */
  'aria-label'?: string
}) {
  const generatedId = useId()
  const fieldId = id ?? generatedId

  return (
    <label
      htmlFor={fieldId}
      className={cn(
        'flex items-center justify-between gap-4 select-none',
        disabled ? 'cursor-not-allowed opacity-50' : 'cursor-pointer',
        className,
      )}
    >
      {(label !== undefined || hint !== undefined) && (
        <span className="min-w-0 leading-tight">
          {label !== undefined && (
            <span className="block text-subhead font-medium">{label}</span>
          )}
          {hint !== undefined && (
            <span className="mt-0.5 block text-footnote text-[var(--fg-secondary)]">
              {hint}
            </span>
          )}
        </span>
      )}

      <span className="relative inline-flex h-[31px] w-[51px] shrink-0 items-center">
        <input
          id={fieldId}
          type="checkbox"
          role="switch"
          aria-label={ariaLabel}
          checked={checked}
          disabled={disabled}
          onChange={(event) => onChange(event.target.checked)}
          className={cn(
            'peer absolute inset-0 size-full appearance-none rounded-full',
            'bg-[var(--border-strong)] transition-colors duration-200 ease-[var(--ease-ios)]',
            'checked:bg-ios-green',
            disabled ? 'cursor-not-allowed' : 'cursor-pointer',
          )}
        />

        {/* Tugmacha. `peer-checked` bilan siljiydi — React qayta render qilmaydi */}
        <span
          aria-hidden
          className={cn(
            'pointer-events-none relative ml-0.5 size-[27px] rounded-full bg-white shadow-raised',
            'transition-transform duration-200 ease-[var(--ease-ios-out)]',
            'peer-checked:translate-x-5',
          )}
        />
      </span>
    </label>
  )
}
