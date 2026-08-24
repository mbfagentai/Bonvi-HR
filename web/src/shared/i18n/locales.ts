/**
 * Qo'llab-quvvatlanadigan tillar.
 *
 * O'zbek tili ikki yozuvda: lotin va kirill. Bu ikki alohida til emas —
 * bitta tilning ikki yozuvi. Shu sababli BCP-47 standarti bo'yicha
 * `uz-Latn` va `uz-Cyrl` deb belgilanadi (`uz` va `uz-UZ` emas).
 *
 * Nega ikkalasi ham kerak: Bonvi'ning butun mavjud hujjatlari —
 * «№91 бланк» Excel'i, buyruqlar, anketalar — kirillda. HR xodimlari
 * kirillda o'qiydi. Yoshroq xodimlar esa lotinni afzal ko'radi.
 */
export const LOCALES = [
  {
    code: 'uz-Latn',
    /** O'z tilidagi nomi — ro'yxatda doim shu ko'rinadi. */
    label: "O'zbekcha",
    /** Yozuvni ajratish uchun ikkilamchi qator. */
    script: 'Lotin',
    short: 'UZ',
    flag: '🇺🇿',
    intl: 'uz-Latn-UZ',
  },
  {
    code: 'uz-Cyrl',
    label: 'Ўзбекча',
    script: 'Кирилл',
    short: 'UZ',
    flag: '🇺🇿',
    intl: 'uz-Cyrl-UZ',
  },
  {
    code: 'ru',
    label: 'Русский',
    script: 'Russian',
    short: 'RU',
    flag: '🇷🇺',
    intl: 'ru-RU',
  },
  {
    code: 'en',
    label: 'English',
    script: 'International',
    short: 'EN',
    flag: '🇬🇧',
    intl: 'en-US',
  },
] as const

export type LocaleCode = (typeof LOCALES)[number]['code']
export type LocaleMeta = (typeof LOCALES)[number]

export const DEFAULT_LOCALE: LocaleCode = 'uz-Latn'

export const LOCALE_CODES = LOCALES.map((l) => l.code)

export function localeMeta(code: string): LocaleMeta {
  return LOCALES.find((l) => l.code === code) ?? LOCALES[0]
}

/**
 * `Intl` uchun tag. O'zbek yozuvlari uchun brauzer qo'llab-quvvatlashi
 * to'liq emas, shu sababli raqam formatlash `formatNumber` orqali
 * qo'lda bajariladi.
 */
export function intlTag(code: string): string {
  return localeMeta(code).intl
}
