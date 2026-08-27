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
import axios from 'axios'

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
  /**
   * Hisob rasmining saqlash kaliti; rasm yo'q bo'lsa `null`.
   *
   * Rasmning O'ZI emas, kaliti keladi — u kesh kaliti bo'lib xizmat qiladi
   * (xuddi xodimlar ro'yxatidagi kabi): surat almashtirilsa kalit ham boshqa
   * bo'ladi va eski nusxaga hech kim murojaat qilmaydi. Kalitsiz ro'yxat
   * har bir qator uchun keraksiz 404 so'rov yuborardi.
   */
  photoKey: string | null
  /**
   * Rol kodi — AYNAN BITTA (`'hr_manager'`). So'rovlarda ham shu kod
   * ishlatiladi.
   *
   * Ro'yxat emas: uch rolli modelda hisobda bittadan ortiq rol bo'lmaydi
   * (`User.SetRole` izohiga qarang). Rol berilmagan eski yozuvda `null`
   * bo'lishi mumkin — amalda bunday hisob yo'q.
   */
  roleCode: string | null
  /**
   * Rol nomi — serverda saqlangan, faqat o'zbek tilida (seed vaqtida
   * yozilgan). Klient uni KO'RSATISHDA ishlatmaydi: `roles.<code>`
   * tarjimasi 4 tilda ham bor va joriy tilga mos keladi
   * (`users-page.tsx` dagi `roleLabel` ga qarang).
   */
  roleName: string | null
}

/** Yaratish va tahrirlashda umumiy maydonlar. */
export interface UserFormInput {
  /** To'liq telefon raqami: «+998905088888». */
  login: string
  lastName: string
  firstName: string
  middleName: string | null
  /** Yangi rol kodi — eskisi ALMASHTIRILADI. */
  roleCode: string
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

/* ─────────────────────────────────────────────────────────── rasm */

/**
 * Foydalanuvchi rasmi — BAYTLAR, havola emas.
 *
 * NEGA `<img src="/api/…">` ishlamaydi: tizim `Authorization: Bearer`
 * sarlavhasi bilan ishlaydi, cookie yo'q. Brauzer esa `<img>` so'roviga
 * hech qanday sarlavha qo'sha olmaydi va server 401 qaytaradi. Shu sababli
 * rasm oddiy so'rov bilan olinadi, ko'rsatish uchun esa `URL.createObjectURL`
 * ishlatiladi (`useObjectUrl`) — xodim rasmidagi bilan aynan bir xil yo'l.
 *
 * `404` — XATO EMAS. U «bu hisobning rasmi yo'q» degani va `null` ga
 * aylanadi: chaqiruvchi jimgina bosh harflarga qaytadi.
 */
export async function fetchUserPhoto(id: string, signal?: AbortSignal): Promise<Blob | null> {
  try {
    const { data } = await api.get<Blob>(`/api/users/${id}/photo`, {
      responseType: 'blob',
      signal,
    })
    return data
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404) return null
    throw error
  }
}

/**
 * Rasmni yuklash. Javobdagi yangi `photoKey` qaytariladi.
 *
 * `Content-Type` bu yerda QO'YILMAYDI va qo'yilmasligi kerak: axios uni
 * `FormData` ni ko'rib o'zi yozadi va **chegara (boundary)** satrini ham
 * qo'shadi. Sarlavhani qo'lda yozish chegarani yo'qotadi va server tanani
 * ajrata olmay qoladi — sabab `http-client.ts` da batafsil.
 */
export async function uploadUserPhoto(id: string, file: File): Promise<string | null> {
  const form = new FormData()
  form.append('file', file)

  const { data } = await api.post<unknown>(`/api/users/${id}/photo`, form)

  const box = (data ?? {}) as Record<string, unknown>
  return text(box['photoKey'])
}

/** Rasmni o'chirish. Server idempotent: rasm bo'lmasa ham `204` qaytaradi. */
export async function deleteUserPhoto(id: string): Promise<void> {
  await api.delete(`/api/users/${id}/photo`)
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
    photoKey: text(box['photoKey']),
    roleCode: text(box['roleCode']),
    roleName: text(box['roleName']),
  }
}

function text(value: unknown): string | null {
  if (typeof value !== 'string') return null
  const trimmed = value.trim()
  return trimmed === '' ? null : trimmed
}
