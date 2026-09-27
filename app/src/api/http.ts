import { ApiError, NoAnswerError, problemToError } from './errors'
import { sessionStore } from './session-store'
import type { LoginResult } from './types'

/**
 * HAQIQIY server bilan gaplashadigan yagona joy — `fetch` ustidagi qobiq.
 *
 * Uchta qoida shu yerda hal qilinadi va ekranlar ularni bilmaydi:
 *
 *  1. **Muddati o'tgan access token** (401 `auth.token_yaroqsiz`) —
 *     sessiya BIR MARTA yangilanadi va so'rov qayta yuboriladi. Yangilash
 *     «bitta parvoz» (single-flight): bir vaqtda yiqilgan besh so'rov
 *     bitta yangilashni kutadi. Aks holda besh parallel yangilash bitta
 *     tokenni besh marta ishlatib, serverdagi o'g'irlik qoidasini yoqib,
 *     hamma sessiyani yopib yuborardi.
 *  2. **403 — biznes xatosi**, sessiya tugagani EMAS
 *     (`davomat.mobil_ruxsat_yoq`, `xodim.hisob_faol_emas`,
 *     `xodim.bloklangan`). Kirish ekraniga FAQAT yangilash ham rad
 *     etilganda qaytiladi. Istisno — `xodim.parol_almashtirilsin`: sessiya
 *     «parol o'rnatish» holatiga o'tadi.
 *  3. **Javobi kelmagan belgilash** — `NoAnswerError` (UX §C.5): so'rov
 *     ketgan bo'lishi mumkin, qayta yuborilmaydi.
 */
const BASE_URL = (import.meta.env.VITE_API_URL ?? '').replace(/\/+$/, '')

/** So'rov shuncha kutadi — keyin «javob kelmadi». */
const TIMEOUT_MS = 20_000

/** Access token tugashidan shuncha oldin yangilanadi — 401 aylanishisiz. */
const REFRESH_AHEAD_MS = 30_000

export interface RequestOptions {
  method?: 'GET' | 'POST'
  body?: unknown
  /** Access token bilanmi (sukut — ha). */
  auth?: boolean
  /**
   * Tarmoq uzilsa yoki proksi javob bermasa — «bilmaymiz» (`NoAnswerError`).
   * Faqat belgilash uchun: u takrorlansa ikkinchi o'tish yasaydi.
   */
  uncertainOnFailure?: boolean
  /** Javob tanasi — JSON (sukut) yoki bayt. */
  as?: 'json' | 'blob'
}

interface RawResponse {
  status: number
  body: unknown
}

/** Bitta HTTP so'rov — hech qanday qayta urinishsiz. */
function send(path: string, options: RequestOptions, token: string | null): Promise<RawResponse> {
  const headers: Record<string, string> = { Accept: options.as === 'blob' ? 'image/*' : 'application/json' }
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`

  const controller = typeof AbortController === 'function' ? new AbortController() : null
  const timer = controller ? window.setTimeout(() => controller.abort(), TIMEOUT_MS) : null

  const done = (): void => {
    if (timer !== null) window.clearTimeout(timer)
  }

  return window
    .fetch(`${BASE_URL}${path}`, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: controller ? controller.signal : undefined,
      // Cookie ishlatilmaydi — token sarlavhada; begona saytdan so'rov
      // bizning nomimizdan hech narsa olib keta olmaydi.
      credentials: 'omit',
    })
    .then(
      (response) => readBody(response, options).then((body) => {
        done()
        return { status: response.status, body }
      }),
      (cause: unknown) => {
        done()
        throw cause
      },
    )
}

function readBody(response: Response, options: RequestOptions): Promise<unknown> {
  if (response.status === 204 || response.status === 304) return Promise.resolve(null)

  if (options.as === 'blob' && response.ok) return response.blob()

  const type = response.headers.get('Content-Type') ?? ''
  if (type.indexOf('json') < 0) return response.text().then(() => null)

  return response.json().catch(() => null)
}

/**
 * Sessiyani yangilaydi — «bitta parvoz».
 *
 * Natija: `true` — yangi juftlik saqlandi; `false` — yangilash tokeni yo'q
 * yoki server uni rad etdi (sessiya tozalanadi). Tarmoq xatosi ESA
 * tashlanadi va sessiya TEGILMAYDI: metroda ketayotgan odam internet
 * qaytgach tizimdan chiqib qolmasligi kerak.
 */
let inflight: Promise<boolean> | null = null

export function refreshSession(): Promise<boolean> {
  if (!inflight) {
    const release = (): void => {
      inflight = null
    }

    inflight = doRefresh().then(
      (value) => {
        release()
        return value
      },
      (cause: unknown) => {
        release()
        throw cause
      },
    )
  }

  return inflight
}

function doRefresh(): Promise<boolean> {
  const refreshToken = sessionStore.get().refreshToken
  if (!refreshToken) return Promise.resolve(false)

  return send('/api/employee-auth/refresh', { method: 'POST', body: { refreshToken }, auth: false }, null).then(
    (response) => {
      if (response.status >= 200 && response.status < 300 && response.body) {
        sessionStore.setTokens(response.body as LoginResult)
        return true
      }

      // 429 — sessiya tirik, faqat hozir emas.
      if (response.status === 429 || response.status >= 500) throw problemToError(response.status, response.body)

      // 403 `xodim.token_yaroqsiz` (bekor qilingan, muddati o'tgan yoki
      // hisob yopilgan) — sessiya tugadi.
      sessionStore.clear(true)
      return false
    },
    () => {
      throw new ApiError('network')
    },
  )
}

/** Access token tugashiga oz qolgan bo'lsa, so'rovdan OLDIN yangilaydi. */
function freshToken(): Promise<string | null> {
  const current = sessionStore.get()
  if (!current.accessToken) return Promise.resolve(null)

  if (current.accessExpiresAt !== null && current.accessExpiresAt - Date.now() < REFRESH_AHEAD_MS) {
    return refreshSession().then(
      () => sessionStore.get().accessToken,
      // Tarmoq yo'q — eski token bilan urinib ko'ramiz: server 401 bersa,
      // quyidagi yo'l yana yangilaydi.
      () => sessionStore.get().accessToken,
    )
  }

  return Promise.resolve(current.accessToken)
}

/** Sessiyasiz yoki yangilash rad etilganda — kirish ekraniga qaytish belgisi. */
function unauthorized(): ApiError {
  return new ApiError('unauthorized', { code: 'auth.token_yaroqsiz', status: 401 })
}

/**
 * So'rov yuboradi va javobni shartnoma turiga keltiradi.
 *
 * Muvaffaqiyat — tana (204 da `undefined`); xato — `ApiError`.
 */
export function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const auth = options.auth !== false

  const attempt = (token: string | null, retried: boolean): Promise<T> =>
    send(path, options, token).then(
      (response) => {
        if (response.status >= 200 && response.status < 300) {
          return (response.status === 204 ? undefined : response.body) as T
        }

        // Muddati o'tgan token — bir marta yangilab, qayta. 401 da so'rov
        // ishlovchiga YETMAGAN, ya'ni belgilashni ham qayta yuborish xavfsiz.
        if (response.status === 401 && auth) {
          if (retried) {
            sessionStore.clear(true)
            throw unauthorized()
          }

          return refreshSession().then((ok) => {
            const next = sessionStore.get().accessToken
            if (!ok || !next) throw unauthorized()
            return attempt(next, true)
          })
        }

        // Proksi javob bermadi (502/503/504) — belgilash bajarilgan
        // bo'lishi mumkin: «bilmaymiz».
        if (options.uncertainOnFailure && response.status >= 502 && response.status <= 504) {
          throw new NoAnswerError()
        }

        const error = problemToError(response.status, response.body)

        if (error.code === 'xodim.parol_almashtirilsin') sessionStore.requirePasswordChange()

        throw error
      },
      (cause: unknown) => {
        if (cause instanceof ApiError) throw cause
        // So'rov ketgan-ketmagani noma'lum.
        throw options.uncertainOnFailure ? new NoAnswerError() : new ApiError('network')
      },
    )

  if (!auth) return attempt(null, false)

  return freshToken().then((token) => {
    if (!token) throw unauthorized()
    return attempt(token, false)
  })
}
