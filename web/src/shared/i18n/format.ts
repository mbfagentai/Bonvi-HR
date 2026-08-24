import { intlTag, type LocaleCode } from './locales'

/**
 * Raqamni bo'sh joy bilan ajratib formatlaydi: 4500000 → «4 500 000».
 *
 * Nega qo'lda: brauzerlar `uz-Latn-UZ` va `uz-Cyrl-UZ` teglarini
 * to'liq qo'llab-quvvatlamaydi va vergul qo'yib yuborishi mumkin.
 * O'zbek va rus tilida ming ajratgichi — bo'sh joy.
 */
export function formatNumber(value: number, locale: LocaleCode): string {
  const useSpace = locale !== 'en'
  const formatted = new Intl.NumberFormat(useSpace ? 'ru-RU' : 'en-US').format(value)
  // ru-RU uzilmaydigan bo'sh joy (U+00A0) qo'yadi — oddiy bo'sh joyga almashtiramiz
  return useSpace ? formatted.replaceAll(' ', ' ') : formatted
}

/** Pul: «4 500 000 so'm». Valyuta yorlig'i tilga qarab o'zgaradi. */
export function formatMoney(
  value: number,
  locale: LocaleCode,
  currency = 'UZS',
): string {
  const amount = formatNumber(value, locale)

  if (currency !== 'UZS') {
    return `${amount} ${currency}`
  }

  const suffix: Record<LocaleCode, string> = {
    'uz-Latn': "so'm",
    'uz-Cyrl': 'сўм',
    ru: 'сум',
    en: 'UZS',
  }

  return `${amount} ${suffix[locale]}`
}

/** Sana: «15.01.1991». Barcha tillarda bir xil — chalkashlik bo'lmasligi uchun. */
export function formatDate(value: Date | string, locale: LocaleCode): string {
  const date = typeof value === 'string' ? new Date(value) : value
  if (Number.isNaN(date.getTime())) return '—'

  const dd = String(date.getDate()).padStart(2, '0')
  const mm = String(date.getMonth() + 1).padStart(2, '0')
  const yyyy = date.getFullYear()

  return locale === 'en' ? `${yyyy}-${mm}-${dd}` : `${dd}.${mm}.${yyyy}`
}

/** Sana va vaqt: «15.01.1991 09:30». */
export function formatDateTime(value: Date | string, locale: LocaleCode): string {
  const date = typeof value === 'string' ? new Date(value) : value
  if (Number.isNaN(date.getTime())) return '—'

  const time = new Intl.DateTimeFormat(intlTag(locale) === 'en-US' ? 'en-GB' : 'ru-RU', {
    hour: '2-digit',
    minute: '2-digit',
  }).format(date)

  return `${formatDate(date, locale)} ${time}`
}
