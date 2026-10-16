import { useLayoutEffect, useRef, type ChangeEvent, type ComponentProps, type Ref } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { displayPercent, percentDigits } from '@/shared/lib/percent-input'
import { TextField } from './text-field'

type PercentInputProps = Omit<
  ComponentProps<typeof TextField>,
  'value' | 'onChange' | 'type' | 'inputMode' | 'ref'
> & {
  /** Kanonik qiymat («4», «2.5») yoki bo'sh satr. */
  value: string
  /** Kanonik qiymat — kasr ajratgichi doim «.». */
  onChange: (canonical: string) => void
  ref?: Ref<HTMLInputElement>
}

/**
 * Foiz maydoni — faqat raqam, ko'pi bilan 100 va ikki kasr xona: «4 %»,
 * «2,5 %».
 *
 * Yaroqsiz bosish (harf, 100 dan katta son, uchinchi kasr xona) umuman
 * QABUL QILINMAYDI — «150» ni yozdirib, keyin xato ko'rsatish o'rniga.
 * Kasr ajratgichi sifatida «,» ham, «.» ham qabul qilinadi; ekranda
 * o'zbek va rus tilida vergul ko'rinadi.
 *
 * Kursor raqamlar SONI bo'yicha tiklanadi (`MoneyInput` dagi kabi): «.»
 * vergulga almashtirilganda yoki boshidagi nol tashlanganda u oxirga
 * sakramaydi.
 */
export function PercentInput({
  value,
  onChange,
  autoComplete = 'off',
  inputClassName,
  ref,
  ...field
}: PercentInputProps) {
  const { locale } = useLocale()
  const decimalComma = locale !== 'en'

  const inputRef = useRef<HTMLInputElement>(null)
  const caret = useRef<number | null>(null)

  /** Ichki ref va tashqaridan berilgani — ikkalasi ham shu `<input>` ni oladi. */
  const setRefs = (node: HTMLInputElement | null) => {
    inputRef.current = node
    if (typeof ref === 'function') ref(node)
    else if (ref) ref.current = node
  }

  useLayoutEffect(() => {
    const position = caret.current
    if (position === null || !inputRef.current) return

    caret.current = null
    inputRef.current.setSelectionRange(position, position)
  })

  const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
    const raw = event.target.value
    const next = percentDigits(raw)
    if (next === null) return

    // Kursorgacha bo'lgan belgilar (raqam va ajratgich) soni saqlanadi.
    const selection = event.target.selectionStart ?? raw.length
    const before = raw.slice(0, selection).replaceAll(/[^\d.,]/gu, '').length
    const shown = displayPercent(next, decimalComma)
    caret.current = Math.min(before, shown.length)

    onChange(next)
  }

  return (
    <TextField
      {...field}
      ref={setRefs}
      value={displayPercent(value, decimalComma)}
      onChange={handleChange}
      type="text"
      inputMode="decimal"
      autoComplete={autoComplete}
      trailing={
        <span className="shrink-0 text-footnote text-[var(--fg-tertiary)] select-none">%</span>
      }
      inputClassName={inputClassName ?? 'tabular'}
    />
  )
}
