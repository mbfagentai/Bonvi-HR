import { z } from 'zod'

/**
 * Ma'lumotnoma formasi.
 *
 * Xato XABARI emas, xato KODI saqlanadi (`malumotnoma.nom_bosh`) — xuddi
 * backend qaytaradigan kodlar kabi. Nega: tarjima sxema yaratilganda emas,
 * ekranga chizilganda tanlanadi, shu sababli til almashtirilganda ochiq
 * turgan xato ham darhol o'zgaradi. Aynan shu kodni server ham qaytaradi,
 * ya'ni klient va server xatolari bir xil ko'rinadi.
 */

/** Nom uzunligi chegarasi — serverdagi `SaveReferenceCommandValidator` bilan bir xil. */
export const CATALOG_NAME_MAX = 255

/**
 * Raqamli maydon formada satr bo'lib yashaydi.
 *
 * NEGA: bo'sh maydon `''` bo'ladi, `0` emas. `z.coerce.number()` bo'sh
 * satrni `0` ga aylantirib yuboradi va foydalanuvchi kiritmagan qiymat
 * kiritilgandek ko'rinadi.
 */
const numericText = z.string().trim().regex(/^\d*$/u, 'malumotnoma.raqam')

export const catalogItemSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'malumotnoma.nom_bosh')
    .max(CATALOG_NAME_MAX, 'malumotnoma.nom_uzun'),
  note: z.string().trim(),
})

export type CatalogItemValues = z.infer<typeof catalogItemSchema>

export const departmentSchema = catalogItemSchema.extend({
  /** Bo'sh satr — ildiz bo'lim (ota yo'q). */
  parentId: z.string(),
})

export type DepartmentValues = z.infer<typeof departmentSchema>

export const positionSchema = catalogItemSchema.extend({
  /** Bo'sh satr — bo'limga biriktirilmagan. */
  departmentId: z.string(),
  /** Daraja 1–5, sinov muddati 0–180 kun — chegaralarni server tekshiradi. */
  grade: numericText,
  probationDays: numericText,
})

export type PositionValues = z.infer<typeof positionSchema>

/** Bo'sh satrni `null` ga, aks holda raqamga aylantiradi. */
export function toNumberOrNull(value: string): number | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : Number(trimmed)
}

/** Bo'sh satrni `null` ga aylantiradi — server «kiritilmagan» deb tushunadi. */
export function toTextOrNull(value: string): string | null {
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}
