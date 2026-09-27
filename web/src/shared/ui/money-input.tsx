import { useLayoutEffect, useRef, type ChangeEvent, type ComponentProps } from 'react'

import { caretAfterDigit } from '@/shared/lib/phone'
import { formatMoneyDigits, moneyDigits } from '@/shared/lib/money-input'
import { TextField } from './text-field'

type MoneyInputProps = Omit<
  ComponentProps<typeof TextField>,
  'value' | 'onChange' | 'type' | 'inputMode' | 'ref'
> & {
  /** Faqat raqamlar («1500000») yoki bo'sh satr. Bo'shliqli qiymat ham tushuniladi. */
  value: string
  /** Faqat raqamlar, bo'shliqsiz — forma va server avvalgidek o'qiydi. */
  onChange: (digits: string) => void
  /** Maydon oxiridagi valyuta belgisi. */
  currency?: string
}

/**
 * Summa maydoni — yozish paytida raqamlar uch xonadan ajratiladi:
 * «1 500 000 so'm».
 *
 * Kursor raqamlar SONI bo'yicha tiklanadi (`PhoneInput` dagi bilan bir xil
 * yo'l): bo'shliqlar qayta qo'yilganda ham kursor o'z raqamining yonida
 * qoladi, o'rtadagi xonani tahrirlash oxirga sakratmaydi.
 */
export function MoneyInput({
  value,
  onChange,
  currency = "so'm",
  autoComplete = 'off',
  inputClassName,
  ...field
}: MoneyInputProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const caret = useRef<number | null>(null)

  useLayoutEffect(() => {
    const position = caret.current
    if (position === null || !inputRef.current) return

    caret.current = null
    inputRef.current.setSelectionRange(position, position)
  })

  const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value
    const selection = event.target.selectionStart ?? raw.length

    // Kursorgacha bo'lgan RAQAMLAR soni saqlanadi (kasr nuqtasi sanalmaydi)
    const prefix = raw.slice(0, selection)
    const digitsBeforeCaret = moneyDigits(prefix).replace('.', '').length
    const digits = moneyDigits(raw)
    const formatted = formatMoneyDigits(digits)

    let position = caretAfterDigit(formatted, digitsBeforeCaret)

    // Vergul endigina terilgan bo'lsa, kursor uning ORTIDA turishi kerak —
    // aks holda keyingi raqam kasr emas, butun qism bo'lib ketardi
    if (/[.,]$/u.test(prefix) && formatted[position] === ',') position += 1

    caret.current = position
    onChange(digits)
  }

  return (
    <TextField
      {...field}
      ref={inputRef}
      value={formatMoneyDigits(moneyDigits(value))}
      onChange={handleChange}
      type="text"
      inputMode="numeric"
      autoComplete={autoComplete}
      trailing={<span className="shrink-0 text-footnote text-[var(--fg-tertiary)] select-none">{currency}</span>}
      inputClassName={inputClassName ?? 'tabular'}
    />
  )
}
