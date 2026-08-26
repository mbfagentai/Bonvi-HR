/**
 * Xodim hujjatlariga qo'yiladigan cheklovlar va yordamchi funksiyalar.
 *
 * Qiymatlar backend shartnomasidan ko'chirilgan (VAZIFA brifi): PDF,
 * JPEG, PNG, WebP — Word/Excel ATAYLAB yo'q; 20 MB chegara. Klient
 * tomonda TAKRORLASH ortiqcha emas: 20 MB fayl serverga chiqib, u yerdan
 * 400 bilan qaytguncha mobil internetda ancha vaqt ketadi — HR esa bu
 * vaqtni bekorga kutadi. Server tekshiruvi baribir asosiy himoya bo'lib
 * qoladi (`shared/lib/photo.ts` dagi bilan bir xil mulohaza).
 *
 * NEGA `shared/lib` da emas: rasm cheklovidan farqli, bu chegara faqat
 * xodim hujjatlari uchun — hisob sozlamalarida yoki boshqa bo'limda
 * ishlatilmaydi.
 *
 * Shu faylda `saveBlobAs` ham turardi va uning izohida «ikkinchi
 * ishlatuvchi paydo bo'lsa, `shared/lib` ga ko'chiriladi» deb yozilgan
 * edi. Shunday bo'ldi: xodimlar ro'yxatining Excel eksporti ham faylni
 * diskka saqlaydi, ya'ni saqlash endi hujjatlar tabining ishi emas —
 * u `shared/lib/download.ts` ga ko'chdi (fayl nomini `Content-Disposition`
 * dan ajratish bilan birga). Quyidagilar esa o'z joyida qoldi: ular
 * HUJJATNING qoidasi, saqlashning emas.
 */

/** Server qabul qiladigan turlar. `<input accept>` uchun ham shu ro'yxat. */
export const DOCUMENT_TYPES = [
  'application/pdf',
  'image/jpeg',
  'image/png',
  'image/webp',
] as const

export const DOCUMENT_ACCEPT = DOCUMENT_TYPES.join(',')

/** 20 MB — shartnomadagi eng katta hajm. */
export const DOCUMENT_MAX_BYTES = 20 * 1024 * 1024

/**
 * Tanlangan fayl yaroqlimi.
 *
 * Xato MATNI emas, KODI qaytariladi — va ATAYLAB backend bilan bir xil
 * kod (`hujjat.yaroqsiz` / `hujjat.katta`): ular allaqachon 4 tilga
 * tarjima qilingan, ya'ni bu yerga yangi tarjima kaliti qo'shish
 * shart emas — server ham, klient ham xatoni bir xil so'z bilan aytadi.
 * `null` — hammasi joyida.
 */
export function documentFileError(file: File): string | null {
  // NEGA `type` bo'yicha: kengaytma yolg'on bo'lishi mumkin, brauzer esa
  // MIME turini faylning o'zidan aniqlaydi (`photoFileError` dagi bilan bir xil mulohaza).
  if (!(DOCUMENT_TYPES as readonly string[]).includes(file.type)) return 'hujjat.yaroqsiz'
  if (file.size > DOCUMENT_MAX_BYTES) return 'hujjat.katta'
  return null
}

/**
 * Baytni o'qiladigan hajmga ajratadi: 1 MB dan kichik fayl KB da,
 * qolgani MB da — kichik skanlar «0,1 MB» emas, «84 KB» bo'lib chiqadi.
 * Sonning o'zi qaytariladi (butun yoki bitta kasr xonali) — ko'rsatish
 * chaqiruvchida (`useLocale().num`, tildagi ajratgich bilan).
 */
export function fileSizeParts(bytes: number): { value: number; unit: 'KB' | 'MB' } {
  if (bytes < 1024 * 1024) return { value: Math.round(bytes / 1024), unit: 'KB' }
  return { value: Math.round((bytes / 1024 / 1024) * 10) / 10, unit: 'MB' }
}
