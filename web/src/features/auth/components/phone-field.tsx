import type { ReactNode } from 'react'

import { PhoneInput } from '@/shared/ui'

/**
 * Kirish formasining telefon maydoni: `+998 __ ___ __ __`.
 *
 * Mask o'zi umumiy `PhoneInput` da (tizimdagi hamma telefon maydoni bir
 * xil ishlaydi). Bu yerda faqat kirish ekraniga xos ko'rinish qoladi:
 * katta, kulrang fonli maydon va brauzer login sifatida eslab qolishi
 * uchun `autoComplete="username"`.
 */
export function PhoneField({
  value,
  onChange,
  onBlur,
  name,
  label,
  error,
  disabled = false,
  autoFocus = false,
}: {
  /** 9 xona, formatlanmagan. */
  value: string
  onChange: (digits: string) => void
  onBlur?: () => void
  name?: string
  label?: ReactNode
  error?: string | undefined
  disabled?: boolean
  autoFocus?: boolean
}) {
  return (
    <PhoneInput
      variant="filled"
      size="lg"
      name={name}
      label={label}
      error={error}
      disabled={disabled}
      autoFocus={autoFocus}
      value={value}
      onChange={onChange}
      onBlur={onBlur}
      autoComplete="username"
    />
  )
}
