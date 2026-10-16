/**
 * Foiz maydonining niqobi: faqat raqam va bitta kasr ajratgichi.
 *
 * Tashqariga KANONIK satr chiqadi — «4», «2.5», «0.25»: forma uni
 * `Number(value)` bilan o'qiydi, server `decimal` qabul qiladi. Ekranda
 * kasr ajratgichi tilga qarab ko'rsatiladi (`displayPercent`).
 *
 * Chegaralar serverdagi bilan bir xil (`Incentive.MaxPercent`,
 * `Incentive.PercentScale`): ko'pi bilan 100, ikki kasr xona.
 */

/** Foizning eng katta qiymati — server `Incentive.MaxPercent`. */
export const MAX_PERCENT = 100

/** Kasr xonalar soni — server `Incentive.PercentScale` (`numeric(5,2)`). */
const MAX_FRACTION_DIGITS = 2

/** Butun qism — «100» dan uzunrog'i hech qachon to'g'ri bo'lmaydi. */
const MAX_INTEGER_DIGITS = 3

/**
 * Kiritilgan matndan kanonik foiz: «2,5» → «2.5», «4 %» → «4», «» → «».
 *
 * Yaroqsiz kiritish uchun `null` — maydon oldingi qiymatida qoladi:
 * harf, ikkinchi kasr ajratgichi, uchdan ortiq butun yoki ikkidan ortiq
 * kasr xona, 100 dan katta son. ⚠️ Ortiqchasini KESIB qolgan qismini
 * qabul qilish mumkin emas: «1000» nusxa qo'yilganda jimgina «100 %»,
 * «4,555» — «4,55 %» bo'lib yozilardi.
 */
export function percentDigits(value: string): string | null {
  const raw = value.replaceAll(/[\s%]/gu, '')
  if (raw === '') return ''
  if (/[^\d.,]/u.test(raw)) return null

  const parts = raw.split(/[.,]/u)
  if (parts.length > 2) return null

  const integer = (parts[0] ?? '').replace(/^0+(?=\d)/u, '')
  const fraction = parts[1]

  if (integer.length > MAX_INTEGER_DIGITS) return null
  if (fraction !== undefined && fraction.length > MAX_FRACTION_DIGITS) return null

  const canonical = fraction === undefined ? integer : `${integer === '' ? '0' : integer}.${fraction}`
  return Number(canonical) > MAX_PERCENT ? null : canonical
}

/** Kanonik qiymatni ko'rsatish uchun: o'zbek va rus tilida kasr — vergul. */
export function displayPercent(canonical: string, decimalComma: boolean): string {
  return decimalComma ? canonical.replace('.', ',') : canonical
}

/** Kanonik satr → son; bo'sh yoki chala («4.») qiymat uchun `null`. */
export function parsePercent(canonical: string): number | null {
  if (canonical === '' || canonical.endsWith('.')) return null
  const value = Number(canonical)
  return Number.isFinite(value) ? value : null
}

/** Server qabul qiladigan foizmi: 0 dan katta, ko'pi bilan 100. */
export function isValidPercent(value: number | null): value is number {
  return value !== null && value > 0 && value <= MAX_PERCENT
}
