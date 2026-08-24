import { useCallback } from 'react'
import { useTranslation } from 'react-i18next'

import { formatDate, formatDateTime, formatMoney, formatNumber } from './format'
import { DEFAULT_LOCALE, localeMeta, type LocaleCode } from './locales'

/**
 * Til bilan ishlash uchun yagona kirish nuqtasi:
 * joriy til, uni almashtirish va tilga bog'liq formatlash.
 */
export function useLocale() {
  const { i18n, t } = useTranslation()

  const locale = (i18n.resolvedLanguage ?? DEFAULT_LOCALE) as LocaleCode

  const setLocale = useCallback(
    (next: LocaleCode) => {
      void i18n.changeLanguage(next)
      document.documentElement.lang = next
    },
    [i18n],
  )

  return {
    t,
    locale,
    setLocale,
    meta: localeMeta(locale),

    num: useCallback((v: number) => formatNumber(v, locale), [locale]),
    money: useCallback(
      (v: number, currency?: string) => formatMoney(v, locale, currency),
      [locale],
    ),
    date: useCallback((v: Date | string) => formatDate(v, locale), [locale]),
    dateTime: useCallback((v: Date | string) => formatDateTime(v, locale), [locale]),
  }
}

/**
 * Backend xato kodini joriy tilga tarjima qiladi.
 *
 * Backend xabar emas, KOD qaytaradi (`pinfl.nazorat_raqami`) —
 * shu sababli xato matni ham foydalanuvchi tilida chiqadi.
 * Tarjimasi yo'q kod bo'lsa — kodning o'zi ko'rsatiladi (yashirilmaydi).
 */
export function useErrorMessage() {
  const { t } = useTranslation()

  return useCallback(
    (code: string | undefined | null, fallback?: string): string => {
      if (!code) return fallback ?? t('common.error')
      const key = `errors.${code}`
      const translated = t(key)
      return translated === key ? (fallback ?? code) : translated
    },
    [t],
  )
}
