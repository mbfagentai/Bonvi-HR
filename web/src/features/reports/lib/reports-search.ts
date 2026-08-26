import { MAX_TURNOVER_MONTHS } from '../api/turnover-api'

/**
 * Hisobotlar sahifasining holati — manzil satrida (`?months=24`).
 *
 * NEGA komponent ichidagi `useState` emas (`employees-search.ts` bilan bir
 * xil sabab, lekin bu yerda urg'u boshqa joyda): bu ekranning butun mazmuni
 * — bitta tanlovga bog'liq bitta manzara. «Mana shu grafikka qara» deb
 * havola tashlash — hisobotning ASOSIY ishlatilish usuli, chunki uni
 * ko'radigan odam (asosiy rahbar) uni odatda HR yuborgan havoladan ochadi.
 * Komponent ichidagi holat bunday havolani umuman qila olmaydi: manzil
 * hammada bir xil, ekran esa har kimda boshqacha bo'lardi.
 *
 * Maydon IXTIYORIY va standart qiymat manzilga YOZILMAYDI: `?months=12`
 * — foydasi yo'q, shovqin ko'p. TanStack Router `undefined` maydonni
 * satrga umuman qo'shmaydi.
 */
export interface ReportsSearch {
  months?: TurnoverPeriod
}

/**
 * Davr almashtirgichining variantlari.
 *
 * NEGA aynan 6 / 12 / 24: 12 — yillik oqim, HR va rahbariyat aynan shu
 * raqamni «kadrlar oqimi» deb tushunadi; 6 — hozirgi tendensiyani
 * ko'rsatadigan qisqa oyna; 24 — server ruxsat beradigan eng uzun oraliq
 * (`MAX_TURNOVER_MONTHS`), ikki yilni yonma-yon qo'yib mavsumiylikni
 * ko'rish uchun. Oraliq qiymatlar (9, 18) hech qanday yangi savolga javob
 * bermaydi, almashtirgichni esa ikki barobar uzaytirardi.
 */
export const TURNOVER_PERIODS = [6, 12, MAX_TURNOVER_MONTHS] as const

export type TurnoverPeriod = (typeof TURNOVER_PERIODS)[number]

/**
 * Standart davr — bir yil.
 *
 * Server sukut bo'yicha ham aynan shuni beradi
 * (`GetTurnoverQueryHandler.DefaultMonths`), ya'ni parametrsiz manzil va
 * `?months=12` bir xil natija qaytaradi — shu sababli u manzilda saqlanmaydi.
 */
export const DEFAULT_PERIOD: TurnoverPeriod = 12

/**
 * Manzil satridan kelgan qiymatni tekshiradi.
 *
 * Hech qachon xato tashlamaydi: `?months=abc` yoki `?months=100` ni istalgan
 * odam yozishi mumkin (yoki eski havola shunday qolgan bo'ladi). Bunda ekran
 * «oq sahifa» bo'lib qolmasligi kerak — noto'g'ri qiymat shunchaki tashlab
 * yuboriladi va standart davr ishlaydi.
 *
 * Yon foydasi: server 400 (`hisobot.oy_notogri`) qaytaradigan qiymat bu
 * yerdayoq to'siladi, ya'ni HR «yuklab bo'lmadi» xatosini umuman ko'rmaydi.
 */
export function validateReportsSearch(search: Record<string, unknown>): ReportsSearch {
  return { months: readPeriod(search['months']) }
}

/** Manzil → almashtirgich. Manzilda qiymat bo'lmasa — standart davr. */
export function periodFromSearch(search: ReportsSearch): TurnoverPeriod {
  return search.months ?? DEFAULT_PERIOD
}

/**
 * Almashtirgich → manzil.
 *
 * Standart davr `undefined` bo'lib qaytadi va manzildan BUTUNLAY yo'qoladi
 * — aks holda odatiy ko'rinish ham `?months=12` bo'lib osilib qolardi.
 */
export function periodToSearch(period: TurnoverPeriod): ReportsSearch {
  return { months: period === DEFAULT_PERIOD ? undefined : period }
}

/**
 * Almashtirgichdan qaytgan SATRni davrga aylantiradi.
 *
 * NEGA satr: `SegmentedControl` ning tip parametri `T extends string` —
 * u qiymatni React kaliti va `aria-selected` uchun ishlatadi. Butun
 * boshqaruvni son qabul qiladigan qilib qayta yozish o'rniga (u
 * `shared/ui` da va boshqa to'rt joyda ishlatiladi) chegarada bitta
 * o'girish qiladi: bu yerda qoida bitta va ko'rinib turadi.
 *
 * Noma'lum satr standart davrga tushadi — manzildagi qiymat bilan bir xil
 * qoida (`validateReportsSearch`).
 */
export function toPeriod(value: string): TurnoverPeriod {
  const parsed = Number(value)

  return Number.isInteger(parsed) && isPeriod(parsed) ? parsed : DEFAULT_PERIOD
}

/*
 * Ruxsat etilgan qiymatlar massiv emas, KALITLI obyekt
 * (`employees-search.ts` dagi `SORT_KEYS` bilan bir xil naqsh va sabab):
 * `Record<Union, true>` yangi davr qo'shilganda TypeScript'ni shu yerni ham
 * to'ldirishga majbur qiladi, `includes` esa jimgina eskirib qolardi.
 */
const PERIODS: Record<TurnoverPeriod, true> = {
  6: true,
  12: true,
  24: true,
}

function isPeriod(value: number): value is TurnoverPeriod {
  return Object.hasOwn(PERIODS, value)
}

/**
 * Davr qiymati.
 *
 * Ikki manbadan kelishi mumkin va ikkalasi ham hisobga olingan: router
 * qiymatlarni JSON sifatida o'qiydi, ya'ni `?months=24` SON bo'lib keladi —
 * lekin qo'lda yozilgan yoki eski havolada u satr bo'lishi ham mumkin.
 */
function readPeriod(value: unknown): TurnoverPeriod | undefined {
  const parsed =
    typeof value === 'number'
      ? value
      : typeof value === 'string'
        ? Number(value)
        : Number.NaN

  if (!Number.isInteger(parsed) || !isPeriod(parsed)) return undefined

  return parsed === DEFAULT_PERIOD ? undefined : parsed
}
