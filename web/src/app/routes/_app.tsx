import { createFileRoute, Outlet, redirect } from '@tanstack/react-router'

import { AppShell } from '@/app/layout/app-shell'
import { authStorage } from '@/shared/lib/auth-storage'

/**
 * Manzilsiz (pathless) qatlam: bu marshrutning URL'da izi yo'q,
 * lekin barcha ichki sahifalar ilova qobig'i ichida ochiladi.
 * Kirish sahifasi shu qobiqdan tashqarida.
 */
export const Route = createFileRoute('/_app')({
  /**
   * Himoya.
   *
   * Bu yerda faqat token BORLIGI tekshiriladi — tekshiruv sinxron va
   * darhol bajariladi, sahifa «bir ko'rinib keyin yo'qolmaydi».
   * Tokenning haqiqiyligini server hal qiladi: yaroqsiz bo'lsa http-client
   * 401 javobini ushlab, uni yangilashga urinadi va uddasidan chiqmasa
   * /kirish ga qaytaradi.
   *
   * `redirect` — foydalanuvchi qayerga bormoqchi bo'lgani. Kirgandan keyin
   * u bosh sahifaga emas, o'sha sahifaga tushadi.
   */
  beforeLoad: ({ location }) => {
    if (!authStorage.isAuthenticated) {
      throw redirect({ to: '/kirish', search: { redirect: location.href } })
    }
  },

  component: () => (
    <AppShell>
      <Outlet />
    </AppShell>
  ),
})
