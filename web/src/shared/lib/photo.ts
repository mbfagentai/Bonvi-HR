/**
 * Suratga qo'yiladigan cheklovlar — xodim kartasi uchun ham, hisob uchun ham.
 *
 * Qiymatlar backend shartnomasidan ko'chirilgan. Klient tomonda TAKRORLASH
 * ortiqcha emas: 20 MB fayl serverga chiqib, u yerdan 400 bilan qaytguncha
 * mobil internetda bir daqiqa ketadi — foydalanuvchi esa bu vaqtni bekorga
 * kutadi. Server tekshiruvi baribir asosiy himoya bo'lib qoladi.
 *
 * NEGA `shared` da: serverda ham quvur bitta (`SquarePhotoStore`), ya'ni
 * chegara ikkala rasm uchun bir xil. Fayl bo'lim ichida qolsa, ikkinchi
 * bo'lim uni nusxalab, bir kun kelib boshqa raqam yozib qo'yardi.
 */

/** Server qabul qiladigan turlar. `<input accept>` uchun ham shu ro'yxat. */
export const PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const

export const PHOTO_ACCEPT = PHOTO_TYPES.join(',')

/** 10 MB — shartnomadagi eng katta hajm. */
export const PHOTO_MAX_BYTES = 10 * 1024 * 1024

/**
 * Tanlangan fayl yaroqlimi.
 *
 * Xato MATNI emas, KODI qaytariladi — qolgan xatolar bilan bir xil yo'ldan
 * (`useErrorMessage`) tarjima qilinsin va til almashganda o'zi o'zgarsin.
 * `null` — hammasi joyida.
 */
export function photoFileError(file: File): string | null {
  // NEGA `type` bo'yicha: kengaytma yolg'on bo'lishi mumkin, brauzer esa
  // MIME turini faylning o'zidan aniqlaydi. Bo'sh `type` (noma'lum fayl)
  // ham ro'yxatga tushmaydi va rad etiladi.
  if (!(PHOTO_TYPES as readonly string[]).includes(file.type)) return 'rasm.turi'
  if (file.size > PHOTO_MAX_BYTES) return 'rasm.hajmi'
  return null
}

/** «2.4 MB» ni hosil qilish uchun — o'lchamni megabaytga o'giradi. */
export function toMegabytes(bytes: number): number {
  return Math.round((bytes / 1024 / 1024) * 10) / 10
}
