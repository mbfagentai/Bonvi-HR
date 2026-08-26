import { z } from 'zod'

import type { UserSummary } from '../api/users-api'

/**
 * Foydalanuvchi formasi (yaratish va tahrirlash — bir xil maydonlar).
 *
 * Xato XABARI emas, xato KODI saqlanadi — `catalog-schemas.ts` boshidagi
 * izohdagi bilan bir xil sabab: tarjima ekranga chizilganda tanlanadi,
 * shu sababli til almashtirilganda ochiq turgan xato ham darhol o'zgaradi.
 * Kodlar backend'nikilar bilan AYNAN bir xil (`telefon.*`, `ism.*`,
 * `foydalanuvchi.*`) — klient va server xatolari bir xil ko'rinadi.
 */
export const userFormSchema = z.object({
  /** 9 xona, «+998» siz — maydonning o'zi shunday ko'rsatadi. */
  phone: z
    .string()
    .trim()
    .min(1, 'telefon.bosh')
    .regex(/^\d{9}$/u, 'telefon.uzunlik'),
  lastName: z.string().trim().min(1, 'ism.familiya_bosh'),
  firstName: z.string().trim().min(1, 'ism.ism_bosh'),
  middleName: z.string().trim(),
  /** Kamida bitta rol — bo'sh ro'yxatni server ham rad etadi. */
  roleCodes: z.array(z.string()).min(1, 'foydalanuvchi.rol_bosh'),
})

export type UserFormValues = z.infer<typeof userFormSchema>

/**
 * Parol maydoni — yaratish va parol tiklash oynalarida umumiy.
 *
 * Ikki `.min()` ketma-ket: backend `PasswordPolicy.Validate` bilan AYNAN
 * bir xil tartib — avval bo'shligi, keyin uzunligi tekshiriladi, shu
 * sababli bo'sh maydonda «parol kiritilishi shart» chiqadi, «kamida 8
 * belgi» emas (ikkalasi ham to'g'ri, lekin birinchisi aniqroq).
 */
export const PASSWORD_MIN_LENGTH = 8

export const passwordFieldSchema = z
  .string()
  .min(1, 'foydalanuvchi.parol_bosh')
  .min(PASSWORD_MIN_LENGTH, 'foydalanuvchi.parol_qisqa')

export const createPasswordSchema = z.object({ password: passwordFieldSchema })
export type CreatePasswordValues = z.infer<typeof createPasswordSchema>

/** Bo'sh satrni `null` ga aylantiradi — server «kiritilmagan» deb tushunadi. */
export function toTextOrNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/** Formadagi 9 xonani serverga to'liq ko'rinishda yuboramiz: «+998905088888». */
export function toFullPhone(nineDigits: string): string {
  return `+998${nineDigits}`
}

/**
 * Serverdagi to'liq raqamdan maydonga tushadigan qismini ajratadi:
 * «+998905088888» → «905088888» (`employee-schemas.ts` dagi `toLocalPhone`
 * bilan bir xil mantiq).
 */
function toLocalPhone(value: string): string {
  const digits = value.replaceAll(/\D/gu, '')
  return digits.length === 12 && digits.startsWith('998') ? digits.slice(3) : digits
}

/**
 * `null` — yangi hisob: bo'sh forma.
 *
 * Familiya/ism/otasining ismi `UserSummaryDto` da ALOHIDA maydon emas —
 * faqat qo'shilgan `fullName` keladi (server javobida parol bo'lmagani
 * kabi ataylab: kartochka faqat ko'rsatish uchun). Shu sababli tahrirlash
 * formasi uni QAYTA AJRATADI — xuddi backend'ning o'zi `FullName.Parse`
 * da qilgani kabi: birinchi ikki so'z familiya va ism, qolgani (bir necha
 * so'zdan iborat bo'lishi mumkin, masalan «Fazlitdin o'g'li») otasining
 * ismi. Familiya yoki ism o'zi bo'sh joy bilan yozilgan noyob holatlarda
 * bu noaniq bo'ladi — xuddi backend'dagi cheklov kabi.
 */
export function toUserFormValues(user: UserSummary | null): UserFormValues {
  if (user === null) {
    return { phone: '', lastName: '', firstName: '', middleName: '', roleCodes: [] }
  }

  const [lastName = '', firstName = '', ...rest] = user.fullName.split(' ')

  return {
    phone: toLocalPhone(user.login),
    lastName,
    firstName,
    middleName: rest.join(' '),
    roleCodes: [...user.roleCodes],
  }
}
