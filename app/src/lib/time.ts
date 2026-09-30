/**
 * ⭐ ILOVANING YAGONA VAQT MINTAQASI — Toshkent (UTC+5).
 *
 * Simda (wire) hamma narsa UTC bo'lib yuradi, ekranda esa HAR DOIM
 * Toshkent vaqti. Bu loyihaning doimiy qoidasi va moduldagi eng ko'p
 * takrorlangan xato — ikkisi aralashsa, xodim «08:35 da keldim» deb
 * yozilgan kunni 03:35 bo'lib ko'radi.
 *
 * NEGA HR ilovasidagidek `Intl` + IANA nomi emas:
 *   · bu ilovaning poli iOS 12.2 (UX §F.1), u yerda `hourCycle`
 *     sozlamasi yo'q va `formatToParts` xulqi beqaror;
 *   · bu ilovada TARIXIY sana yo'q — faqat joriy davomat. O'zbekiston
 *     1992-yildan beri yozgi vaqtga o'tmaydi, ya'ni qat'iy +5 siljish
 *     bugungi ma'lumot uchun IANA jadvali bilan bir xil natija beradi.
 * HR ilovasida esa tug'ilgan sanalar 1950-yillarga boradi va u yerda
 * IANA nomi haqiqatan kerak — shu sababli qoida ikki joyda ikki xil,
 * va sababi shu izohda yozilgan.
 */
const TASHKENT_OFFSET_MINUTES = 5 * 60

export interface TimeParts {
  /** 2026 */
  year: number
  /** 1–12 */
  month: number
  /** 1–31 */
  day: number
  /** 0–23 */
  hour: number
  /** 0–59 */
  minute: number
  /** Dushanba = 1 … yakshanba = 7 (ISO). Taqvim dushanbadan boshlanadi. */
  weekday: number
}

/** Sanani Toshkent mintaqasidagi bo'laklarga ajratadi. */
export function tashkentParts(value: Date | string): TimeParts | null {
  const date = typeof value === 'string' ? parseServerDate(value) : value
  if (!date || isNaN(date.getTime())) return null

  const shifted = new Date(date.getTime() + TASHKENT_OFFSET_MINUTES * 60_000)
  const jsWeekday = shifted.getUTCDay() // yakshanba = 0

  return {
    year: shifted.getUTCFullYear(),
    month: shifted.getUTCMonth() + 1,
    day: shifted.getUTCDate(),
    hour: shifted.getUTCHours(),
    minute: shifted.getUTCMinutes(),
    weekday: jsWeekday === 0 ? 7 : jsWeekday,
  }
}

/**
 * Serverdan kelgan satrni `Date` ga aylantiradi.
 *
 * ⚠️ `2026-09-22T08:35:00` (mintaqa belgisisiz) — eski Safari uni
 * MAHALLIY vaqt deb o'qiydi, Chrome esa UTC deb. Backend UTC yuboradi,
 * shu sababli belgisi yo'q satrga `Z` qo'shiladi. Bu bitta harf
 * ikki brauzerda besh soatlik farq tug'dirardi.
 */
export function parseServerDate(value: string): Date | null {
  if (!value) return null

  const hasZone = /[zZ]$|[+-]\d{2}:?\d{2}$/.test(value)
  const normalized = value.indexOf('T') > 0 && !hasZone ? `${value}Z` : value
  const date = new Date(normalized)

  return isNaN(date.getTime()) ? null : date
}

/** «2026-09-22» — Toshkent kunining kaliti. */
export function tashkentIsoDate(value: Date | string): string {
  const parts = tashkentParts(value)
  if (!parts) return ''
  return `${parts.year}-${pad(parts.month)}-${pad(parts.day)}`
}

/** Bugungi kun, Toshkent bo'yicha. */
export function todayIso(): string {
  return tashkentIsoDate(new Date())
}

/** «2026-09» — oy kaliti. */
export function monthKey(year: number, month: number): string {
  return `${year}-${pad(month)}`
}

/** «2026-09» ni bo'laklarga ajratadi; noto'g'ri bo'lsa `null`. */
export function parseMonthKey(value: string | null | undefined): { year: number; month: number } | null {
  if (!value) return null
  const match = /^(\d{4})-(\d{2})$/.exec(value)
  if (!match) return null

  const year = Number(match[1])
  const month = Number(match[2])
  if (month < 1 || month > 12 || year < 2000 || year > 2100) return null

  return { year, month }
}

/** «2026-09-22» ni bo'laklarga ajratadi; noto'g'ri bo'lsa `null`. */
export function parseIsoDate(value: string | null | undefined): TimeParts | null {
  if (!value) return null
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value)
  if (!match) return null

  const year = Number(match[1])
  const month = Number(match[2])
  const day = Number(match[3])

  // Kun raqamini tekshirishning eng ishonchli usuli — uni UTC da qurib,
  // qaytib o'qish: «2026-02-31» o'zini 3-martga aylantirib fosh qiladi.
  const built = new Date(Date.UTC(year, month - 1, day))
  if (built.getUTCMonth() + 1 !== month || built.getUTCDate() !== day) return null

  const jsWeekday = built.getUTCDay()

  return {
    year,
    month,
    day,
    hour: 0,
    minute: 0,
    weekday: jsWeekday === 0 ? 7 : jsWeekday,
  }
}

/** Oydagi kunlar soni. */
export function daysInMonth(year: number, month: number): number {
  return new Date(Date.UTC(year, month, 0)).getUTCDate()
}

/** Oyning 1-kuni haftaning nechanchi kuni (dushanba = 1 … yakshanba = 7). */
export function firstWeekdayOfMonth(year: number, month: number): number {
  const jsWeekday = new Date(Date.UTC(year, month - 1, 1)).getUTCDay()
  return jsWeekday === 0 ? 7 : jsWeekday
}

/** Oyni bir qadam oldinga yoki orqaga suradi. */
export function shiftMonth(year: number, month: number, delta: number): { year: number; month: number } {
  const zeroBased = year * 12 + (month - 1) + delta
  return { year: Math.floor(zeroBased / 12), month: (zeroBased % 12) + 1 }
}

/** Ikki ISO sana orasidagi kunlar soni (ikkala chet ham kiradi). */
export function daysBetween(fromIso: string, toIso: string): number {
  const from = parseIsoDate(fromIso)
  const to = parseIsoDate(toIso)
  if (!from || !to) return 0

  const fromMs = Date.UTC(from.year, from.month - 1, from.day)
  const toMs = Date.UTC(to.year, to.month - 1, to.day)

  return Math.floor((toMs - fromMs) / 86_400_000) + 1
}

/** ISO sanani kunlarga suradi: «2026-09-22» + 3 → «2026-09-25». */
export function addDays(iso: string, days: number): string {
  const parts = parseIsoDate(iso)
  if (!parts) return iso

  const moved = new Date(Date.UTC(parts.year, parts.month - 1, parts.day + days))
  return `${moved.getUTCFullYear()}-${pad(moved.getUTCMonth() + 1)}-${pad(moved.getUTCDate())}`
}

export function pad(value: number): string {
  return value < 10 ? `0${value}` : String(value)
}
