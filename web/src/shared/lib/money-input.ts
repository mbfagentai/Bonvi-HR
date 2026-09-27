/**
 * Summa maydonining niqobi: «1500000» → «1 500 000».
 *
 * NEGA kerak (HR talabi, 2026-09-27): `type="number"` maydonda katta summa
 * «1500000000» bo'lib qo'shilib ketadi va bitta nolni ortiq yoki kam
 * bosganini ko'rib bo'lmaydi — oylik uchun bu xavfli xato. Endi raqamlar
 * YOZISH paytidayoq uch xonadan ajratiladi, xuddi ko'rsatishdagi kabi.
 *
 * Tashqariga KANONIK satr chiqadi — «1500000» yoki «1500000.5»: forma va
 * server o'zgarmaydi, `Number(value)` ham, `toAmount` ham avvalgidek
 * ishlaydi.
 */

/** Butun qismning eng ko'p xonalari — 999 999 999 999 999 dan kattasi yozilmaydi. */
const MAX_INTEGER_DIGITS = 15

/** Kasr qismining eng ko'p xonalari (tiyin). */
const MAX_FRACTION_DIGITS = 2

/**
 * Kiritilgan matndan kanonik summa: butun qism + ixtiyoriy «.» + ≤ 2 xona.
 *
 * Kasr ajratgichi qanday topiladi («.» va «,» ikkalasi ham bo'lishi mumkin):
 *
 * · Ajratgich BITTA bo'lsa — u kasr. Ortiqcha kasr xonalari tashlanadi:
 *   «5 000 000,555» → 5000000.55. ⚠️ Uni minglik deb olish mumkin emas —
 *   bitta ortiqcha bosish summani jimgina 1000 barobar oshirardi.
 * · Bir nechta BIR XIL ajratgich («1,500,000», «1.500.000») — mingliklar.
 * · Aralash («1.500.000,50», «1,500,000.50») — OXIRGISI kasr, qolganlari
 *   minglik.
 *
 * ⚠️ Kasrni oddiy tashlab yuborish ham mumkin emas: tuzatish oynasi mavjud
 * summani «5000000.5» ko'rinishida to'ldiradi va nuqtasiz u 50000005 ga —
 * 10 barobar kattaroqqa — aylanardi.
 */
export function moneyDigits(value: string): string {
  const separators = [...value.matchAll(/[.,]/gu)]
  const last = separators.at(-1)

  const isDecimal =
    last !== undefined &&
    (separators.length === 1 || separators.some((separator) => separator[0] !== last[0]))

  const integerSource = isDecimal ? value.slice(0, last.index) : value
  const fraction = isDecimal
    ? value.slice(last.index + 1).replaceAll(/\D/gu, '').slice(0, MAX_FRACTION_DIGITS)
    : null

  const integer = integerSource
    .replaceAll(/\D/gu, '')
    .replace(/^0+(?=\d)/u, '')
    .slice(0, MAX_INTEGER_DIGITS)

  if (fraction === null) return integer

  // Faqat ajratgichning o'zi terilgan («0,») — butun qism baribir bo'lishi kerak
  return `${integer === '' ? '0' : integer}.${fraction}`
}

/**
 * Kanonik summani ko'rinishga: «1500000.5» → «1 500 000,5».
 *
 * Kasr ajratgichi — vergul (o'zbek va rus yozuvidagi kabi); ichki qiymatda
 * esa nuqta, `Number` uni shunday o'qiydi.
 */
export function formatMoneyDigits(value: string): string {
  const [integer = '', fraction] = value.split('.')
  const grouped = integer.replaceAll(/\B(?=(\d{3})+(?!\d))/gu, ' ')

  return fraction === undefined ? grouped : `${grouped},${fraction}`
}
