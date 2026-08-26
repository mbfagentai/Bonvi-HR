/**
 * Faylni brauzerda saqlash — baytdan diskkacha bo'lgan qism.
 *
 * NEGA `shared/lib` da: `saveBlobAs` ilgari
 * `features/employees/lib/document-file.ts` da turardi va o'sha faylning
 * izohida oldindan shunday yozib qo'yilgan edi — «ikkinchi ishlatuvchi
 * paydo bo'lsa, shu yerdan `shared/lib` ga ko'chirish kifoya». Ikkinchisi
 * paydo bo'ldi: xodimlar ro'yxatining Excel eksporti. Nusxa emas, aynan
 * KO'CHIRISH qilindi — bir xil o'n qator ikki joyda yotsa, biri
 * tuzatilganda (masalan `revokeObjectURL` unutilib xotira sizib qolsa)
 * ikkinchisi jimgina eskirib qolardi.
 *
 * Fayl nomini `Content-Disposition` dan ajratish ham shu yerda va bu
 * ataylab: baytni olgan joyda darhol «qanday nom bilan saqlansin?» degan
 * savol tug'iladi, javobi esa HTTP shartnomasidan keladi — biror
 * bo'limning ish qoidasidan emas.
 */

/**
 * Blob'ni foydalanuvchi diskiga saqlaydi — vaqtinchalik `<a download>`
 * havolasi orqali.
 *
 * NEGA shu usul: backend javobi `Content-Disposition: attachment` bilan
 * keladi — server niyati aniq, ko'rish emas, SAQLASH. Havola sahifaga
 * fokus yoki skrinriderlar uchun emas, faqat bitta `click()` uchun
 * qo'shiladi va darhol olib tashlanadi (`useObjectUrl` dagi kabi, havola
 * ham shu zahoti bekor qilinadi — sizib qoladigan joy yo'q).
 */
export function saveBlobAs(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

/**
 * `Content-Disposition` sarlavhasidan fayl nomini ajratadi.
 *
 * NEGA umuman kerak: nom SERVERNIKI bo'lishi kerak — u sanani, tilni va
 * kengaytmani biladi. Klient uni faqat sarlavhadan o'qiy oladi, javob
 * tanasidan emas: eksport javobida DTO yo'q, u to'g'ridan-to'g'ri `.xlsx`
 * baytlari (hujjat yuklab olishda esa nom DTO da bor edi, shu sababli u
 * bu savolga umuman duch kelmagan).
 *
 * Ikkala shakl ham qaraladi va AYNAN shu tartibda:
 *   1. `filename*=UTF-8''…` (RFC 5987) — foizli kodlangan, ya'ni kirill
 *      yoki bo'sh joyli nomni ham buzmasdan tashiydi;
 *   2. `filename="…"` — eski, faqat ASCII shakli.
 * ASP.NET Core ikkalasini ham yuboradi. Nom lotincha bo'lsa ular bir xil,
 * kirillcha bo'lsa esa ikkinchisida harflar «_» ga aylanib ketadi — shu
 * sababli birinchisi ustun turadi.
 *
 * `null` — «sarlavha yo'q yoki tushunarsiz». Bu HAQIQIY ehtimol, xato
 * emas: eski server uni yubormasligi, proksi esa uni kesib tashlashi
 * mumkin, qolaversa server `Access-Control-Expose-Headers` ga qo'shmasa
 * brauzer sarlavhani JS ga umuman ko'rsatmaydi. Yuklab olish shu
 * sabablarning birortasidan BUZILMASLIGI kerak — chaqiruvchi `null` ni
 * ko'rib, o'zi mantiqiy nom yasaydi.
 */
export function fileNameFromDisposition(header: string | null | undefined): string | null {
  if (typeof header !== 'string' || header === '') return null

  // `filename*=<charset>'<til>'<qiymat>` — o'rtadagi til qismi ko'pincha bo'sh
  const extended = /filename\*\s*=\s*[^']*'[^']*'([^;]+)/iu.exec(header)
  const encoded = extended?.[1]
  if (encoded !== undefined) {
    const decoded = decodeOrNull(encoded.trim())
    if (decoded !== null) {
      const safe = safeFileName(decoded)
      if (safe !== null) return safe
    }
  }

  // `filename=` — qo'shtirnoq bilan ham, qo'shtirnoqsiz ham uchraydi
  const plain = /filename\s*=\s*(?:"([^"]*)"|([^;]+))/iu.exec(header)
  const raw = plain?.[1] ?? plain?.[2]

  return raw === undefined ? null : safeFileName(raw.trim())
}

/**
 * Noto'g'ri foiz ketma-ketligi (`%zz`) `decodeURIComponent` da ISTISNO
 * tashlaydi. Buzilgan sarlavha butun yuklab olishni yiqitmasligi uchun
 * istisno shu yerda yutiladi — chaqiruvchi oddiy `filename=` ga o'tadi.
 */
function decodeOrNull(value: string): string | null {
  try {
    return decodeURIComponent(value)
  } catch {
    return null
  }
}

/**
 * Nomdan papka yo'lini va boshqaruv belgilarini olib tashlaydi.
 *
 * NEGA: nom SERVERDAN keladi va to'g'ridan-to'g'ri `<a download>` ga
 * tushadi. Brauzerlar bunday nomni o'zlari ham cheklaydi, lekin sarlavha
 * proksi orqali o'zgargan holda kelishi mumkin — «../» qismini shu yerda
 * kesib tashlash arzon va aniq: yuklab olish papkasidan tashqariga
 * yozishga urinish umuman boshlanmaydi.
 *
 * Nomdan hech narsa qolmasa — `null`, ya'ni «nom yo'q»: bo'sh yoki bitta
 * nuqtadan iborat fayl nomi saqlashni jimgina buzardi.
 */
function safeFileName(name: string): string | null {
  const clean = name.replaceAll(/[\\/\u0000-\u001F]/gu, '').trim()
  return clean === '' || clean === '.' || clean === '..' ? null : clean
}
