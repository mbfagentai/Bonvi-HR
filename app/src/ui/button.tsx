import clsx from 'clsx'
import type { ButtonHTMLAttributes, ReactNode } from 'react'

/**
 * Tugma (UX §E.3).
 *
 * ⚠️ Bosilganda tugma QORAYADI — kichraymaydi, sakramaydi, to'lqin
 * chiqarmaydi (UX §D.8). Brauzerning kulrang «yonishi» global
 * o'chirilgan, shu sababli `:active` holati bu yerda MAJBURIY:
 * usiz tugma sekin tarmoqda umuman javob bermayotgandek ko'rinadi.
 */
type Variant = 'primary' | 'secondary' | 'plain' | 'danger'
type Size = 'hero' | 'regular' | 'compact'

interface ButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'className'> {
  variant?: Variant
  size?: Size
  /** Ish ketmoqda: spinner + ishchi fe'l. Kenglik o'zgarmaydi. */
  busy?: boolean
  block?: boolean
  children: ReactNode
}

const VARIANT_CLASS: Record<Variant, string> = {
  primary: 'bg-accent text-fg-on-accent active:bg-accent-pressed',
  secondary: 'bg-accent-soft text-accent-on-soft active:bg-bg-pressed',
  plain: 'bg-transparent text-accent-text active:bg-bg-pressed',
  danger: 'bg-danger text-fg-on-accent active:opacity-90',
}

const SIZE_CLASS: Record<Size, string> = {
  // 64 px — ilovadagi eng katta boshqaruv, eng katta burchak radiusi
  // bilan: 12 px burchak bunday tugmani quticha qilib ko'rsatardi.
  hero: 'h-16 text-title2 rounded-hero px-6',
  regular: 'h-12 text-body rounded-control px-5',
  compact: 'h-9 text-subhead rounded-control px-4',
}

export function Button({
  variant = 'primary',
  size = 'regular',
  busy = false,
  block = false,
  disabled,
  children,
  ...rest
}: ButtonProps) {
  return (
    <button
      type="button"
      disabled={disabled || busy}
      {...rest}
      className={clsx(
        'inline-flex items-center justify-center font-medium transition-colors duration-150 ease-ios',
        'disabled:opacity-60',
        VARIANT_CLASS[variant],
        SIZE_CLASS[size],
        block && 'w-full',
      )}
    >
      {busy ? <Spinner /> : null}
      <span className="truncate">{children}</span>
    </button>
  )
}

function Spinner() {
  return (
    <svg
      viewBox="0 0 24 24"
      className="mr-2 h-5 w-5 animate-spin"
      fill="none"
      aria-hidden="true"
      focusable="false"
    >
      <circle cx="12" cy="12" r="9" stroke="currentColor" strokeWidth="2" opacity="0.25" />
      <path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  )
}

/**
 * Forma tugmasi — xavfsiz zonadan yuqorida qadalgan holda turadi
 * (UX §E.3). Shu sababli hech bir formada «saqlash» uzun aylantirish
 * oxirida qolib ketmaydi.
 */
export function BottomAction({ children }: { children: ReactNode }) {
  return (
    <div className="fixed bottom-0 left-0 right-0 z-20 border-t border-border bg-bg-elevated">
      <div className="mx-auto w-full max-w-app px-4 pb-safe pt-3">
        <div className="pb-3">{children}</div>
      </div>
    </div>
  )
}
