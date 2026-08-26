/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN (`employees-api.ts` boshidagi
 * izohga qarang: bir xil naqsh, `unknown` dan xavfsiz o'qish).
 *
 * Farqi `employments-api.ts` dagi bilan bir xil: bu yerda ham endpoint
 * ALLAQACHON TAYYOR va testlangan (boshqa agent backend'ni yozgan), shu
 * sababli maydon nomlari «kutilayotgan shartnoma» emas — ular haqiqiy
 * manbadan olingan:
 *   api/src/Bonvi.Api/Endpoints/UserEndpoints.cs
 *   api/src/Bonvi.Application/Identity/Users/Dtos/UserDtos.cs (UserSummaryDto)
 *
 * Backend uchun `./bonvi gen` ishga tushirilgach: shu fayl O'CHIRILADI,
 * `users-queries.ts` generatsiya qilingan hook'ga o'tkaziladi.
 */
import { api } from '@/shared/api/http-client'

/**
 * Ro'yxatdagi/kartochkadagi bitta foydalanuvchi.
 *
 * Parol va uning xeshi BU YERDA ATAYLAB YO'Q — server javobida ham
 * umuman yo'q (`UserSummaryDto` boshidagi izohga qarang).
 */
export interface UserSummary {
  id: string
  /** Kirish logini — to'liq telefon raqami: «+998905088888». */
  login: string
  fullName: string
  /** Qisqa ko'rinish: «Мамажонов Б.Ф.» — bu klientda hozircha ishlatilmaydi. */
  shortName: string
  isActive: boolean
  /** ISO-8601, yoki hech qachon kirmagan bo'lsa `null`. */
  lastLoginAt: string | null
  /**
   * Ketma-ket xato urinishlar sababli vaqtincha bloklangan bo'lsa — shu
   * vaqtgacha. `isActive` dan MUSTAQIL: hisob faol turib ham vaqtincha
   * bloklangan bo'lishi mumkin (parolni bir necha marta xato kiritish).
   */
  lockedUntil: string | null
  /** Rol kodlari, masalan `['admin']`. So'rovlarda ham shu kod ishlatiladi. */
  roleCodes: string[]
  /**
   * Rol nomlari — serverda saqlangan, faqat o'zbek tilida (seed vaqtida
   * yozilgan). Klient ularni KO'RSATISHDA ishlatmaydi: `roles.<code>`
   * tarjimasi 4 tilda ham bor va joriy tilga mos keladi
   * (`user-presentation.ts` ga qarang).
   */
  roleNames: string[]
}

/** Yaratish va tahrirlashda umumiy maydonlar. */
export interface UserFormInput {
  /** To'liq telefon raqami: «+998905088888». */
  login: string
  lastName: string
  firstName: string
  middleName: string | null
  /** Yangi TO'LIQ rol ro'yxati — eskisi ALMASHTIRILADI, qo'shilmaydi. */
  roleCodes: string[]
}

/** Yaratishda qo'shimcha — boshlang'ich parol. */
export interface CreateUserInput extends UserFormInput {
  password: string
}

export async function fetchUsers(signal?: AbortSignal): Promise<UserSummary[]> {
  const { data } = await api.get<unknown>('/api/users', { signal })
  return Array.isArray(data) ? data.map(toUser) : []
}

export async function createUser(body: CreateUserInput): Promise<UserSummary> {
  const { data } = await api.post<unknown>('/api/users', body)
  return toUser(data)
}

export async function updateUser(id: string, body: UserFormInput): Promise<UserSummary> {
  const { data } = await api.put<unknown>(`/api/users/${id}`, body)
  return toUser(data)
}

/** Hisob o'chirilmaydi, faqat kirish yopiladi. Javob: 204. */
export async function deactivateUser(id: string): Promise<void> {
  await api.post(`/api/users/${id}/deactivate`)
}

/** Bloklash hisoblagichi ham tozalanadi. Javob: 204. */
export async function activateUser(id: string): Promise<void> {
  await api.post(`/api/users/${id}/activate`)
}

/**
 * Muvaffaqiyatdan keyin foydalanuvchining BARCHA joriy sessiyalari
 * bekor qilinadi (server tomonda). Javob: 204.
 */
export async function resetUserPassword(id: string, newPassword: string): Promise<void> {
  await api.post(`/api/users/${id}/reset-password`, { newPassword })
}

/* ─────────────────────────────────────────────────── xaritalash (map) */

function toUser(raw: unknown): UserSummary {
  const box = (raw ?? {}) as Record<string, unknown>

  return {
    id: text(box['id']) ?? '',
    login: text(box['login']) ?? '',
    fullName: text(box['fullName']) ?? '—',
    shortName: text(box['shortName']) ?? '—',
    isActive: box['isActive'] === true,
    lastLoginAt: text(box['lastLoginAt']),
    lockedUntil: text(box['lockedUntil']),
    roleCodes: stringArray(box['roleCodes']),
    roleNames: stringArray(box['roleNames']),
  }
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}

function stringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : []
}
