import axios from 'axios'

/**
 * ASP.NET Core'ning standart xato javobi (RFC 9457).
 *
 * Kelishuv: `title` da xato KODI turadi (`hisob.rasm_turi`), matn emas —
 * matnni foydalanuvchi tilida frontend hosil qiladi (`useErrorMessage`).
 */
export interface ProblemDetails {
  title?: unknown
  detail?: unknown
  /** Server qo'shimcha ma'lumotni shu yerga qo'yadi (masalan ogohlantirishlar). */
  warnings?: unknown
  extensions?: { warnings?: unknown }
}

/** Javob umuman kelmadi: server o'chiq, DNS yiqilgan yoki timeout. */
export const NETWORK_ERROR_CODE = 'auth.server_javob_bermadi'

/**
 * Xatodan tarjima qilinadigan kodni ajratib oladi.
 *
 * NEGA `shared` da: bir xil mantiq S2 (ma'lumotnomalar), S4 (xodimlar) va
 * hisob sozlamalarida kerak bo'ldi. Ikkitagacha nusxa chidasa bo'lardi,
 * uchinchisi esa aniq belgi: bu bo'limga emas, HTTP shartnomasiga tegishli.
 *
 * Bo'sh satr — «kod noma'lum»: `useErrorMessage` uni umumiy
 * «Nimadir noto'g'ri ketdi» ga aylantiradi.
 */
export function problemCode(error: unknown): string {
  if (!axios.isAxiosError(error)) return ''

  const response = error.response
  if (!response) return NETWORK_ERROR_CODE

  const title = (response.data as ProblemDetails | undefined)?.title

  return typeof title === 'string' && title.length > 0 ? title : ''
}
