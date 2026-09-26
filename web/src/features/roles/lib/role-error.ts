/**
 * Qaysi xato kodi rol NOMI maydoniga tegishli (`user-error.ts` dagi
 * `USER_ERROR_FIELD` bilan bir xil sabab): «bu nom band» degan xabar
 * oynaning tepasida emas, aynan nom maydoni ostida turgani ma'qul.
 * Qolgan kodlar (`rol.oz_roli`, `rol.ortiqcha_ruxsat`, …) butun rolga
 * tegishli va umumiy ogohlantirishga tushadi.
 *
 * Kodlar backend'nikilar bilan AYNAN bir xil — klient tekshiruvi ham
 * ularni ishlatadi, ya'ni ikki tomon xatosi bir xil ko'rinadi.
 */
export const ROLE_NAME_ERRORS: ReadonlySet<string> = new Set([
  'rol.nom_bosh',
  'rol.nom_uzun',
  'rol.nom_band',
])
