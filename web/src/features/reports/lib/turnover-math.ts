import type { TurnoverMonth } from '../api/turnover-api'

/**
 * ⭐ NEGA HISOB SHU YERDA, SERVERDA EMAS
 *
 * Backend foizni ATAYLAB qaytarmaydi va buni endpoint izohida ochiq
 * yozgan: «frontend uni "Left / o'rtacha shtat" formulasi bilan o'zi
 * hisoblaydi — kerakli ikkala son javobda bor». Ya'ni server FAKTni
 * (nechta odam, qaysi oyda) beradi, TALQIN esa ekranga tegishli: bir xil
 * ma'lumotdan oylik oqim ham, davriy oqim ham chiqadi va ularning
 * qaysi biri kerakligini ekran biladi.
 */

/** Davr bo'yicha yig'ma ko'rsatkichlar. */
export interface TurnoverTotals {
  hired: number
  left: number
  /** Davr bo'yicha o'rtacha shtat — foizning maxraji. */
  averageHeadcount: number
  /**
   * Kadrlar oqimi, foizda.
   *
   * `undefined` — «hisoblab BO'LMAYDI», nol emas. Farqi muhim: nol
   * «hech kim ketmadi» degani, `undefined` esa «savolning o'zi ma'nosiz»
   * (pastdagi izohga qarang).
   */
  rate: number | undefined
}

/**
 * Butun davr bo'yicha kadrlar oqimi.
 *
 * ┌─ FORMULA ────────────────────────────────────────────────────────┐
 * │  rate = jami ketganlar / o'rtacha shtat × 100                     │
 * │  o'rtacha shtat = har oyning (boshi + oxiri) / 2 qiymatlarining   │
 * │                   o'rtachasi                                      │
 * └───────────────────────────────────────────────────────────────────┘
 *
 * NEGA maxraj «(davr boshidagi shtat + davr oxiridagi shtat) / 2» EMAS:
 * u butun oraliqni ikkita nuqta bilan almashtiradi va o'rtada bo'lgan
 * hamma narsani ko'rmaydi. Yozda 60 kishiga chiqib, kuzda yana 35 ga
 * qaytgan mavsumiy korxonada bunday maxraj 35 chiqadi — yozda ketgan
 * o'nlab odam esa shu 35 ga bo'linib, oqimni ikki barobar shishirib
 * yuboradi. Oylik o'rtachalarning o'rtachasi har bir oyni bir xil
 * og'irlik bilan hisobga oladi va shishishni yo'qotadi.
 *
 * NEGA YILLIKKA KELTIRILMAYDI (× 12 / oy soni): unda 6 oylik oyna
 * «yiliga 24%» deb ko'rsatardi — bu FAKT emas, BASHORAT. Bir marta
 * bo'lgan qisqartirish butun yilga cho'zilib, hisobot bo'lmagan narsani
 * bo'lgandek qilib qo'yardi. Bu yerda esa har doim aynan tanlangan davr
 * uchun haqiqiy son turadi, davr oralig'i esa kartaning ostida yozilgan —
 * ya'ni raqamning nimaga tegishli ekani ekrandan o'qiladi.
 *
 * ⚠️ Shu sababli 6, 12 va 24 oylik foizlar bir-biriga TAQQOSLANMAYDI va
 * taqqoslanishi ham kerak emas: almashtirgich davrni emas, SAVOLni
 * almashtiradi.
 */
export function periodTotals(months: readonly TurnoverMonth[]): TurnoverTotals {
  let hired = 0
  let left = 0
  let headcountSum = 0

  for (const month of months) {
    hired += month.hired
    left += month.left
    headcountSum += monthAverageHeadcount(month)
  }

  const averageHeadcount = months.length === 0 ? 0 : headcountSum / months.length

  return { hired, left, averageHeadcount, rate: rate(left, averageHeadcount) }
}

/**
 * Bitta oyning oqimi — grafikdagi izoh oynasi uchun.
 *
 * Maxraj shu oyning o'z o'rtachasi: oy davomida shtat o'zgargan bo'lsa
 * (masalan 40 dan 30 ga), ketganlar 40 ga ham, 30 ga ham emas, 35 ga
 * bo'linadi — aks holda bir xil ketish soni oy boshidagi va oxiridagi
 * songa qarab ikki xil foiz berardi.
 */
export function monthRate(month: TurnoverMonth): number | undefined {
  return rate(month.left, monthAverageHeadcount(month))
}

function monthAverageHeadcount(month: TurnoverMonth): number {
  return (month.headcountStart + month.headcountEnd) / 2
}

/**
 * Foizning o'zi.
 *
 * ⚠️ MAXRAJ NOL BO'LGANDA FOIZ UMUMAN QAYTMAYDI — na «0%», na «∞».
 *
 * Bo'sh baza (hali bironta xodim kiritilmagan, yoki oraliq tizim
 * ishga tushishidan oldingi oylarga tushgan) — real holat: dev bazasida
 * ham eski oylarda shtat noldan boshlanadi. «0%» bunday holatda YOLG'ON
 * bo'lardi: u «oqim yo'q, hammasi joyida» degan ma'no beradi, aslida esa
 * o'lchanadigan narsaning o'zi yo'q. Chaqiruvchi tomon `undefined` ni
 * ko'rib ko'rsatkichni butunlay chizmaydi («—») — bu yagona to'g'ri javob.
 *
 * `Number.isFinite` qo'shimcha to'siq: maxraj kasr bo'lgani uchun
 * (o'rtachalar) u nolga juda yaqin, lekin nol bo'lmagan qiymat bo'lishi
 * nazariy jihatdan mumkin va u `Infinity` bermasa ham ulkan son berardi.
 */
function rate(left: number, averageHeadcount: number): number | undefined {
  if (averageHeadcount <= 0) return undefined

  const value = (left / averageHeadcount) * 100

  return Number.isFinite(value) ? value : undefined
}

/**
 * Ulush, foizda: bitta sababning butun davrdagi ketishlarga nisbati.
 *
 * Maxraj nol bo'lganda nol qaytadi — bu yerda `undefined` shart emas:
 * yig'indi nol bo'lsa, ro'yxatda umuman qator bo'lmaydi va bu funksiya
 * chaqirilmaydi ham.
 */
export function share(count: number, total: number): number {
  return total <= 0 ? 0 : (count / total) * 100
}
