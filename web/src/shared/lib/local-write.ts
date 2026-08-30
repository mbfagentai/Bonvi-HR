/**
 * «Shu brauzerda hozirgina yozuv amali bajarildi» belgisi.
 *
 * ⭐ <b>Nima uchun kerak.</b> Jonli bildirishnomalar oqimi
 * (`useNotificationStream`) bazadagi HAR QANDAY o'zgarishda ishga
 * tushadi — shu jumladan foydalanuvchining O'ZI qilgan o'zgarishida.
 * Usiz HR nomzod qo'shib «Saqlash» bosgan zahoti ekranning tepasidan
 * «Yangi nomzod: …» degan xabar tushardi. Bu xabar hech narsa
 * bildirmaydi (u endigina o'zi kiritdi) va aynan ishlayotgan odamni
 * bo'ladi — foydalanuvchi so'ragan qoidaning teskarisi.
 *
 * <b>Nega vaqt, bayroq emas.</b> Yozuv va oqimdan kelgan javob
 * orasida bir necha yuz millisekund bo'ladi (server qayta hisoblaydi,
 * javob tarmoqdan o'tadi). Bayroqni qachon o'chirishni bilib
 * bo'lmasdi; vaqt esa o'zi «o'chadi».
 *
 * <b>Nega bu faqat XABARNI to'xtatadi.</b> Sanoq, qo'ng'iroq
 * animatsiyasi va ro'yxatning o'zi baribir yangilanadi — ular
 * to'g'ri va ular hech kimni bo'lmaydi. To'xtatiladigan yagona narsa
 * — ekran ustidagi karta.
 *
 * <b>Chegarasi ochiq aytiladi:</b> aynan o'sha lahzada BOSHQA odam
 * yoki bot yozuv kiritsa, uning xabari ham tushib qoladi. Ehtimoli
 * kichik, zarari esa kichikroq: bildirishnoma yo'qolmaydi — u
 * qo'ng'iroq ostidagi ro'yxatda va sanoqda qoladi, qo'ng'iroqning
 * o'zi esa chayqaladi.
 */
let lastWriteAt = 0

/** Yozuv amali muvaffaqiyatli tugadi. `providers.tsx` chaqiradi. */
export function markLocalWrite(): void {
  lastWriteAt = Date.now()
}

/**
 * Oxirgi yozuvdan beri <paramref name="withinMs"/> dan kam vaqt o'tdimi.
 *
 * 4 soniya: server qayta hisoblab, javobni yuborishi uchun yetarli
 * (odatda 200–400 ms), lekin keyingi mustaqil o'zgarishni yashirib
 * qo'yadigan darajada uzoq emas.
 */
export function isRecentLocalWrite(withinMs = 4_000): boolean {
  return Date.now() - lastWriteAt < withinMs
}
