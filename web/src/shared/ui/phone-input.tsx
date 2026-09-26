import { useLayoutEffect, useRef, type ChangeEvent, type ComponentProps } from 'react'

import {
  UZ_PHONE_PLACEHOLDER,
  caretAfterDigit,
  formatUzPhone,
  uzPhoneDigits,
} from '@/shared/lib/phone'
import { TextField } from './text-field'

type PhoneInputProps = Omit<
  ComponentProps<typeof TextField>,
  'value' | 'onChange' | 'leading' | 'type' | 'inputMode' | 'ref'
> & {
  /**
   * 9 xona («905088888»). To'liq raqam («+998905088888») berilsa ham
   * tushuniladi — mamlakat kodi tashlab yuboriladi.
   */
  value: string
  /** Faqat 9 xona, bo'shliqsiz. Bo'sh maydon — `''`. */
  onChange: (digits: string) => void
}

/**
 * Maskali telefon maydoni: `+998 90 123 45 67`.
 *
 * «+998» maydonning tahrirlanmaydigan qismi — uni terish, o'chirish yoki
 * ikki marta yozish mumkin emas. Foydalanuvchi faqat operator kodi va
 * raqamni teradi, bo'shliqlar o'zi qo'yiladi. Tashqariga FAQAT 9 xona
 * chiqadi, shu sababli formalar sxemasi (`/^\d{9}$/`) o'zgarmaydi.
 *
 * Maydon bo'sh qolishi mumkin: ixtiyoriy telefon (nomzod, ikkinchi raqam)
 * ham shu komponentdan foydalanadi, majburiylikni forma sxemasi hal qiladi.
 */
export function PhoneInput({
  value,
  onChange,
  placeholder = UZ_PHONE_PLACEHOLDER,
  autoComplete = 'off',
  inputClassName,
  ...field
}: PhoneInputProps) {
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
      {...field}
      ref={inputRef}
      value={formatUzPhone(uzPhoneDigits(value))}
      onChange={handleChange}
      type="tel"
      inputMode="numeric"
      autoComplete={autoComplete}
      placeholder={placeholder}
      leading={<span className="tabular font-medium">+998</span>}
      inputClassName={inputClassName ?? 'tabular tracking-[0.02em]'}
    />
  )
}
