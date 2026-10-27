import { problemCode } from '@/shared/api/problem-details'

/**
 * Xatodan tarjima qilinadigan kodni ajratib oladi.
 *
 * Mantiqning o'zi `shared/api/problem-details.ts` da: u HTTP shartnomasiga
 * tegishli va uchta bo'limda bir xil. Shu nom esa qoladi — modul kodi uni
 * o'nlab joyda chaqiradi.
 *
 * Bo'sh satr — «kod noma'lum»: `useErrorMessage` uni umumiy
 * «Nimadir noto'g'ri ketdi» ga aylantiradi. Xatoni yashirmaymiz,
 * lekin foydalanuvchiga texnik matn ham ko'rsatmaymiz.
 */
export function catalogErrorCode(error: unknown): string {
  return problemCode(error)
}

/**
 * Qaysi xato kodi qaysi maydonga tegishli.
 *
 * NEGA kerak: «Bunday nom band» degan xabar formaning tepasida emas,
 * aynan nom maydoni ostida turgani ma'qul — foydalanuvchi nimani
 * tuzatishni izlab yurmaydi. Ro'yxatda yo'q kodlar umumiy ogohlantirishga
 * tushadi (masalan `bolim.halqa` — u bitta maydonga tegishli emas).
 */
export const CATALOG_ERROR_FIELD: Readonly<Record<string, string>> = {
  'malumotnoma.nom_bosh': 'name',
  'malumotnoma.nom_uzun': 'name',
  'malumotnoma.nom_band': 'name',
  'qayerdan.nom_bosh': 'name',
  'bolim.nom_bosh': 'name',
  'lavozim.nom_bosh': 'name',
  // `lavozim.daraja` ataylab yo'q: daraja maydoni formadan olib tashlangan,
  // shu sababli bu xato (agar kelsa) umumiy ogohlantirishga tushadi
  'lavozim.sinov_muddati': 'probationDays',
}
