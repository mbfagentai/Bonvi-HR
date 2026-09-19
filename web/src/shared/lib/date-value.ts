/**
 * Sana maydonining qiymati bilan ishlash — SOF satr va son arifmetikasi.
 *
 * ⭐ <b>Bu yerda `new Date(satr)` YO'Q</b> va bo'lmaydi ham. Sabab
 * `shared/lib/timezone.ts` da batafsil yozilgan: brauzer «2026-09-19»
 * ni UTC yarim tuniga bog'laydi va uni MAHALLIY mintaqada o'qiydi —
 * UTC-5 dagi mashinada kun bir kunga orqaga siljiydi. Maydon esa
 * ko'rsatish uchun emas, BAZAGA yoziladigan qiymatni tutadi.
 *
 * Shu sababli barcha amallar «yil, oy, kun» uchligi ustida bajariladi.
 * Yagona istisno — hafta kunini aniqlash: u `Date.UTC` orqali
 * hisoblanadi, ya'ni mintaqa umuman qatnashmaydi.
 *
 * <b>Shartnoma:</b> ISO qiymati — `«YYYY-MM-DD»`, bo'sh maydon — `''`.
 * Aynan shu format backend'ning `DateOnly` iga tushadi va
 * `employee-schemas.ts` dagi `ISO_DATE` tekshiruvi ham shuni kutadi.
 */

/** Yil / oy / kun — oy 1 dan 12 gacha (JavaScript'dagi 0 dan emas). */
export interface DateParts {
  year: number
  month: number
  day: number
}

/** Ekranda ko'rsatiladigan format — barcha tillarda bir xil. */
export const DATE_MASK = 'dd.mm.yyyy'

/**
 * ISO qiymatni bo'laklarga ajratadi. Yaroqsiz bo'lsa — `null`.
 *
 * Tekshiruv shaklga qarab emas, MA'NOGA qarab: «2026-02-31» shakl
 * jihatidan to'g'ri, lekin bunday kun yo'q va u taqvimda mart oyiga
 * sakrab ketardi.
 */
export function parseIso(iso: string): DateParts | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/u.exec(iso)
  if (match === null) return null

  const year = Number(match[1])
  const month = Number(match[2])
  const day = Number(match[3])

  if (month < 1 || month > 12) return null
  if (day < 1 || day > daysInMonth(year, month)) return null

  return { year, month, day }
}

/** Bo'laklardan ISO qiymat yasaydi: `{2026, 9, 19}` → «2026-09-19». */
export function toIso({ year, month, day }: DateParts): string {
  return `${pad(year, 4)}-${pad(month, 2)}-${pad(day, 2)}`
}

/** Oydagi kunlar soni — kabisa yilini ham hisobga oladi. */
export function daysInMonth(year: number, month: number): number {
  // Keyingi oyning «0-kuni» — shu oyning oxirgi kuni. UTC'da, ya'ni
  // mintaqa qatnashmaydi.
  return new Date(Date.UTC(year, month, 0)).getUTCDate()
}

/**
 * Oyning birinchi kuni haftaning nechanchi ustuniga tushadi (0 — dushanba).
 *
 * `getUTCDay()` yakshanbani `0` deb beradi, bizda esa hafta
 * DUSHANBADAN boshlanadi — shu sababli siljitiladi.
 */
export function mondayFirstWeekday(year: number, month: number): number {
  const sundayFirst = new Date(Date.UTC(year, month - 1, 1)).getUTCDay()

  return (sundayFirst + 6) % 7
}

/**
 * Oyni siljitadi. Kun oydan oshib ketsa, oyning OXIRGI kuniga qisiladi.
 *
 * 31-yanvardan bir oy oldinga — 28 yoki 29-fevral. Qisilmasa, brauzer
 * uni 3-martga tashlab yuborardi va foydalanuvchi «‹» tugmasini bosib
 * turib mutlaqo boshqa oyga tushib qolardi.
 */
export function addMonths(parts: DateParts, delta: number): DateParts {
  const total = parts.year * 12 + (parts.month - 1) + delta
  const year = Math.floor(total / 12)
  const month = (total % 12) + 1

  return { year, month, day: Math.min(parts.day, daysInMonth(year, month)) }
}

/**
 * ISO qiymatlarni solishtiradi — satr sifatida.
 *
 * `«YYYY-MM-DD»` da leksikografik tartib xronologik tartib bilan mos
 * tushadi (bo'laklar to'ldirilgan va kattadan kichikka yozilgan), ya'ni
 * sanani obyektga aylantirish shart emas. Bo'sh satr hech qachon
 * chegara sifatida kelmaydi — chaqiruvchi uni oldin tekshiradi.
 */
export function isoWithin(iso: string, min?: string, max?: string): boolean {
  if (min !== undefined && min !== '' && iso < min) return false
  if (max !== undefined && max !== '' && iso > max) return false

  return true
}

/**
 * ISO qiymatni ekran formatiga o'tkazadi: «2026-09-19» → «19.09.2026».
 *
 * <b>Format barcha tillarda BIR XIL</b> va bu ataylab. Ro'yxatlardagi
 * `formatDate` ingliz tilida `«2026-09-19»` ko'rsatadi, lekin u FAQAT
 * o'qish uchun; bu yerda esa maydon TAHRIRLANADI — ikki xil niqob
 * ikki xil terish qoidasi, ikki xil tekshiruv va ikki xil xato holati
 * degani bo'lardi. Tizimning asosiy foydalanuvchisi — o'zbek va rus
 * tilidagi HR, ikkalasida ham kun birinchi turadi.
 */
export function isoToDisplay(iso: string): string {
  const parts = parseIso(iso)
  if (parts === null) return ''

  return `${pad(parts.day, 2)}.${pad(parts.month, 2)}.${pad(parts.year, 4)}`
}

/**
 * Foydalanuvchi TERGAN matnni ISO qiymatga o'tkazadi.
 *
 * To'liq va ma'noli sana bo'lmasa — `null`: maydon hali terilayotgan
 * bo'lishi mumkin («19.09.20»), va uni har harfda xatoga chiqarish
 * terishni imkonsiz qilardi. Chaqiruvchi bo'sh natijani «hali
 * tayyor emas» deb tushunadi.
 */
export function displayToIso(text: string): string | null {
  const match = /^(\d{2})\.(\d{2})\.(\d{4})$/u.exec(text.trim())
  if (match === null) return null

  const iso = `${match[3]}-${match[2]}-${match[1]}`

  return parseIso(iso) === null ? null : iso
}

/**
 * Terilayotgan matnga nuqtalarni O'ZI qo'yadi: «19092026» → «19.09.2026».
 *
 * Nuqtani qo'lda terish kerak bo'lsa, HR ning har sanasi ikki
 * qo'shimcha bosish degani — kuniga o'nlab marta. Raqamdan boshqa
 * hamma narsa tashlanadi, uzunlik esa sakkiz raqam bilan cheklanadi:
 * ortiqchasi jimgina yo'qoladi, ya'ni maydon hech qachon buzilgan
 * ko'rinishga tushmaydi.
 */
export function maskDateInput(raw: string): string {
  const digits = raw.replaceAll(/\D/gu, '').slice(0, 8)

  if (digits.length <= 2) return digits
  if (digits.length <= 4) return `${digits.slice(0, 2)}.${digits.slice(2)}`

  return `${digits.slice(0, 2)}.${digits.slice(2, 4)}.${digits.slice(4)}`
}

function pad(value: number, length: number): string {
  return String(value).padStart(length, '0')
}
