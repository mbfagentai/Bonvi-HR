/**
 * Server xatosi — KOD, matn emas.
 *
 * Backend `davomat.obyektdan_uzoq` kabi kod qaytaradi, matnni esa
 * ilova o'zining tarjimasidan oladi (`errors.<kod>`), shunda xabar
 * xodimning tilida chiqadi. Tarjimasi topilmasa kodning o'zi
 * ko'rsatiladi — yashirish mumkin emas, aks holda xato jim yo'qoladi.
 */
export type ApiErrorKind =
  /** Server javob berdi va rad etdi (4xx) — `code` to'la. */
  | 'rejected'
  /** 5xx. */
  | 'server'
  /** So'rov umuman ketmadi yoki javob kelmadi. */
  | 'network'
  /** Token yaroqsiz yoki audience mos emas (401/403). */
  | 'unauthorized'

export class ApiError extends Error {
  readonly kind: ApiErrorKind
  readonly code: string | null
  readonly status: number | null
  /**
   * Xato bilan birga kelgan qo'shimcha qiymatlar — masofa, aniqlik,
   * obyekt nomi. Xabar matniga aynan shular qo'yiladi: «Eng yaqin
   * obyekt: Bosh ofis — 420 m» kabi jumla «qayta urinib ko'ring» dan
   * ko'ra foydaliroq.
   */
  readonly details: Record<string, string | number>

  constructor(
    kind: ApiErrorKind,
    options: {
      code?: string | null
      status?: number | null
      details?: Record<string, string | number>
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
