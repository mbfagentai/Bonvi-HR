/**
 * Server xatosi — KOD, matn emas.
 *
 * Backend `davomat.obyektdan_uzoq` kabi kod qaytaradi (ProblemDetails'ning
 * `title` maydonida), matnni esa ilova o'zining tarjimasidan oladi
 * (`errors.<kod>`), shunda xabar xodimning tilida chiqadi. Tarjimasi
 * topilmasa kodning o'zi ko'rsatiladi — yashirish mumkin emas, aks holda
 * xato jim yo'qoladi.
 */
export type ApiErrorKind =
  /** Server javob berdi va rad etdi (4xx) — `code` to'la. 403 ham shu: u BIZNES xatosi. */
  | 'rejected'
  /** 5xx. */
  | 'server'
  /** So'rov umuman ketmadi yoki javob kelmadi. */
  | 'network'
  /**
   * Sessiya YO'Q: access token yaroqsiz va yangilash ham rad etildi.
   * Faqat shu holatda ilova kirish ekraniga qaytadi — har 403 da emas.
   */
  | 'unauthorized'

/** Xato bilan kelgan qiymat — son, satr yoki lahza (ISO satr). */
export type ErrorDetail = string | number

export class ApiError extends Error {
  readonly kind: ApiErrorKind
  readonly code: string | null
  readonly status: number | null
  /**
   * Xato bilan birga kelgan qo'shimcha qiymatlar — ProblemDetails'ning
   * kengaytma maydonlari (`accuracyMeters`, `siteName`, `distanceMeters`,
   * `lastAt`, `retryAfterMinutes`, `retryAfterSeconds`). Xabar matniga
   * aynan shular qo'yiladi: «Eng yaqin obyekt: Bosh ofis — 420 m» kabi
   * jumla «qayta urinib ko'ring» dan ko'ra foydaliroq.
   */
  readonly details: Record<string, ErrorDetail>

  constructor(
    kind: ApiErrorKind,
    options: {
      code?: string | null
      status?: number | null
      details?: Record<string, ErrorDetail>
      message?: string
    } = {},
  ) {
    super(options.message ?? options.code ?? kind)
    this.name = 'ApiError'
    this.kind = kind
    this.code = options.code ?? null
    this.status = options.status ?? null
    this.details = options.details ?? {}
  }

  static from(cause: unknown): ApiError {
    if (cause instanceof ApiError) return cause
    if (cause instanceof Error) return new ApiError('network', { message: cause.message })
    return new ApiError('network')
  }
}

/**
 * Javob yo'lda yo'qolganda tashlanadi.
 *
 * ⚠️ Bu XATO EMAS, NOANIQLIK: belgi serverda yaratilgan bo'lishi ham
 * mumkin (UX §C.5). Shu sababli alohida tur — avtomatik qayta urinish
 * ikkinchi o'tish yasab qo'yardi.
 */
export class NoAnswerError extends ApiError {
  constructor() {
    super('network', { code: 'javob_kelmadi' })
    this.name = 'NoAnswerError'
  }
}

/** ProblemDetails'ning standart maydonlari — qolganlari xato qiymatlari. */
const STANDARD_FIELDS = ['type', 'title', 'status', 'detail', 'instance', 'traceId', 'errors', 'warnings']

/** Xato kodi shakli: `davomat.obyektdan_uzoq`, `rate_limit.oshib_ketdi`. */
const CODE_PATTERN = /^[a-z0-9_]+(\.[a-z0-9_]+)+$/

/**
 * Server javobidagi ProblemDetails'dan xato yasaydi.
 *
 * Kod — `title` (backend `Error.Code` ni shu yerga yozadi); u kod
 * ko'rinishida bo'lmasa (masalan ASP.NET'ning «So'rov noto'g'ri» sarlavhasi)
 * kod yo'q hisoblanadi va umumiy matn chiqadi.
 */
export function problemToError(status: number, body: unknown): ApiError {
  const problem = body && typeof body === 'object' ? (body as Record<string, unknown>) : {}
  const title = typeof problem['title'] === 'string' ? problem['title'] : null
  const code = title && CODE_PATTERN.test(title) ? title : null

  const details: Record<string, ErrorDetail> = {}
  Object.keys(problem).forEach((key) => {
    if (STANDARD_FIELDS.indexOf(key) >= 0) return
    const value = problem[key]
    if (typeof value === 'string' || typeof value === 'number') details[key] = value
  })

  return new ApiError(status >= 500 ? 'server' : 'rejected', {
    code,
    status,
    details,
    message: typeof problem['detail'] === 'string' ? problem['detail'] : undefined,
  })
}
