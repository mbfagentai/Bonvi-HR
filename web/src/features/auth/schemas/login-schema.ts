import { z } from 'zod'

import { UZ_PHONE_LENGTH } from '../lib/phone'

/**
 * Kirish formasi.
 *
 * Xato XABARI emas, xato KODI saqlanadi (`auth.login_bosh`). Nega: tarjima
 * sxema yaratilganda emas, ekranga chizilganda tanlanadi — foydalanuvchi
 * tilni almashtirsa, ochiq turgan xato ham darhol o'zgaradi.
 * Kodni matnga aylantirish — `useErrorMessage()`.
 */
export const loginSchema = z.object({
  /** Faqat 9 xona: «+998» maydonning o'zgarmas qismi. */
  phone: z
    .string()
    .min(1, 'auth.login_bosh')
    .regex(new RegExp(`^\\d{${UZ_PHONE_LENGTH}}$`, 'u'), 'telefon.uzunlik'),

  password: z.string().min(1, 'auth.parol_bosh'),

  /** Sessiya brauzer yopilgandan keyin ham saqlansinmi. */
  remember: z.boolean(),
})

export type LoginValues = z.infer<typeof loginSchema>
