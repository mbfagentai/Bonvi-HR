import axios from 'axios'

/**
 * ASP.NET Core'ning standart xato javobi (RFC 9457).
 * Kelishuv: `title` da xato KODI turadi (`auth.notogri_malumot`), matn emas —
 * matnni foydalanuvchi tilida frontend hosil qiladi.
 */
interface ProblemDetails {
  title?: unknown
  detail?: unknown
}

/** Tarmoq uzilganda yoki server o'chiq bo'lganda. */
const NETWORK_CODE = 'auth.server_javob_bermadi'

/** Kod aytilmagan 400/401 — deyarli har doim login/parol mos kelmagani. */
const CREDENTIALS_CODE = 'auth.notogri_malumot'

/**
 * Xatodan tarjima qilinadigan kodni ajratib oladi.
 *
 * Bo'sh satr qaytishi mumkin: bu «kod noma'lum» degani va `useErrorMessage`
 * uni umumiy «Nimadir noto'g'ri ketdi» ga aylantiradi. Xatoni yashirmaymiz,
 * lekin foydalanuvchiga texnik matn ham ko'rsatmaymiz.
 */
export function authErrorCode(error: unknown): string {
  if (!axios.isAxiosError(error)) return ''

  const response = error.response

  // Javobning o'zi yo'q: server o'chiq, DNS yiqilgan yoki timeout
  if (!response) return NETWORK_CODE

  const problem = response.data as ProblemDetails | undefined
  const title = problem?.title

  if (typeof title === 'string' && title.length > 0) return title

  return response.status === 400 || response.status === 401 ? CREDENTIALS_CODE : ''
}
