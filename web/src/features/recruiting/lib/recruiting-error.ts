import { problemCode } from '@/shared/api/problem-details'

/**
 * Xatodan tarjima qilinadigan kodni ajratib oladi.
 *
 * Mantiqning o'zi `shared/api/problem-details.ts` da — u HTTP
 * shartnomasiga tegishli va endi oltita bo'limda bir xil. Shu nom esa
 * qoladi (`catalog-error.ts`, `safety-error.ts` bilan bir xil naqsh):
 * modul kodi uni o'nlab joyda chaqiradi va «qaysi bo'limning xatosi»
 * degan savol chaqiruv joyida ko'rinib tursin.
 *
 * ⚠️ Rekruting xato kodlari (`errors.nomzod.*`, `errors.vakansiya.*`,
 * `errors.taklif.*`, `errors.bosqich.*`) TO'RT TILGA ALLAQACHON tarjima
 * qilingan — `useErrorMessage` ularni o'zi topadi, bu yerda hech narsa
 * takrorlanmaydi.
 */
export function recruitingErrorCode(error: unknown): string {
  return problemCode(error)
}

/**
 * Qaysi xato kodi qaysi maydonga tegishli.
 *
 * NEGA kerak: «Bunday nomli bosqich allaqachon bor» degan xabar
 * formaning tepasida emas, aynan nom maydoni ostida turgani ma'qul —
 * foydalanuvchi nimani tuzatishni izlab yurmaydi (`catalog-error.ts`
 * dagi bilan bir xil sabab). Ro'yxatda yo'q kodlar umumiy
 * ogohlantirishga tushadi.
 *
 * Bu yerda FAQAT formaning o'z maydoniga tushadigan kodlar bor.
 * Masalan `vakansiya.qabul_qilmaydi` ataylab yo'q: u bitta maydonga
 * emas, butun amalga tegishli — «bu vakansiya ochiq emas» degani.
 */
export const RECRUITING_ERROR_FIELD: Readonly<Record<string, string>> = {
  // ── bosqich ma'lumotnomasi
  'bosqich.nom_bosh': 'name',
  'bosqich.nom_band': 'name',

  // ── vakansiya
  'vakansiya.lavozim_yoq': 'positionId',
  'vakansiya.lavozim_topilmadi': 'positionId',
  'vakansiya.yuridik_shaxs_yoq': 'legalEntityId',
  'vakansiya.yuridik_shaxs_topilmadi': 'legalEntityId',
  'vakansiya.bolim_topilmadi': 'departmentId',
  'vakansiya.masul_topilmadi': 'responsibleEmployeeId',
  'vakansiya.orin_soni': 'openings',
  'vakansiya.yopish_sanasi': 'closedAt',

  // ── nomzod
  'ism.familiya_bosh': 'lastName',
  'ism.ism_bosh': 'firstName',
  'ism.uzun': 'lastName',
  'nomzod.tugilgan_sana': 'birthDate',
  'nomzod.vakansiya_yoq': 'vacancyId',
  /*
    Telefon xatolari (`telefon.*`) — `Phone` qiymat obyektidan keladi va
    ular xodimlar modulida ham xuddi shu maydonga tushadi. Nomzodda
    telefon IXTIYORIY, lekin kiritilgan bo'lsa formati tekshiriladi.
  */
  'telefon.uzunlik': 'phone',
  'telefon.mamlakat_kodi': 'phone',
  'telefon.operator': 'phone',

  // ── taklif
  'taklif.boshlash_sanasi': 'startDate',
  'taklif.muddat_sanasi': 'expiresAt',
  'pul.manfiy': 'salaryAmount',
  'pul.aniqlik': 'salaryAmount',
  'pul.valyuta': 'salaryAmount',

  // ── taklif qabul qilinganda: ishga qabulning o'z xatolari
  'pinfl.uzunlik': 'pinfl',
  'pinfl.birinchi_raqam': 'pinfl',
  'pinfl.sana': 'pinfl',
  'pinfl.nazorat_raqami': 'pinfl',
  'pasport.uzunlik': 'passport',
  'pasport.seriya': 'passport',
  'pasport.raqam': 'passport',
  'mehnat.stavka': 'rate',
  'sinov.muddat': 'probationDays',
  'sinov.majburiy': 'probationDays',
}
