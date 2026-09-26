import { createFileRoute } from '@tanstack/react-router'

import { RolesPage } from '@/features/roles/components/roles-page'

export interface RolesSearch {
  /**
   * Ochiq muharrir: rol identifikatori yoki yangi rol uchun `new`.
   *
   * Manzilda — loyihaning qat'iy konvensiyasi: sahifa yangilanganda yoki
   * havola boshqa odamga yuborilganda o'sha rol ochiq holda keladi.
   */
  role?: string
}

/**
 * Rollar — egasi o'zi yaratadi, nomlaydi va ruxsatlarini belgilaydi.
 *
 * `foydalanuvchilar.tsx` dagi bilan bir xil naqsh: marshrutda `beforeLoad`
 * yo'q, ruxsatsiz odamga sahifaning o'zi tushunarli holat ko'rsatadi —
 * server `GET /api/roles/catalog` ni ham `roles.manage` bilan himoyalagan.
 */
export const Route = createFileRoute('/_app/rollar')({
  /**
   * Hech qachon xato tashlamaydi (`validateListSearch` qoidasi): noto'g'ri
   * qiymat shunchaki tashlab yuboriladi va ro'yxat ochiladi. Mavjud
   * bo'lmagan identifikatorni esa sahifa o'zi «rol topilmadi» deb aytadi.
   */
  validateSearch: (search: Record<string, unknown>): RolesSearch => {
    const value = search['role']
    return { role: typeof value === 'string' && value.trim() !== '' ? value.trim() : undefined }
  },

  component: RolesPage,
})
