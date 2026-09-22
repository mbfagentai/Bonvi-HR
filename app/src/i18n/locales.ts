/**
 * Qo'llab-quvvatlanadigan tillar — HR ilovasidagi ro'yxatning aynan o'zi,
 * lekin NUSXA qilib ko'chirilgan (UX §0.1): bu ilova HR ilovasidan bitta
 * ham fayl import qilmaydi.
 *
 * O'zbek tili ikki yozuvda: lotin va kirill. Bu bitta tilning ikki yozuvi,
 * shu sababli BCP-47 bo'yicha `uz-Latn` va `uz-Cyrl`.
 */
export const LOCALES = [
  { code: 'uz-Latn', label: 'O‘zbekcha', script: 'Lotin' },
  { code: 'uz-Cyrl', label: 'Ўзбекча', script: 'Кирилл' },
  { code: 'ru', label: 'Русский', script: 'Russian' },
  { code: 'en', label: 'English', script: 'International' },
] as const

export type LocaleCode = (typeof LOCALES)[number]['code']

export const DEFAULT_LOCALE: LocaleCode = 'uz-Latn'

export const LOCALE_CODES: readonly LocaleCode[] = LOCALES.map((l) => l.code)

export function isLocale(value: string): value is LocaleCode {
  return LOCALE_CODES.indexOf(value as LocaleCode) >= 0
}

/**
 * `<html lang>` uchun teg. Ekran o'quvchi to'g'ri ovozni tanlashi uchun
 * til ALMASHGANDA ham yangilanadi (UX §H.4).
 */
export function htmlLang(code: LocaleCode): string {
  return code
}
