/**
 * ⚠️ VAQTINCHALIK KLIENT — QO'LDA YOZILGAN.
 *
 * Backend'ning `POST /api/auth/*` endpoint'lari hali yozilmagan, shu sababli
 * tiplar bu yerda kelishilgan shartnoma bo'yicha qo'lda e'lon qilingan.
 *
 * Backend tayyor bo'lgach:
 *   1. `./bonvi gen` — OpenAPI'dan haqiqiy klient generatsiya qilinadi
 *      (`@/shared/api/generated/auth/auth` va `.../model`)
 *   2. Shu fayl O'CHIRILADI, import'lar generatsiya qilingan modulga ko'chiriladi
 *   3. `AuthUser` o'rniga generatsiya qilingan DTO ishlatiladi
 *
 * Token yangilash (`POST /api/auth/refresh`) bu yerda YO'Q: uni
 * `@/shared/api/http-client` interceptor'i o'zi bajaradi.
 */
import axios from 'axios'

import { api } from '@/shared/api/http-client'

export interface AuthUser {
  id: string
  /** Kirish logini — to'liq telefon raqami: «+998905088888». */
  login: string
  fullName: string
  /** Rol kodlari: `admin`, `hr_manager`, `hr_operator`, `dept_head`, `viewer`. */
  roles: string[]
  /** Ruxsat kodlari: `employees.view`, `employees.edit`, … */
  permissions: string[]
  /**
   * Rol NOMLARI, `roles` bilan bir xil tartibda. Rollarni egasi o'zi
   * yaratadi va nomlaydi — yangi rolning tarjima kaliti yo'q, shu sababli
   * ekranda nomi ko'rsatiladi. Eski saqlangan nusxada bo'lmasligi mumkin.
   */
  roleNames: string[]
  /**
   * Hisob rasmining saqlash kaliti; rasm yo'q bo'lsa `null`.
   *
   * Rasmning O'ZI emas, kaliti keladi — u kesh kaliti bo'lib xizmat qiladi:
   * surat almashtirilsa kalit ham boshqa bo'ladi va eski nusxaga hech kim
   * murojaat qilmaydi.
   */
  photoKey: string | null
}

export interface AuthSession {
  accessToken: string
  refreshToken: string
  /** ISO-8601: access token qachon eskiradi. */
  expiresAt: string
  user: AuthUser
}

export interface LoginRequest {
  /** To'liq telefon raqami: «+998905088888». */
  login: string
  password: string
}

/**
 * `skipAuthRefresh` — noto'g'ri parol ham 401 qaytaradi, lekin bu sessiya
 * tugagani emas. Bayroqsiz interceptor sahifani /kirish ga qayta yuklab
 * yuboradi va forma xatoni ko'rsatishga ulgurmaydi.
 */
export async function login(body: LoginRequest): Promise<AuthSession> {
  const { data } = await api.post<AuthSession>('/api/auth/login', body, {
    skipAuthRefresh: true,
  })
  return data
}

/** Server refresh token'ni bekor qiladi. Javob: 204. */
export async function logout(refreshToken: string): Promise<void> {
  await api.post('/api/auth/logout', { refreshToken }, { skipAuthRefresh: true })
}

/** Joriy foydalanuvchi — sahifa yangilanganda ruxsatlarni tasdiqlash uchun. */
export async function getMe(): Promise<AuthUser> {
  const { data } = await api.get<AuthUser>('/api/auth/me')
  return data
}

/* ───────────────────────────────────────────────────── hisob rasmi */

/**
 * Hisob rasmi — BAYTLAR, havola emas.
 *
 * NEGA `<img src="/api/…">` ishlamaydi: tizim `Authorization: Bearer`
 * sarlavhasi bilan ishlaydi, cookie yo'q. Brauzer esa `<img>` so'roviga
 * hech qanday sarlavha qo'sha olmaydi va server 401 qaytaradi. Shu sababli
 * rasm oddiy so'rov bilan olinadi, ko'rsatish uchun esa `URL.createObjectURL`
 * ishlatiladi (`useObjectUrl`).
 *
 * `404` — XATO EMAS. U «rasm yo'q» degani va `null` ga aylanadi: chaqiruvchi
 * jimgina bosh harflarga qaytadi.
 */
export async function fetchMyPhoto(signal?: AbortSignal): Promise<Blob | null> {
  try {
    const { data } = await api.get<Blob>('/api/auth/me/photo', {
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
export async function uploadMyPhoto(file: File): Promise<string | null> {
  const form = new FormData()
  form.append('file', file)

  const { data } = await api.post<{ photoKey?: unknown }>('/api/auth/me/photo', form)

  return typeof data?.photoKey === 'string' ? data.photoKey : null
}

/** Rasmni o'chirish. Server idempotent: rasm bo'lmasa ham `204` qaytaradi. */
export async function deleteMyPhoto(): Promise<void> {
  await api.delete('/api/auth/me/photo')
}
