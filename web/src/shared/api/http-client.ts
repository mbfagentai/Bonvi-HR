import axios, {
  type AxiosError,
  type AxiosRequestConfig,
  type AxiosResponse,
} from 'axios'

import { authStorage } from '@/shared/lib/auth-storage'

/*
 * Axios'ning so'rov sozlamalariga bitta maydon qo'shiladi.
 * Tip parametrlari (`D`, `P`) va ularning `any` standart qiymati
 * axios'nikiga AYNAN mos bo'lishi shart — aks holda TypeScript
 * interfeyslarni birlashtirmaydi.
 */
declare module 'axios' {
  interface AxiosRequestConfig<D = any, P = any> {
    /**
     * Shu so'rov uchun 401 javobida avtomatik token yangilash O'TKAZIB YUBORILADI.
     *
     * Kirish, token yangilash va chiqish so'rovlariga kerak. Noto'g'ri parol
     * kiritilganda ham 401 keladi, lekin bu «sessiya tugadi» degani emas —
     * bayroqsiz interceptor foydalanuvchini /kirish ga qayta yuklab yuboradi
     * va forma xatoni ko'rsatishga ulgurmaydi.
     */
    skipAuthRefresh?: boolean
  }
}

/**
 * Barcha API so'rovlari shu nusxadan o'tadi.
 * Orval generatsiya qilgan hook'lar ham shuni ishlatadi.
 *
 * ⚠️ `Content-Type` ATAYLAB O'RNATILMAGAN.
 *
 * Ilgari bu yerda `application/json` turardi va u **fayl yuklashni buzardi**:
 * `FormData` yuborilganda multipart tanasi bo'laklarga **chegara (boundary)**
 * satri bilan bo'linadi, chegarani esa brauzer `Content-Type` ga o'zi yozadi.
 * Sarlavha oldindan belgilangan bo'lsa, brauzer unga tegmaydi — chegara
 * yo'qoladi va server tanani ajrata olmay `400` qaytaradi. Xodim rasmi
 * aynan shu sababdan yuklanmasdi.
 *
 * Sarlavhasiz axios uni tananing turiga qarab O'ZI qo'yadi: oddiy obyektga
 * `application/json`, `FormData` ga esa chegarasi bilan
 * `multipart/form-data`. Ya'ni bu yozuv ortiqcha ham, zararli ham edi.
 */
export const api = axios.create({
  baseURL: import.meta.env['VITE_API_URL'] ?? 'http://localhost:7080',
  timeout: 30_000,
})

/** Har bir so'rovga joriy access token qo'shiladi. */
api.interceptors.request.use((config) => {
  const token = authStorage.getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

/**
 * 401 kelganda token bir marta yangilanadi va so'rov qayta yuboriladi.
 * Bir vaqtda bir nechta so'rov 401 olsa — yangilash FAQAT BIR MARTA bajariladi,
 * qolganlari o'sha va'daga (promise) ulanadi.
 */
let refreshing: Promise<string | null> | null = null

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as (AxiosRequestConfig & { _retried?: boolean }) | undefined

    if (
      error.response?.status !== 401 ||
      !original ||
      original._retried ||
      original.skipAuthRefresh
    ) {
      return Promise.reject(error)
    }

    original._retried = true

    refreshing ??= refreshAccessToken().finally(() => {
      refreshing = null
    })

    const token = await refreshing

    if (!token) {
      authStorage.clear()
      window.location.assign(loginUrl())
      return Promise.reject(error)
    }

    return api(original)
  },
)

/**
 * Kirish sahifasining manzili, ochilgan sahifa eslab qolingan holda.
 *
 * Sessiya ish o'rtasida tugaganda foydalanuvchi qaytib kirgach o'sha
 * sahifada davom etadi — bosh sahifadan qaytadan yo'l izlamaydi.
 */
function loginUrl(): string {
  const current = window.location.pathname + window.location.search

  // Allaqachon kirish sahifasidamiz — o'zimizni o'zimizga qaytarmaymiz
  if (window.location.pathname === '/kirish') return '/kirish'

  return `/kirish?redirect=${encodeURIComponent(current)}`
}

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = authStorage.getRefreshToken()
  if (!refreshToken) return null

  try {
    const { data } = await axios.post<{ accessToken: string; refreshToken: string }>(
      `${api.defaults.baseURL}/api/auth/refresh`,
      { refreshToken },
    )
    authStorage.setTokens(data.accessToken, data.refreshToken)
    return data.accessToken
  } catch {
    return null
  }
}

/**
 * Orval uchun mutator. Generatsiya qilingan har bir endpoint
 * shu funksiya orqali chaqiriladi.
 */
export const httpClient = <T>(config: AxiosRequestConfig): Promise<T> =>
  api.request<unknown, AxiosResponse<T>>(config).then((r) => r.data)

export default httpClient
