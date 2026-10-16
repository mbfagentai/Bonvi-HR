import { tashkentParts } from '@/shared/lib/timezone'
import type { LocaleCode } from './locales'

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

/**
 * Foiz: «4 %», «2,5 %» (inglizchada «4%»). Kasr ajratgichi tilga qarab —
 * `formatNumber` bilan bir xil qoida.
 */
export function formatPercent(value: number, locale: LocaleCode): string {
  const number = formatNumber(value, locale)
  return locale === 'en' ? `${number}%` : `${number} %`
}

/** Pul: «4 500 000 so'm». Valyuta yorlig'i tilga qarab o'zgaradi. */
export function formatMoney(
  value: number,
  locale: LocaleCode,
  currency = 'UZS',
): string {
  return `${formatNumber(value, locale)} ${currencySuffix(locale, currency)}`
}

/**
 * Valyuta belgisi: so'm — tilga qarab («so'm», «сўм», «сум», «UZS»),
 * boshqasi — o'z kodi bilan. Summa maydoni (`MoneyInput`) ham shuni
 * ishlatadi: yozishdagi va ko'rsatishdagi yozuv bir xil bo'lsin.
 */
export function currencySuffix(locale: LocaleCode, currency = 'UZS'): string {
  if (currency !== 'UZS') return currency

  const suffix: Record<LocaleCode, string> = {
    'uz-Latn': "so'm",
    'uz-Cyrl': 'сўм',
    ru: 'сум',
    en: 'UZS',
  }

  return suffix[locale]
}

/**
 * Sana: «15.01.1991». Barcha tillarda bir xil — chalkashlik bo'lmasligi uchun.
 *
 * ⭐ Sana TOSHKENT mintaqasida hisoblanadi, brauzernikida emas
 * (`TASHKENT_TIME_ZONE` izohi): boshqa mintaqadagi noutbukda yoki
 * serverdagi brauzerda kun bir kunga siljib ketardi. Backend ham
 * aynan shu qoida bilan ishlaydi (`SystemClock.TashkentOffset`).
 *
 * `DateOnly` qiymatlari («1991-01-15») ham xavfsiz: ular UTC yarim
 * tunga bog'lanadi, Toshkent esa UTC dan DOIM oldinda — ya'ni kun
 * o'zgarmaydi. Brauzer mintaqasida esa `UTC-5` da turgan mashina
 * o'sha sanani «14.01.1991» deb chizardi.
 */
export function formatDate(value: Date | string, locale: LocaleCode): string {
  const date = typeof value === 'string' ? new Date(value) : value
  if (Number.isNaN(date.getTime())) return '—'

  const { year, month, day } = tashkentParts(date)

  return locale === 'en' ? `${year}-${month}-${day}` : `${day}.${month}.${year}`
}

/**
 * Sana va vaqt: «15.01.1991 09:30».
 *
 * Soat ham Toshkent mintaqasida: yozuv qachon qilinganini HR
 * O'ZINING soatiga qarab o'qiydi, brauzer qayerda turganiga emas.
 * Bo'laklar `formatDate` bilan BITTA formatlagichdan keladi, ya'ni
 * sana va soat bir-biriga mos bo'lmay qolishi mumkin emas.
 */
export function formatDateTime(value: Date | string, locale: LocaleCode): string {
  const date = typeof value === 'string' ? new Date(value) : value
  if (Number.isNaN(date.getTime())) return '—'

  const { hour, minute } = tashkentParts(date)

  return `${formatDate(date, locale)} ${hour}:${minute}`
}
