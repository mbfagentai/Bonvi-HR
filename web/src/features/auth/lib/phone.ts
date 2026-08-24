/**
 * Telefon raqami — kirish logini.
 *
 * Formada FAQAT 9 xona saqlanadi («905088888»). «+998» maydonning o'zgarmas
 * qismi bo'lib turadi va tahrirlanmaydi — foydalanuvchi uni o'chirib
 * yuborishi yoki ikki marta yozishi mumkin emas.
 */

/** Maydondagi namuna: `90 123 45 67`. */
export const UZ_PHONE_PLACEHOLDER = '90 123 45 67'

export const UZ_PHONE_LENGTH = 9

/**
 * Har qanday kiritilgan matndan 9 xonali mahalliy raqamni ajratib oladi.
 * «+998 90 508-88-88» yoki «998905088888» ko'chirib qo'yilsa —
 * mamlakat kodi tashlab yuboriladi.
 */
export function uzPhoneDigits(value: string): string {
  const digits = value.replaceAll(/\D/gu, '')

  const local =
    digits.length > UZ_PHONE_LENGTH && digits.startsWith('998') ? digits.slice(3) : digits

  return local.slice(0, UZ_PHONE_LENGTH)
}

/** `905088888` → `90 508 88 88`. To'liq bo'lmagan raqam ham formatlanadi. */
export function formatUzPhone(digits: string): string {
  const d = digits.slice(0, UZ_PHONE_LENGTH)

  return [d.slice(0, 2), d.slice(2, 5), d.slice(5, 7), d.slice(7, 9)]
    .filter(Boolean)
    .join(' ')
}

/**
 * Formatlangan satrda `digitCount`-raqamdan keyingi pozitsiya.
 *
 * Mask qayta qurilgandan keyin kursorni o'z joyiga qaytarish uchun kerak:
 * o'rtadagi raqamni tahrirlaganda kursor satr oxiriga sakramaydi.
 */
export function caretAfterDigit(formatted: string, digitCount: number): number {
  if (digitCount <= 0) return 0

  let seen = 0
  for (const [index, char] of [...formatted].entries()) {
    if (char >= '0' && char <= '9') {
      seen += 1
      if (seen === digitCount) return index + 1
    }
  }

  return formatted.length
}

/** Backend to'liq raqam kutadi: `905088888` → `+998905088888`. */
export function toFullPhone(digits: string): string {
  return `+998${digits}`
}

/**
 * Ko'rsatish uchun: `+998905088888` → `+998 90 508 88 88`.
 * Kutilgan shaklga mos kelmasa — qiymat o'zgarishsiz qaytadi.
 */
export function formatFullPhone(value: string): string {
  const digits = uzPhoneDigits(value)
  return digits.length === UZ_PHONE_LENGTH ? `+998 ${formatUzPhone(digits)}` : value
}
