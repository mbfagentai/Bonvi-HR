import { redirect } from '@tanstack/react-router'

import { attendanceScopeRedirect } from '@/features/auth'
import { authStorage } from '@/shared/lib/auth-storage'

/**
 * `_app` qatlamining himoyasi.
 *
 * Bu yerda faqat token BORLIGI tekshiriladi — tekshiruv sinxron va
 * darhol bajariladi, sahifa «bir ko'rinib keyin yo'qolmaydi».
 * Tokenning haqiqiyligini server hal qiladi: yaroqsiz bo'lsa http-client
 * 401 javobini ushlab, uni yangilashga urinadi va uddasidan chiqmasa
 * /kirish ga qaytaradi.
 *
 * `redirect` — foydalanuvchi qayerga bormoqchi bo'lgani. Kirgandan keyin
 * u bosh sahifaga emas, o'sha sahifaga tushadi.
 *
 * A «Davomat mas'uli» stays inside `/verifikatsiya/**`. That also covers the post-login redirect:
 * every target of `login-form.tsx` — `/` included — passes through here. The user comes from the
 * stored copy because a route guard runs outside React; `login` writes it before navigating.
 */
export function appBeforeLoad({ location }: { location: { href: string; pathname: string } }): void {
  if (!authStorage.isAuthenticated) {
    throw redirect({ to: '/kirish', search: { redirect: location.href } })
  }

  const target = attendanceScopeRedirect(authStorage.getUser(), location.pathname)

  if (target !== null) {
    throw redirect({ href: target, replace: true })
  }
}
