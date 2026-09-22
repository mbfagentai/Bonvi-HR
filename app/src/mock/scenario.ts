import { readStorage, writeStorage } from '@/lib/storage'

/**
 * Sinov senariysi — ekranlarni ko'z bilan tekshirish uchun.
 *
 * NEGA kerak: belgilashning to'qqizta holati bor (UX §C.2) va ularning
 * yettitasi faqat server rad javobida ko'rinadi. Haqiqiy server
 * ulanmaguncha ularni ko'rsatishning yagona yo'li — mock'ga «bu safar
 * shunday javob ber» deb aytish.
 *
 * Brauzer konsolidan almashtiriladi:
 *     localStorage.setItem('bonvi.ilova.senariy', 'uzoq')
 *     location.reload()
 *
 * ⚠️ Bu fayl mock bilan BIRGA o'chadi. Haqiqiy mijoz ulanganda
 * `src/mock/` butunlay olib tashlanadi va bu bilan senariy ham ketadi.
 */
export type CheckInScenario =
  /** Hamma narsa yaxshi — belgi qabul qilinadi. */
  | 'ok'
  /** `davomat.obyektdan_uzoq` */
  | 'uzoq'
  /** `davomat.joylashuv_aniq_emas` */
  | 'aniqemas'
  /** `davomat.takroriy_belgi` */
  | 'takror'
  /** `davomat.mobil_ruxsat_yoq` — hisobda bayroq o'chirilgan. */
  | 'ruxsatyoq'
  /** 5xx */
  | 'server'
  /** So'rov ketdi, javob kelmadi — UX §C.5 dagi eng nozik holat. */
  | 'javobyoq'

const KEY = 'bonvi.ilova.senariy'

const ALL: readonly CheckInScenario[] = [
  'ok',
  'uzoq',
  'aniqemas',
  'takror',
  'ruxsatyoq',
  'server',
  'javobyoq',
]

export function readScenario(): CheckInScenario {
  const stored = readStorage(KEY)
  const found = ALL.filter((item) => item === stored)[0]
  return found ?? 'ok'
}

export function writeScenario(value: CheckInScenario): void {
  writeStorage(KEY, value)
}
