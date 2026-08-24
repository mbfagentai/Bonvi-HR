import type { LucideIcon } from 'lucide-react'
import type { ComponentPropsWithoutRef } from 'react'

import { cn } from '@/shared/lib/cn'

export function Input({
  className,
  icon: Icon,
  shape = 'rounded',
  size = 'md',
  ...props
}: Omit<ComponentPropsWithoutRef<'input'>, 'size'> & {
  icon?: LucideIcon
  /** `pill` — to'liq dumaloq burchak (iOS qidiruv maydoni). */
  shape?: 'rounded' | 'pill'
  size?: 'md' | 'lg'
}) {
  const pill = shape === 'pill'
  const large = size === 'lg'

  return (
    <div className="relative w-full">
      {Icon && (
        <Icon
          className={cn(
            'pointer-events-none absolute top-1/2 -translate-y-1/2 text-[var(--fg-tertiary)]',
            large ? 'left-4 size-[18px]' : 'left-3 size-4',
          )}
          strokeWidth={2}
        />
      )}

      <input
        className={cn(
          'w-full border transition-all duration-200 ease-[var(--ease-ios)]',
          'bg-[var(--bg-elevated)] border-[var(--border-strong)]',
          'placeholder:text-[var(--fg-tertiary)]',
          'hover:border-[var(--fg-tertiary)]',
          // Fokusda brend rangli nozik halqa — iOS'dagi qidiruv maydoni kabi
          'focus:border-[var(--accent)] focus:ring-4 focus:ring-[var(--accent)]/10 focus:outline-none',
          'disabled:opacity-50',
          large ? 'h-11 text-subhead' : 'h-10 text-subhead',
          pill ? 'rounded-full' : 'rounded-md',
          Icon
            ? large
              ? pill ? 'pr-5 pl-11' : 'pr-4 pl-11'
              : pill ? 'pr-4 pl-9' : 'pr-3 pl-9'
            : large
              ? pill ? 'px-5' : 'px-4'
              : pill ? 'px-4' : 'px-3',
          className,
        )}
        {...props}
      />
    </div>
  )
}
