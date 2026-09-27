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

/*
 * Kiritish maskasi: «+998» o'zgarmas, foydalanuvchi faqat 9 xonani teradi
 * va ular `90 123 45 67` bo'lib ajratiladi. Ilgari faqat kirish formasida
 * (`features/auth/lib/phone.ts`) yashardi; 2026-09-26 dan tizimdagi HAMMA
 * telefon maydoni shu maskadan foydalanadi (`shared/ui/phone-input.tsx`).
 */

/** Maydondagi namuna: `90 123 45 67`. */
export const UZ_PHONE_PLACEHOLDER = '90 123 45 67'

export const UZ_PHONE_LENGTH = 9

/**
 * Har qanday kiritilgan matndan 9 xonali mahalliy raqamni ajratib oladi.
 * «+998 90 508-88-88» yoki «998905088888» ko'chirib qo'yilsa —
 * mamlakat kodi tashlab yuboriladi.
 *
 * ⚠️ «998» xonalar soni 9 dan OSHGAN zahoti mamlakat kodi deb olinadi —
 * bu ATAYLAB, ikki zid holatdan kichik yomonligi tanlangan:
 *
 * · HR raqamni «998» bilan tersa (odat), xonalar 10 taga yetgan lahzada
 *   «998» kesiladi va raqam to'g'ri yig'iladi. Chegara «12 xona» qilib
 *   ko'rilgan edi (2026-09-27) — o'shanda maydon 9 xonada to'lib qolib,
 *   «998901234» JIMGINA saqlanardi: yaroqli ko'rinadigan, lekin boshqa
 *   odamning raqami. Bu teskari yo'lga qaytarildi.
 * · Evaziga: «99 812 34 56» kabi «998» bilan boshlanadigan TO'LIQ raqamga
 *   yana bitta xona bosilsa, operator kodi kesilib, 7 xona qoladi. Bu
 *   KO'RINIB turadigan xato — maydon qisqaradi, forma esa 9 xonasiz
 *   raqamni saqlamaydi.
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
