/**
 * Nomzod fayllariga qo'yiladigan cheklovlar.
 *
 * Qiymatlar backend shartnomasidan ko'chirilgan
 * (`CandidateEndpoints` fayl endpoint'lari izohi): PDF, JPEG, PNG, WebP —
 * Word/Excel ATAYLAB yo'q; har bir faylga 20 MB chegara. Klient tomonda
 * takrorlash ortiqcha emas: 20 MB fayl serverga chiqib, u yerdan 400
 * bilan qaytguncha mobil internetda ancha vaqt ketadi — HR esa bu vaqtni
 * bekorga kutadi. Server tekshiruvi baribir asosiy himoya bo'lib qoladi
 * (`document-file.ts` dagi bilan bir xil mulohaza).
 *
 * FAYL NOMI VA EKSPORTLAR O'ZGARMADI, garchi nomzodda endi BITTA
 * rezyume emas, o'ntagacha fayl bo'lsa ham. Sabab: har bir cheklov
 * BITTA faylga tegishli (tur, hajm, o'lchov matni) va ular o'zgarmadi —
 * qayta nomlash faqat import satrlarini surardi va xato kodlari
 * (`nomzod.rezyume_*`) baribir «rezyume» bo'lib qoladi, chunki HR
 * biriktirilgan faylni shu so'z bilan ataydi (shartnoma qarori).
 *
 * NEGA `document-file.ts` DAN QAYTA ISHLATILMADI: chegaralar bugun bir
 * xil, lekin ular BOSHQA-BOSHQA qoidalar. Xodim hujjati kadrlar
 * bo'limining rasmiy fayli, nomzod fayli esa nomzodning o'zi yuborgan
 * hujjat — biri o'zgarganda ikkinchisi o'zgarishi shart emas.
 * Umumlashtirilsa, birini o'zgartirish ikkinchisini jimgina buzardi.
 */

/** Server MAZMUNIGA qarab aniqlaydigan turlar. `<input accept>` uchun ham shu ro'yxat. */
export const RESUME_TYPES = [
  'application/pdf',
  'image/jpeg',
  'image/png',
  'image/webp',
] as const

export const RESUME_ACCEPT = RESUME_TYPES.join(',')

/** 20 MB — BITTA faylning shartnomadagi eng katta hajmi. */
export const RESUME_MAX_BYTES = 20 * 1024 * 1024

/**
 * Nomzodga biriktiriladigan fayllarning eng ko'p soni — server qoidasi
 * (`Candidate.MaxFiles`).
 *
 * Bu domen INVARIANTI, klient qulayligi emas: server o'ntadan keyin
 * `nomzod.rezyume_kop` (400) qaytaradi. Klientda takrorlanishining
 * sababi — HR tanlagan 15 ta faylni birma-bir yuklab, oxirgi beshtasida
 * xato ko'rishdan ko'ra, tugmani o'chirib qo'yish va ortiqchasini
 * DARHOL aytish ma'qul.
 *
 * ⚠️ UMUMIY HAJM (server tomonda 60 MB) bu yerda TEKSHIRILMAYDI va bu
 * ataylab. Klient saqlanadigan hajmni BILA OLMAYDI: server rasmlarni
 * kichiklashtiradi, ya'ni 5 MB telefon surati diskda ~300 KB bo'lib
 * qoladi. Klient tomonda yig'indi hisoblansa, u server bajonidil qabul
 * qiladigan yuklashni RAD ETARDI — eng yomon turdagi xato, chunki
 * foydalanuvchi uni chetlab o'ta olmaydi. 60 MB qoidasi faqat
 * serverning ishi; klient `nomzod.rezyume_joy` javobini ko'rsatadi.
 */
export const MAX_CANDIDATE_FILES = 10

/**
 * Tanlangan fayl yaroqlimi.
 *
 * Xato MATNI emas, KODI qaytariladi — va ATAYLAB backend bilan bir xil
 * kod (`nomzod.rezyume_yaroqsiz` / `nomzod.rezyume_katta`): ular
 * allaqachon 4 tilga tarjima qilingan, ya'ni yangi tarjima kaliti
 * qo'shish shart emas va server ham, klient ham xatoni BIR XIL so'z
 * bilan aytadi. `null` — hammasi joyida.
 */
export function resumeFileError(file: File): string | null {
  // NEGA `type` bo'yicha: kengaytma yolg'on bo'lishi mumkin, brauzer esa
  // MIME turini faylning o'zidan aniqlaydi (`documentFileError` bilan bir xil).
  if (!(RESUME_TYPES as readonly string[]).includes(file.type)) return 'nomzod.rezyume_yaroqsiz'
  if (file.size > RESUME_MAX_BYTES) return 'nomzod.rezyume_katta'
  return null
}

/**
 * Baytni o'qiladigan hajmga ajratadi: 1 MB dan kichik fayl KB da,
 * qolgani MB da. Sonning o'zi qaytariladi — ko'rsatish chaqiruvchida
 * (`useLocale().num`, tildagi ajratgich bilan).
 *
 * NEGA `document-file.ts` dagi `fileSizeParts` import QILINMADI: u
 * xodim hujjatlari modulining ichki qoidasi va rezyume kartasi shu
 * sababli boshqa bo'limning `lib` iga bog'lanib qolmasligi kerak.
 * Uch qatorlik takror bu bog'liqlikdan arzonroq.
 */
export function resumeSizeParts(bytes: number): { value: number; unit: 'KB' | 'MB' } {
  if (bytes < 1024 * 1024) return { value: Math.round(bytes / 1024), unit: 'KB' }
  return { value: Math.round((bytes / 1024 / 1024) * 10) / 10, unit: 'MB' }
}
