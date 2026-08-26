/**
 * Telefon raqamini ko'rsatish uchun formatlash.
 *
 * NEGA `shared` da: raqam bazada kanonik shaklda saqlanadi (`+998905088877`,
 * `PhoneNumber` value object) va bu qoida xodimga ham, foydalanuvchi
 * hisobiga ham, kelajakdagi har qanday boshqa telefon maydoniga ham bir xil
 * tegishli — bu xodimga xos emas, butun tizimning ko'rinish qoidasi.
 * Ilgari `features/employees` ichida yashagani sababli boshqa bo'limlar
 * o'z nusxasini yozgan edi (`features/auth/lib/phone.ts` dagi
 * `formatFullPhone`) — endi ikkalasi ham shu bitta funksiyaga tayanadi.
 */

/** «905088888» yoki «+998905088888» → «+998 90 508 88 88». */
export function formatPhone(phone: string | null): string | null {
  if (phone === null) return null

  const digits = phone.replaceAll(/\D/gu, '')
  const local = digits.length === 12 && digits.startsWith('998') ? digits.slice(3) : digits
  if (local.length !== 9) return phone

  return `+998 ${local.slice(0, 2)} ${local.slice(2, 5)} ${local.slice(5, 7)} ${local.slice(7)}`
}
