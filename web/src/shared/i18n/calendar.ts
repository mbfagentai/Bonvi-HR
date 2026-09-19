import type { LocaleCode } from './locales'

/**
 * Taqvimning oy va hafta nomlari — har til uchun QO'LDA.
 *
 * <b>Nega <c>Intl</c> EMAS.</b> Sabab `formatNumber` izohidagi bilan
 * aynan bir xil: brauzerlar `uz-Latn-UZ` va `uz-Cyrl-UZ` teglarini
 * to'liq qo'llab-quvvatlamaydi. `Intl.DateTimeFormat('uz-Latn', …)`
 * ko'pincha ingliz nomlarini qaytaradi yoki kirill bilan lotinni
 * aralashtiradi — ya'ni taqvimning sarlavhasi o'zbek tilidagi
 * interfeysda «September» bo'lib chiqardi.
 *
 * <b>Nega tarjima JSON'ida emas.</b> Bu — 4 til × (12 + 12 + 7) = 124
 * kalit degani va ularning hech biri hech qachon alohida
 * o'zgartirilmaydi: oy nomlari tilning o'zgarmas qismi. `formatMoney`
 * dagi valyuta qo'shimchasi ham xuddi shu sababdan kodda turadi.
 *
 * ⚠️ Hafta DUSHANBADAN boshlanadi — O'zbekiston va Rossiyada shunday.
 * `Date.getUTCDay()` esa YAKSHANBADAN sanaydi, shu sababli tarjima
 * `mondayFirstWeekday()` da bajariladi (`shared/lib/date-value.ts`).
 */

/** To'liq oy nomlari — ro'yxatdan tanlash uchun. Yanvardan boshlab. */
export const MONTH_NAMES: Record<LocaleCode, readonly string[]> = {
  'uz-Latn': [
    'Yanvar', 'Fevral', 'Mart', 'Aprel', 'May', 'Iyun',
    'Iyul', 'Avgust', 'Sentabr', 'Oktabr', 'Noyabr', 'Dekabr',
  ],
  'uz-Cyrl': [
    'Январ', 'Феврал', 'Март', 'Апрел', 'Май', 'Июн',
    'Июл', 'Август', 'Сентябр', 'Октябр', 'Ноябр', 'Декабр',
  ],
  ru: [
    'Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь',
    'Июль', 'Август', 'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь',
  ],
  en: [
    'January', 'February', 'March', 'April', 'May', 'June',
    'July', 'August', 'September', 'October', 'November', 'December',
  ],
}

/**
 * Qisqa oy nomlari — taqvim sarlavhasida.
 *
 * Sarlavhada joy tor (oy, yil va ikkita o'q bitta qatorda), «Sentabr»
 * esa o'q tugmalarini siqib qo'yardi.
 */
export const MONTH_NAMES_SHORT: Record<LocaleCode, readonly string[]> = {
  'uz-Latn': ['Yan', 'Fev', 'Mar', 'Apr', 'May', 'Iyn', 'Iyl', 'Avg', 'Sen', 'Okt', 'Noy', 'Dek'],
  'uz-Cyrl': ['Янв', 'Фев', 'Мар', 'Апр', 'Май', 'Июн', 'Июл', 'Авг', 'Сен', 'Окт', 'Ноя', 'Дек'],
  ru: ['Янв', 'Фев', 'Мар', 'Апр', 'Май', 'Июн', 'Июл', 'Авг', 'Сен', 'Окт', 'Ноя', 'Дек'],
  en: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
}

/**
 * Hafta kunlari — DUSHANBADAN yakshanbagacha, taqvim ustunlari uchun.
 *
 * Ikki harfli emas, TILGA mos: o'zbekchada «CHO» va «SHA» uch harf
 * bo'ladi (ch va sh — bitta tovush), ularni ikki harfga qisqartirish
 * «CH» va «SH» ni farqlab bo'lmaydigan qilardi.
 */
export const WEEKDAY_NAMES_SHORT: Record<LocaleCode, readonly string[]> = {
  'uz-Latn': ['Du', 'Se', 'Cho', 'Pa', 'Ju', 'Sha', 'Yak'],
  'uz-Cyrl': ['Ду', 'Се', 'Чо', 'Па', 'Жу', 'Ша', 'Як'],
  ru: ['Пн', 'Вт', 'Ср', 'Чт', 'Пт', 'Сб', 'Вс'],
  en: ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'],
}
