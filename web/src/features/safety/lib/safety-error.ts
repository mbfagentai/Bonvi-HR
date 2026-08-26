import { problemCode } from '@/shared/api/problem-details'

/**
 * Xatodan tarjima qilinadigan kodni ajratib oladi.
 *
 * `catalog-error.ts` / `employee-error.ts` dagi bilan bir xil naqsh: mantiq
 * `shared/api/problem-details.ts` da, bo'sh satr — «kod noma'lum»
 * (`useErrorMessage` uni umumiy xabarga aylantiradi).
 */
export function safetyErrorCode(error: unknown): string {
  return problemCode(error)
}

/**
 * Qaysi xato kodi yaratish/tahrirlash formasining qaysi maydoniga tegishli.
 *
 * Ro'yxatda yo'q kodlar (masalan `baxtsiz_hodisa.topilmadi`) umumiy
 * ogohlantirishga tushadi — ular bitta maydonga emas, butun yozuvga tegishli.
 */
export const SAFETY_ERROR_FIELD: Readonly<Record<string, string>> = {
  'baxtsiz_hodisa.xodim_yoq': 'employeeId',
  'baxtsiz_hodisa.joy_bosh': 'location',
  'baxtsiz_hodisa.tavsif_bosh': 'description',
  'baxtsiz_hodisa.lavozim_topilmadi': 'positionId',
  'baxtsiz_hodisa.masul_topilmadi': 'responsibleEmployeeId',
}

/** Komissiya xulosasi oynasidagi yagona maydon — `/conclude` xatosi shunga tushadi. */
export const CONCLUDE_ERROR_FIELD: Readonly<Record<string, string>> = {
  'baxtsiz_hodisa.xulosa_bosh': 'conclusion',
}
