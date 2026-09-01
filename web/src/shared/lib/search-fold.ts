/**
 * Qidiruv matnini «bir xil ko'rinadigan» harflardan tozalaydi.
 *
 * NEGA KERAK: HR o'zbek kirillidagi қ, ғ, ҳ, ў harflarini rus
 * klaviaturasida tera olmaydi va ularni eng yaqin harf bilan yozadi —
 * «Қўқон» → «Кукон», «Ғаниев» → «Ганиев». Yig'ishsiz bunday qidiruv
 * hech narsa topmaydi va HR yozuv umuman yo'q deb o'ylaydi.
 *
 * ⚠️ BAZADAGI QOIDANING NUSXASI. Backend'da xuddi shu almashtirish
 * ikki joyda takrorlanadi: `SearchFolding.NameFoldFrom/To` (C#) va
 * migratsiyadagi `translate(..., 'қғҳўё', 'кгхуе')` indeksi (SQL).
 * Uchalasi BIRGA o'zgaradi — biri o'zgarib, ikkinchisi qolsa, server
 * qidiruvi bilan ekrandagi qidiruv boshqa natija berardi va sababini
 * topish juda qiyin bo'lardi.
 *
 * NEGA TRANSLITERATSIYA YO'Q: «Abduqodirov» yozib «Абдуқодиров» ni
 * topish ATAYLAB qilinmagan — foydalanuvchining qarori. Qidiruv matn
 * QANDAY yozilgan bo'lsa, shunday izlaydi; bu yerda faqat bir xil
 * ko'rinadigan kirill harflari birlashtiriladi.
 */
const FOLD_FROM = 'қғҳўё'
const FOLD_TO = 'кгхуе'

/**
 * Taqqoslash uchun matnni normallashtiradi: kichik registr + yig'ish.
 *
 * `toLocaleLowerCase` ataylab EMAS: u brauzer tiliga qarab boshqacha
 * ishlaydi (turkcha «I» klassik misoli) va bir xil ma'lumot ikki
 * kompyuterda ikki xil topilardi. Bu yerda qat'iy, tildan mustaqil
 * qoida kerak.
 */
export function foldForSearch(value: string): string {
  let folded = ''

  for (const symbol of value.toLowerCase()) {
    const index = FOLD_FROM.indexOf(symbol)
    folded += index >= 0 ? FOLD_TO[index] : symbol
  }

  return folded
}

/**
 * `needle` `haystack` ichida bormi — yig'ilgan holda.
 *
 * Ikkala tomon ham yig'iladi: HR «Кукон» deb yozsa ham, ma'lumotnomada
 * «Қўқон» tursa ham topilsin.
 */
export function matchesSearch(haystack: string, needle: string): boolean {
  return foldForSearch(haystack).includes(foldForSearch(needle))
}
