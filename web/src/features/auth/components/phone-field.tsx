import { useLayoutEffect, useRef, type ChangeEvent, type ReactNode } from 'react'

import { TextField } from '@/shared/ui'
import {
  caretAfterDigit,
  formatUzPhone,
  UZ_PHONE_PLACEHOLDER,
  uzPhoneDigits,
} from '../lib/phone'

/**
 * Maskali telefon maydoni: `+998 __ ___ __ __`.
 *
 * Tashqariga FAQAT 9 xona chiqadi («905088888»), ekranda esa bo'shliqlar
 * bilan ajratilgan ko'rinish. «+998» maydonning tahrirlanmaydigan qismi.
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
  const inputRef = useRef<HTMLInputElement>(null)
  /** Mask qayta qurilgandan keyin kursor qaytariladigan joy. */
  const caret = useRef<number | null>(null)

  // Kursorni React qiymatni chizib bo'lgach joylashtiramiz — `useEffect`
  // bo'lganda foydalanuvchi bir kadr davomida sakragan kursorni ko'rardi
  useLayoutEffect(() => {
    const position = caret.current
    if (position === null || !inputRef.current) return

    caret.current = null
    inputRef.current.setSelectionRange(position, position)
  })

  const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value
    const selection = event.target.selectionStart ?? raw.length

    // Kursorgacha bo'lgan RAQAMLAR soni saqlanadi. Mask qayta qurilganda
    // bo'shliqlar joyi o'zgaradi, raqamlar tartibi esa o'zgarmaydi —
    // shu sababli kursor o'z raqamining yonida qoladi.
    const digitsBeforeCaret = uzPhoneDigits(raw.slice(0, selection)).length
    const digits = uzPhoneDigits(raw)

    caret.current = caretAfterDigit(formatUzPhone(digits), digitsBeforeCaret)
    onChange(digits)
  }

  return (
    <TextField
      ref={inputRef}
      variant="filled"
      size="lg"
      name={name}
      label={label}
      error={error}
      disabled={disabled}
      autoFocus={autoFocus}
      value={formatUzPhone(value)}
      onChange={handleChange}
      onBlur={onBlur}
      type="tel"
      inputMode="numeric"
      autoComplete="username"
      placeholder={UZ_PHONE_PLACEHOLDER}
      leading={<span className="tabular font-medium">+998</span>}
      inputClassName="tabular tracking-[0.02em]"
    />
  )
}
