/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employees-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Farqi `employments-api.ts` dagi bilan bir xil: bu yerda ham endpoint
 * ALLAQACHON TAYYOR va testlangan (boshqa agent backend'ni yozgan), shu
 * sababli maydon nomlari «kutilayotgan shartnoma» emas — ular haqiqiy
 * manbadan olingan:
 *   api/src/Bonvi.Domain/Employees/EmployeeCompetency.cs
 *   api/src/Bonvi.Api/Endpoints/EmployeeEndpoints.cs (`MapCompetency`)
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * `employees-queries.ts` dagi hook'lar generatsiya qilinganiga o'tkaziladi.
 */
import { api } from '@/shared/api/http-client'

/**
 * Xodimning qobiliyatlar profili (Excel: «91.15 Қобилятлар жадвали»).
 *
 * Bitta xodimda BITTA profil bo'ladi — `EmployeeFlag` yoki `Incentive`
 * kabi tarix emas: yangi ma'lumot eskisini TO'LIQ almashtiradi.
 */
export interface EmployeeCompetency {
  id: string
  employeeId: string
  languages: string | null
  /** Kompyuter savodxonligi, 0–100%. */
  computerLiteracyPercent: number | null
  specialization: string | null
  otherAbilities: string | null
  interest: string | null
  educationPlace: string | null
}

/**
 * `PUT` tanasi — butun profil bir yo'la yuboriladi, qisman yangilash yo'q.
 * Forma har doim BARCHA oltita maydonni qaytaradi.
 */
export interface SaveCompetencyRequest {
  languages: string | null
  computerLiteracyPercent: number | null
  specialization: string | null
  otherAbilities: string | null
  interest: string | null
  educationPlace: string | null
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

/** `int?` — son yoki `null`. Tushunarsiz qiymat ham `null` (himoyaviy o'qish). */
function intOrNull(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return Math.trunc(value)
  if (typeof value === 'string' && value.trim() !== '') {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? Math.trunc(parsed) : null
  }
  return null
}

/**
 * Javob tanasi umuman bo'sh — profil hali yaratilmagan.
 *
 * Server `Results.Ok(null)` qaytaradi, bu JSON `null` matniga aylanadi va
 * axios uni to'g'ridan-to'g'ri `null` qilib o'qiydi. `''`/`undefined` ham
 * shu bilan bir qatorda tekshiriladi — qo'shimcha ehtiyot chorasi, aslida
 * shu holat kelmasligi kerak (ASP.NET Core doim `"null"` yozadi).
 */
function isEmptyBody(data: unknown): boolean {
  return data === null || data === undefined || data === ''
}

function toCompetency(raw: unknown): EmployeeCompetency {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    employeeId: text(box['employeeId']) ?? '',
    languages: text(box['languages']),
    computerLiteracyPercent: intOrNull(box['computerLiteracyPercent']),
    specialization: text(box['specialization']),
    otherAbilities: text(box['otherAbilities']),
    interest: text(box['interest']),
    educationPlace: text(box['educationPlace']),
  }
}

/**
 * Qobiliyatlar profilini o'qiydi.
 *
 * ⚠️ HR hali to'ldirmagan bo'lsa server `200` va BO'SH tana qaytaradi —
 * `404` EMAS (`EmployeeEndpoints.MapCompetency` izohiga qarang: «HR hali
 * to'ldirmagan bo'lsa — bo'sh javob (200, null tana), xato emas»). Bu
 * holat xato emas, `null` ga aylanadi: chaqiruvchi buni «forma bo'sh
 * boshlanadi» deb o'qiydi, `isError` emas.
 *
 * `404` bu yerda faqat XODIM topilmasa keladi (haqiqiy xato) — u
 * qayta ishlanmasdan chaqiruvchiga o'tadi.
 */
export async function fetchEmployeeCompetency(
  employeeId: string,
  signal?: AbortSignal,
): Promise<EmployeeCompetency | null> {
  const { data } = await api.get<unknown>(`/api/employees/${employeeId}/competency`, { signal })
  return isEmptyBody(data) ? null : toCompetency(data)
}

/**
 * Profilni saqlaydi — birinchi marta YARATADI yoki bor bo'lsa TO'LIQ
 * ALMASHTIRADI. Qisman yangilash yo'q: `body` har doim barcha oltita
 * maydonni o'z ichiga oladi.
 */
export async function saveEmployeeCompetency(
  employeeId: string,
  body: SaveCompetencyRequest,
): Promise<EmployeeCompetency> {
  const { data } = await api.put<unknown>(`/api/employees/${employeeId}/competency`, body)
  return toCompetency(data)
}
