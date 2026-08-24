import { z } from 'zod'

import type { AuthUser } from '../api/auth-api'

/**
 * Brauzerda saqlangan foydalanuvchi nusxasini tekshirish.
 *
 * Nega tekshiruv kerak: nusxa eski versiyadan qolgan bo'lishi mumkin
 * (maydon nomi o'zgargan, `permissions` hali yo'q edi). Ishonib olsak —
 * interfeys `undefined` ustida yiqiladi. Mos kelmasa, nusxa yo'q deb
 * hisoblanadi va ma'lumot serverdan qayta so'raladi.
 */
const authUserSchema = z.object({
  id: z.string(),
  login: z.string(),
  fullName: z.string(),
  roles: z.array(z.string()),
  permissions: z.array(z.string()),

  /*
   * ATAYLAB ixtiyoriy va `null` ga keltiriladi.
   *
   * Maydon keyinroq qo'shilgan: brauzerda hali rasm kalitisiz eski nusxa
   * yotgan bo'lishi mumkin. Uni «mos emas» deb rad etsak, foydalanuvchi
   * sahifa yangilaganda menyuda bir zumga «Noma'lum» ko'rinardi — server
   * javobi kelguncha. Yo'qligi esa «rasm yo'q» degan to'g'ri ma'noni beradi.
   */
  photoKey: z
    .string()
    .nullish()
    .transform((value) => value ?? null),
})

export function parseAuthUser(value: unknown): AuthUser | null {
  const result = authUserSchema.safeParse(value)
  return result.success ? result.data : null
}
