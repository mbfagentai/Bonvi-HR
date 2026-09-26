import { formatPhone } from '@/shared/lib/phone'

/**
 * Telefon raqami — kirish logini.
 *
 * Formada FAQAT 9 xona saqlanadi («905088888»). «+998» maydonning o'zgarmas
 * qismi bo'lib turadi va tahrirlanmaydi — foydalanuvchi uni o'chirib
 * yuborishi yoki ikki marta yozishi mumkin emas.
 */

// Mask yordamchilari umumiy bo'ldi — hamma telefon maydoni ishlatadi
export {
  UZ_PHONE_LENGTH,
  UZ_PHONE_PLACEHOLDER,
  caretAfterDigit,
  formatUzPhone,
  uzPhoneDigits,
} from '@/shared/lib/phone'

/** Backend to'liq raqam kutadi: `905088888` → `+998905088888`. */
export function toFullPhone(digits: string): string {
  return `+998${digits}`
}

/**
 * Ko'rsatish uchun: `+998905088888` → `+998 90 508 88 88`.
 * Kutilgan shaklga mos kelmasa — qiymat o'zgarishsiz qaytadi.
 *
 * NEGA o'zi hisoblamaydi: bu shakl butun tizim uchun umumiy qoida
 * (`shared/lib/phone.ts` dagi `formatPhone`), login logikasi bilan
 * bog'liq emas — shu sababli ikkinchi marta yozilmaydi.
 */
export function formatFullPhone(value: string): string {
  return formatPhone(value) ?? value
}
